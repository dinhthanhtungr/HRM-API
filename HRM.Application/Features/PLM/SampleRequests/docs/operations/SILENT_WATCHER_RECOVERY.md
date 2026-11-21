# Đối soát và khôi phục người xem im lặng

Dev → Job thông báo: dòng SampleRequestWorkflowCreated có canReconcileWatchers=true
hiện nút **Đối soát người xem**. Xem trước rồi bấm **Khôi phục người còn thiếu**.
Thực hiện theo từng hồ sơ/job đã chọn, không tự ghi khi mở màn hoặc chạy cả công ty.
Muốn tìm hồ sơ có job đã hoàn tất, chọn trạng thái Tất cả.

## Quyền và phạm vi

[Authorize], employee/company hợp lệ và cả hai capability hiện có
dev.workflow-notification-jobs.view + dev.workflow-notification-jobs.retry.
Đây là phục hồi một phần tác động của job khởi tạo, không phải API thêm người tùy ý.
Baseline Developer; thiếu quyền trả 403 trước query. Không thêm permission/seed/schema.
Company/actor lấy từ token; ID job khác công ty không trả dữ liệu.

Chỉ nhóm Chung hiện có của yêu cầu active cùng company, đúng một root và cấu trúc
hợp lệ theo cờ GroupStorage. Không tạo conversation, mapping hoặc nhóm con; không áp
dụng DefaultMembershipPolicy. API đồng bộ group-storage hiện có vẫn đảm nhiệm mapping
và bù President Kỹ thuật/Giá, không bị nhân đôi tại đây.

## Nguồn và kế hoạch

Outbox phải là SampleRequestWorkflowNotification trỏ đúng event
SampleRequestWorkflowCreated cùng công ty, IsInitialMessage=true và tác giả gốc
thuộc company. Không đoán thành viên khi không còn nguồn job hợp lệ.

- SilentWatchers khác null: dùng đúng ID đã lưu, kể cả mảng rỗng [].
- SilentWatchers=null và UseDefaultSilentWatchers=true: dùng resolver hiện tại,
  loại tác giả gốc. Resolver hiện chọn LabUser/LabAdmin, không cấp xem toàn công ty.
- Không yêu cầu watcher trong nguồn: danh sách rỗng.
- Kiểm tra employee active/company và hạn chế sản phẩm/R&D qua Recipients hiện có.
- Giữ riêng người gửi, Sale phụ trách, Recipients/ExtraRecipients đã lưu, người bắt
  buộc nhận theo category và leader/admin Sales của người gửi. Ứng viên chưa có
  membership thuộc tập này là NotificationRecipient, không tự thêm làm watcher.
- Membership đang có không đổi: ExistingWatcher hoặc ExistingMember; inactive là
  Removed và không tái kích hoạt. Không đổi mute/archive/role/JoinedAt cũ.
- Nhân viên ngoài company không được trả ID/tên. Nhân viên cùng company không đủ
  điều kiện là Ineligible. Chỉ Missing được thêm.

Đây là đối soát với rule hiện tại + ý định đã lưu. Role/team có thể đã đổi từ lúc tạo,
nên cần preview; không khẳng định phục hồi một danh sách lịch sử nguyên vẹn nếu nguồn
chỉ lưu cờ dùng mặc định.

## API

GET `/api/v1/dev/workflow-notification-jobs/{jobId:long}/silent-watchers`

POST `/api/v1/dev/workflow-notification-jobs/{jobId:long}/silent-watchers/restore`
với body `{"fingerprint":"<64 ký tự từ preview>"}`. Không nhận employee IDs từ FE.

```json
{"outcome":"Preview","sampleRequestCode":"TP_31074","fingerprint":"...","rows":[{"employeeId":"...","name":"Tên nhân viên","status":"Missing"}],"missingCount":1,"addedCount":0}
```

Rows là ứng viên watcher, không phải toàn bộ thành viên/người nhận thông báo. Status:
Missing, ExistingWatcher, ExistingMember, Removed, Ineligible, NotificationRecipient.
missingCount tính theo Rows, addedCount=0 trên preview. Chỉ trả tên nhân viên cùng
company phục vụ đối soát, không trả body tin, payload job, SQL, lỗi thô, liên lạc/giá.

Khi xác nhận, server khóa company trước rồi root conversation, đọc lại membership và
tính lại kế hoạch. Fingerprint SHA-256 gắn company/root/job, nguồn watcher và ID/status;
đây là mã đối chiếu, không phải quyền truy cập. Còn Missing mà kế hoạch đổi → 409
PreviewChanged, FE yêu cầu Đối soát lại. Source/structure lỗi → 409
InvalidSource/UnsupportedSource/Unavailable; fingerprint sai dạng → 409 InvalidPreview.
Các nhánh bị chặn không insert.

Chỉ insert Missing với Watcher, IsMuted=true, IsActive=true, IsArchived=false,
JoinedAt=thời điểm phục hồi. Insert kiểm tra lại employee/root/sample active cùng
company, ON CONFLICT (ConversationId,EmployeeId) DO NOTHING. Hai request đồng thời hay
chạy lại không nhân đôi và không overwrite. 200 Restored có addedCount thực tế đã
commit; Rows vẫn là kế hoạch trước ghi, fingerprint=null; FE tải preview mới.
200 AlreadyCurrent khi không còn Missing, không ghi gì; vẫn có thể có Removed/Ineligible.
Không kết luận rollback khi mất kết nối lúc commit; đối soát lại an toàn.

## Tác động và vận hành

Chỉ thêm Watcher còn thiếu ở Chung. Không sửa người nhận hiện có, tạo notification/
NotificationUserState/SignalR/Push/tin hệ thống/read-state lịch sử, hoặc đổi
LastMessageId/At, outbox, event nghiệp vụ. Mute/mention/delivery tương lai tiếp tục
theo cơ chế hiện hành. Không đổi nhóm Kỹ thuật/Giá/Custom. Log chỉ có actor/company/
job/số thêm, không tên/body/payload. Không tự gọi API phục hồi trên DB ứng dụng khi
triển khai code. Đóng dialog sau phục hồi rồi mở lại nhóm Chung để tải thành viên mới.

## Kiểm chứng

PostgreSQL test dùng cluster mới biệt lập, không đọc cấu hình DB ứng dụng: preview
không ghi, chặn company khác, bỏ qua inactive/foreign, giữ nguyên người đã có/bị gỡ/
thuộc diện nhận thông báo, stale fingerprint không ghi, hai caller chỉ thêm một lần,
explicit [] không thành default. DB test không có bảng read-state/notification/
mapping nên tác động ghi ngoài phạm vi sẽ làm test lỗi.
FE test kiểm tra mở dialog không mutate, chỉ gửi fingerprint sau xác nhận, chặn khi
không còn Missing và ẩn preview khi đổi phiên/company.
