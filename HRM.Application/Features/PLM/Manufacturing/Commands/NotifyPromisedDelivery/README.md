# Thông báo ngày hứa giao MFG

`POST /api/v1/plm/production-orders/notify-promised-delivery`

Yêu cầu đăng nhập, CompanyId/EmployeeId hợp lệ và capability `plm.production-order.manage`.
BE lấy company và người gửi từ phiên đăng nhập; FE không truyền người nhận.

```json
{
  "externalId": "MFG261000123",
  "promisedDeliveryDate": "2026-10-20"
}
```

`externalId` là mã **MfgProductionOrder**, không phải mã ĐHG: trim, khớp chính xác,
không rỗng, tối đa 200 ký tự. Ngày ISO `yyyy-MM-dd` bắt buộc, không chuyển timezone;
cho phép ngày trong quá khứ để thông báo hồi tố.

BE yêu cầu duy nhất một MFG active trong company và truy MfgOrderPO bằng ID nội bộ.
Chỉ xét link, MerchandiseOrderDetail và MerchandiseOrder active, ĐHG cùng company.
Người nhận là ManagerById (sale phụ trách) active/cùng company của các ĐHG và leader
active trong nhóm sale `CMR`/`CMR.*`, qua ISaleGroupRecipientResolver. Gộp trùng
employee; không gửi cho toàn bộ role Leader, không tự thêm CreatedBy của ĐHG.
Người gửi nhận nếu thuộc tập này; policy chung vẫn tự thêm Developer active.

Không cập nhật ExpectedDate, RequiredDate, lịch sản xuất hay ĐHG; không tạo message
InternalMail. PublishAsync lưu notification, recipient audit, inbox/unread và outbox
SignalR/Web Push theo cấu hình hiện có.

Topic `MerchandiseOrderPromisedDeliveryNotified = 55` có code
`sales.merchandise_order.promised_delivery_notified`, category `sales_order`, event group
`schedule`, aggregate `MerchandiseOrder`. Title là MerchandiseOrder.ExternalId;
nếu có nhiều ĐHG, ghép các mã phân biệt theo thứ tự tăng dần. AggregateId/Code và
link dùng ĐHG đầu tiên, sắp theo ExternalId rồi ID để ổn định giữa các lần gọi.
Link `/plm/sale-orders?keyword=<MerchandiseOrder.ExternalId>` có encode mã.
Payload thêm `manufacturingOrder: { id, externalId }` để giữ thông tin MFG nguồn.
Payload gồm `promisedDeliveryDate` từ
request và `merchandiseOrders` từ DB: mỗi item có `id` = MerchandiseOrderId và
`externalId` = MerchandiseOrder.ExternalId. Không có giá/cost. Nội dung chứa mã MFG,
ngày và các mã ĐHG. FE có thể tạo link riêng cho từng item trong merchandiseOrders.

Thành công trả HTTP 200:

```json
{
  "success": true,
  "message": "Đã tạo thông báo ngày hứa giao.",
  "data": "00000000-0000-0000-0000-000000000001"
}
```

`data` là ID notification đã lưu. HTTP 400 với `success:false` khi thiếu quyền/context,
input không hợp lệ, MFG không tồn tại/không active/trùng mã, không có ĐHG active hoặc
không có sale/leader active. Lỗi model binding dùng ValidationProblemDetails mặc định;
chưa đăng nhập trả 401. Không có người nhận thì không tạo notification.

Nếu ngày request trùng phần ngày của MFG.ExpectedDate hiện tại (bỏ giờ, không đổi timezone),
trả HTTP 200 với `success:true`, `data:"00000000-0000-0000-0000-000000000000"`,
`message:"Ngày hứa giao không thay đổi; không tạo thông báo."`.
Nhánh này vẫn kiểm tra quyền, input và MFG cùng company/active/duy nhất; bỏ qua resolve người nhận và PublishAsync,
không tạo notification, inbox hoặc outbox. ExpectedDate null được xem là khác ngày request hợp lệ.
Ngày khác dù sớm hơn hay muộn hơn đều tạo thông báo theo rule người nhận ở trên.
Mỗi POST có ngày khác ExpectedDate vẫn tạo notification mới; chưa có idempotency.
FE khóa nút khi đang gửi và không tự retry sau mất response để tránh gửi trùng.

Nội dung: `Ngày hứa giao đổi từ 19/10/2026 thành 20/10/2026. Lệnh: MFG... ĐHG: DHG...`.
Ngày cũ lấy từ ExpectedDate đang lưu của MFG tại thời điểm gọi, chỉ lấy phần ngày;
nếu null thì hiển thị `chưa xác định`. Payload `previousPromisedDeliveryDate` là ngày
cũ ISO hoặc null; `promisedDeliveryDate` là ngày mới từ request. FE gọi trước khi
lưu ngày mới nếu muốn giữ đúng ngày cũ. Endpoint này chỉ thông báo, không lưu thay đổi ngày.
