# Tóm tắt hiện hành của yêu cầu mẫu

## Phạm vi và triển khai

`SampleRequests:ConversationSummary:Enabled` mặc định `false`. Cần triển khai bảng
`SampleRequests.SampleRequestConversationSummaries` đúng EF configuration, sau đó bật
cấu hình và nạp lại ứng dụng. Task này chỉ thêm entity/config/code; không có migration,
script triển khai, truy cập hoặc thay đổi DB thật. Khi tắt, worker không đọc DB cho tính
năng này, SaveChanges không xếp job và API không truy cập bảng mới. FE ẩn thẻ hiện hành.

Entity có khóa chính `(CompanyId, SampleRequestId)`, khóa ngoại SampleRequest xóa Cascade:

| Field | Ý nghĩa |
| --- | --- |
| `ContentJson` | Mảng `{Code, Value}` của bản dựng đã lưu, không phải message/payload notification |
| `FormatVersion` | Phiên bản whitelist/định dạng nội dung; hiện là 1 |
| `AppliedSourceFingerprint` | SHA-256 của phiên bản + nội dung hiển thị chuẩn hóa |
| `SyncedAt` | Giờ server lưu một bản nội dung mới, không phải giờ sửa hồ sơ hoặc lần GET gần nhất |
| `Status`, `LastErrorCode` | Trạng thái bản dựng lưu; retry và lỗi xử lý chính nằm ở outbox |

Giá, cost, margin, công thức và LabComment không thuộc whitelist nhóm Chung.
Whitelist hiện gồm mã/loại yêu cầu, tên/mã/tên màu, nhựa/ứng dụng/sản phẩm sử dụng,
yêu cầu sản phẩm/thời tiết/ánh sáng/kiểm tra, sản lượng/số lượng mẫu, đóng gói/khối lượng bao,
mã sản phẩm khách, ngày yêu cầu/dự kiến giao/thử mẫu/dự kiến báo giá và ghi chú Sale/bổ sung.
Giá trị rỗng bị bỏ; số/ngày dùng định dạng invariant. Fingerprint chỉ đối chiếu dữ liệu
đang hiển thị, không coi đổi timestamp hoặc một field giá riêng là đổi nội dung tóm tắt.

## Khi nào đồng bộ

SaveChanges bắt thay đổi scalar của SampleRequest/Product và audit của các lệnh
ExecuteUpdate hiện hành (lịch hẹn, ngừng hồ sơ, bổ sung tên màu). Nó chỉ thêm outbox
`SampleRequestConversationSummarySync` gồm Version, CompanyId, EntityId, Kind và Cursor;
không dựng nội dung, không tìm mọi hồ sơ liên quan trong POST/PATCH. Nguồn và job nằm
trong cùng giao dịch lưu. Chi phí thêm là INSERT outbox, không cam kết latency bằng 0.

Worker riêng xử lý tối đa 5 job nối tiếp mỗi vòng, nghỉ 1 giây; dùng scope/DbContext mới
cho từng job. Job Request đọc đúng một hồ sơ. Job Product phân trang khóa tối đa 100
hồ sơ cùng company/ProductId và ghi một job tiếp nối nếu còn; không quét 25k mỗi lần sửa.
Chạy nhiều instance vẫn khóa hàng outbox và khóa advisory theo company trước khi đọc
nguồn; không dựng từ payload cũ. Tối đa 100 job trùng payload có mặt trước lần đọc nguồn
được gộp xử lý. Job tới sau vẫn pending. Projection + đánh dấu job commit nguyên tử;
retry cùng nội dung không đổi SyncedAt, không tạo thêm bản tóm tắt. Sau 5 lỗi job dừng;
chỉ lưu mã `ConversationSummary.SyncFailed`, không lưu exception/note vào error.

Không gọi publisher, SignalR hoặc Web Push; không tạo message/read state, đổi unread,
LastMessageAt hay thứ tự hội thoại. Hồ sơ inactive, sản phẩm inactive/khác company và
hồ sơ nội bộ/private/KH_VIETAUS không trả tóm tắt; worker xóa projection cũ khi xử lý.

Thay đổi bằng SQL trực tiếp hoặc ứng dụng khác không đi qua SaveChanges/audit này sẽ
không tự tạo job. GET vẫn đối chiếu nguồn và phát hiện bản đã cũ; Dev có thể xếp lại.
Không có quét toàn bộ dữ liệu lúc khởi động, lúc GET hoặc mỗi POST/PATCH.

## API đọc và trạng thái

