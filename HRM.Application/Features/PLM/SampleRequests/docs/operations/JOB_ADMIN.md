# Quản trị job thông báo workflow

Thao tác phục hồi thành viên: [Đối soát người xem im lặng](SILENT_WATCHER_RECOVERY.md).

Trang FE `/dev/notification-jobs` nằm trong mục Dev. Phạm vi bản đầu là outbox
`SampleRequestWorkflowNotification` của công ty hiện tại, có sự kiện và hồ sơ nguồn
cùng công ty và hồ sơ còn active. Không phải bảng quản lý mọi loại job trong hệ thống; không suy luận
tin tóm tắt bị thiếu đồng nghĩa có job lỗi. Hồ sơ không có outbox/sự kiện hoặc payload
không liên kết đúng không nằm trong danh sách, cần điều tra riêng, không tạo bù tự động.

## Quyền và API

`[Authorize]`, authenticated user, EmployeeId và CompanyId bắt buộc. Capability
`dev.workflow-notification-jobs.view` để đọc; retry cần thêm
`dev.workflow-notification-jobs.retry`. Baseline/fallback chỉ Developer. Môi trường
dùng explicit permission token cần seed role claims rồi login/refresh; code không tự
chạy seed. Script seed/rollback cạnh tài liệu này; không thêm schema hoặc migration.
API không nhận company/actor từ client. ID khác công ty không tiết lộ tồn tại.

- GET `/api/v1/dev/workflow-notification-jobs?sampleRequestCode=TP_31075&status=Attention&pageNumber=1&pageSize=20`
- POST `/api/v1/dev/workflow-notification-jobs/{jobId:long}/retry`, không có body.
- POST `/api/v1/dev/workflow-notification-jobs/{jobId:long}/restore-summary`, không có body.

Mã hồ sơ là phép so sánh chính xác (trim, tối đa 80 ký tự), không tìm substring.
`status`: Attention (mặc định), Failed, Delayed, Pending, Completed, All.
Page clamp 1..1000000; size 1..50; thứ tự chưa xử lý trước, cũ trước, Id tăng dần.
Filter không hợp lệ trả 400; thiếu quyền/context trả 403.
Nhánh thiếu quyền/context trả kết quả từ chối trước khi truy cập DB, không ném
UnauthorizedAccessException; controller ánh xạ thành 403. Quyền explicit vẫn đọc từ
token hiện tại: chạy seed trong DB không cập nhật token đã phát, cần login/refresh.

POST không khả dụng hoặc
bị khóa trả 409 với outcome Unavailable. GET trả PagedResult:

```json
{"items":[{"jobId":"123","eventId":"...","sampleRequestId":"...","sampleRequestCode":"TP_31075","eventType":"LabStarted","status":"Failed","createdAt":"2026-11-03T08:00:00","processedAt":null,"attempts":5,"maxAttempts":5,"errorCode":"WorkflowNotification.MissingPresident","canRetry":true}],"totalCount":1,"pageNumber":1,"pageSize":20,"totalPages":1,"hasPreviousPage":false,"hasNextPage":false}
```

`jobId` là chuỗi để tránh mất chính xác bigint ở JS. createdAt/processedAt là mốc
outbox gốc, cùng quy ước thời gian hiện tại của worker; retry không đổi createdAt.
Attempts là số lần thử trong chu kỳ hiện tại, reset khi retry, không phải tổng lịch sử.
errorCode null khi chưa lỗi/đã reset; lỗi cũ không thuộc enum được thay bằng
WorkflowNotification.Unknown. Không trả raw error, payload, nội dung chat, ghi chú,
người nhận, giá hoặc thông tin khách hàng. EventType là mã sự kiện kỹ thuật.

## Trạng thái và chống trùng

- Completed: processedAt có giá trị và không có error, có thể worker đã chủ động không phát thông báo.
- NeedsReview: processedAt có giá trị nhưng còn error; không khẳng định đã tạo tin/notification.
  Lỗi worker cũ `No handler for type SampleRequestWorkflowNotification` được map thành
  `WorkflowNotification.LegacyHandlerMissing`, không trả raw error. Nhóm này có trong
  Attention/All, không có trong Completed và không cho retry mù quáng.
- Failed: chưa xử lý và attempts >= 5.
- Delayed: chưa xử lý, chưa hết lượt thử và createdAt quá 10 phút.
- Pending: các job chưa xử lý còn lại.
- Attention: Failed, Delayed hoặc NeedsReview. Chờ lâu là chỉ báo tuổi job, chưa phải xác nhận kẹt.