GET `/api/v1/internal-mail/conversations/{id}/sample-request-summary` chỉ nhận ID
nhóm Chung SampleRequest được `InternalMailAreaAccessService` cho phép xem, cùng company,
active và current employee hợp lệ. Không lấy quyền xem công thức làm quyền đọc nhóm Chung;
ID Kỹ thuật/Giá, nhóm không được xem hoặc company khác trả 404. GET không ghi dữ liệu.

Ví dụ: `{ "status": "Synced", "syncedAt": "2026-11-07T10:00:00", "fields":
[{ "code": "requestCode", "value": "TP_TEST" }] }`.

| status | Ý nghĩa |
| --- | --- |
| Disabled | Chưa bật tính năng; fields rỗng |
| Unavailable | Nguồn không còn hợp lệ hoặc bị suppress; fields rỗng |
| Historical | Chưa có bản dựng và chưa có job Request đang chờ; giữ tin ban đầu làm lịch sử |
| Pending | Có job Request chưa hết lượt; có thể kèm bản đã lưu gần nhất |
| Failed | Chưa đồng bộ và job Request đã hết 5 lượt; có thể kèm bản cũ |
| OutOfDate | Nội dung/phiên bản nguồn khác bản lưu nhưng chưa có job Request đang chờ |
| Synced | Phiên bản và fingerprint của bản lưu khớp dữ liệu nguồn vừa đọc |

Job Product chưa fan-out có thể khiến GET hiện OutOfDate/Historical trong thời gian chờ.
Không đánh dấu Synced chỉ dựa vào timestamp hay cờ Status trong bảng. Nội dung của phiên
bản không hỗ trợ không được trả như bản hiện hành. Các ngày null nghĩa là chưa có bản
lưu; fields [] không có nội dung dựng để hiển thị, không phải lịch sử tin nhắn rỗng.

FE có thẻ thu gọn **Thông tin hiện tại** riêng trong nhóm Chung. Chỉ hội thoại đang mở
có một query theo user/company/auth generation: 5 giây khi Pending, 30 giây
ở trạng thái khác, dừng khi tab bị ẩn/mất focus, lỗi, Disabled hoặc Unavailable. Chuyển
nhóm/hồ sơ hủy request qua AbortSignal; không invalidate feed/inbox/unread summary.
Tin lịch sử không bị sửa/xóa và không giả là bản hiện tại.

## Dữ liệu cũ và công cụ Dev

GET `/api/v1/dev/sample-request-summary-sync`: capability view job; trả Enabled và số
job Pending/Failed/Completed của company hiện tại. Đây là số job (kể cả job Product và
job trùng được gộp), không phải số hồ sơ đã đối chiếu. Completed không nghĩa mọi hồ sơ
hiện tại đều có cùng fingerprint; trạng thái từng hồ sơ do API đọc ở trên xác minh.

POST cùng route: cần cả `dev.workflow-notification-jobs.view` và `.retry`; body
`{cursor:null,take:100,sampleRequestId:null}`. Company lấy từ phiên, không nhận từ client.
take 1–100; cursor là Guid exclusive. sampleRequestId tùy chọn chỉ xếp một hồ sơ active
trong company (không nhận ID từ company khác). Response gồm queued, nextCursor, hasMore,
enabled; queued chỉ là đã xếp, không phải worker hoàn thành. Thiếu quyền trả 403.

Màn Dev Job thông báo có nút Bắt đầu/Tạm dừng/Tiếp tục/Đặt lại và Kiểm tra trạng thái;
mỗi dòng job workflow có nút đồng bộ **hiện tại** riêng, khác khôi phục tin lịch sử.
UI xếp tối đa 100/lần và chờ khi snapshot có >=200 job pending. Tạm dừng/hủy màn hình
chỉ dừng xếp thêm, không hủy job đã commit. Cursor và bộ đếm trong phiên UI; đổi phiên,
company hoặc rời màn hình sẽ khởi đầu lại. Nếu request bị hủy sau server đã commit,
chạy lại cùng lô vẫn an toàn nhờ khóa chính/fingerprint/coalescing; có thể có thêm job
nhưng không thêm bản tóm tắt hay thông báo trùng. Kiểm tra trạng thái đọc snapshot mới,
không polling toàn hệ thống tự động khi người dùng chưa bấm chạy.

Mã cũ không được tự backfill lúc khởi động. Chỉ hồ sơ/sản phẩm vừa được chỉnh hoặc lô
Dev chủ động chạy mới được xử lý. Khi đã sửa nguyên nhân job lỗi, bấm đồng bộ hồ sơ
hoặc chạy lại lô sẽ xếp job mới; worker gộp job lỗi cũ cùng target khi thành công.