canRetry cần quyền retry, Failed và event.NotificationId chưa có. POST khóa chính
dòng outbox bằng FOR UPDATE OF o SKIP LOCKED, cùng lock worker; tái kiểm tra sau lock,
chỉ reset Attempts/Error, không tạo job hay phát lại sự kiện nghiệp vụ. Một worker
đang dispatch không bị giành job. Hai request retry nối tiếp: request sau nhận
AlreadyQueued; job đã xử lý/đã có notification nhận Completed; thay đổi thành công
nhận Queued. Response 200 `{"outcome":"Queued"}` chỉ xác nhận vào hàng chờ, chưa
khẳng định gửi thành công. FE reload sau retry và có nút Làm mới xem kết quả.

Giữ nguyên EventId/PayloadJson và cơ chế stable message ID của dispatcher hiện hữu.
Audit vận hành qua log có actor/company/job và attempts cũ, không log payload.
Không chạy replay lịch sử/bulk repair trong tác vụ triển khai. Rollback code bằng
gỡ route/menu/capability mới, không ảnh hưởng worker/per-record retry cũ.

## Nút khôi phục summary lịch sử

DTO thêm `canRestoreSummary` và `hasMessage`. canRestoreSummary chỉ bật khi có quyền
view + retry, job đã processed với đúng lỗi legacy handler không hỗ trợ, event là
SampleRequestWorkflowCreated, payload IsInitialMessage=true còn Message không rỗng,
event chưa liên kết message/notification. hasMessage cho biết event đã có MessageId,
không phải bằng chứng notification đã gửi. Job vẫn giữ NeedsReview/error lịch sử sau
phục hồi; FE hiển thị Đã có tin tóm tắt khi event có liên kết, không còn nút phục hồi.

Endpoint restore-summary khóa job, event rồi root conversation cùng công ty. Phải có
không quá một nhóm Chung active, hồ sơ active và tác giả gốc thuộc công ty. Chỉ chèn tin
thiếu bằng event ID ổn định, body từ payload lưu sẵn và SentAt=SampleRequest.CreatedDate.
Không dựng nội dung từ hồ sơ mới nhất. Nếu chưa từng có hội thoại, dùng SendSampleRequestMessage
ở chế độ nội bộ RestoreInitialSummaryHistory + DeferNotification để tạo nhóm Chung và
thành viên qua resolver hiện hành (phân loại Lab, Sale, watcher và company scope).
Chỉ sử dụng extras/watchers hợp lệ trong payload gốc, không nhận company/actor từ caller.
CreatedAt/SentAt/LastMessageAt của nhóm mới dùng giờ tạo hồ sơ; không tạo unread/read receipts
hoặc Notification/UserState/SignalR/Push. Không tự tạo Kỹ thuật/Giá; không tái tạo nhóm inactive.
Nhóm đã tồn tại vẫn dùng nhánh bổ sung tin, không sửa membership/read-state/latest.
Kiểm tra trùng theo ID toàn cục, cùng body trong nhóm và workflowEventId metadata,
kể cả tin đã xóa; xung đột trả 409 RecoveryConflict, job bận/khác scope trả 409 Unavailable.
Nguyên nhân cụ thể: RecoverySourceMissing (thiếu nguồn), RecoveryMultipleConversations
(nhiều root), RecoveryConversationUnavailable (không tạo được nhóm theo quyền/người nhận hiện hành).
Tin cùng ID chỉ được coi AlreadyRestored khi khớp nhóm, tác giả, body, thời điểm và
chưa bị xóa; trường hợp này đồng bộ liên kết event còn thiếu. Thành công 200 Restored
hoặc AlreadyRestored. 403 giữ nguyên khi thiếu quyền/context; không trả body tin ra API.

Nhánh nhóm đã có chỉ cập nhật InternalMessage và event.MessageId/ConversationId; nhánh chưa có
tạo thêm root/membership theo resolver và gắn tin đầu tiên với giờ gốc. Giữ nguyên job,
read states, unread và Notification/UserState/SignalR/Push; không sửa nhóm khác.
Không replay LabFormulaConfirmed qua nút này vì đó là side effect nghiệp vụ khác.
Audit log chỉ có actor/company/job, không có nội dung. Nút yêu cầu xác nhận tác động
trước khi gửi POST, không tự gọi recovery khi mở màn hoặc lọc dữ liệu.

## Verification

Các test targeted bao gồm PostgreSQL tạm biệt lập: lọc công ty, chặn retry
khác công ty, SKIP LOCKED khi worker giữ lock, giữ nguyên job/payload, retry lặp
trả AlreadyQueued và job processed không reset. DB ứng dụng không được dùng trong test.
Regression processed kèm error xác nhận job xuất hiện trong Attention với NeedsReview,
không nằm trong Completed và không được retry tự động.
BE build và FE production build/lint đã qua; còn warning dependency ImageSharp và
warning test cũ ngoài phạm vi. Kiểm tra trình duyệt chưa hoàn tất do Chrome CDP timeout.
