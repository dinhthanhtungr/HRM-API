# Lịch và tiến độ LabAdmin

Phân việc, company/owner và Developer xem thử chỉ đọc:
[ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md).

LabAdmin quản lý hai mốc dự kiến của hồ sơ còn mở. Thao tác lịch độc lập với tạo/xác nhận VU,
gửi chip/kg, phản hồi khách và hoàn thành hồ sơ; không dùng việc lưu lịch để thực hiện các bước đó.

## API và dữ liệu

`PATCH /api/v1/plm/sample-requests/{id}/work-schedule`

```json
{
  "expectedUpdatedDate": "2026-11-03T08:00:00",
  "expectedDeliveryDate": "2026-11-06",
  "clearFields": ["expectedPriceQuoteDate"]
}
```

- Route `id` là nguồn chuẩn, không nhận company/status/formula/actor từ client.
- Hai field được phép là `expectedDeliveryDate` (dự kiến có/giao mẫu) và `expectedPriceQuoteDate`
  (dự kiến báo giá), lưu trên SampleRequest cha, chuẩn hóa về ngày không có giờ.
- Field không gửi hoặc gửi null là không đổi. Muốn xóa phải ghi tên field trong `clearFields`.
  Không được vừa đặt vừa xóa cùng field; field lạ trong `clearFields` bị từ chối.
- `expectedUpdatedDate` phải khớp token hiện hành, kể cả token null nếu server đang trả null.
  Cập nhật kiểm tra lại company, visibility, active, trạng thái còn mở và token ngay trong SQL UPDATE.
- Response là OperationResult<Guid> hiện hành; thành công HTTP 200, từ chối/validation/concurrency HTTP 400.
  Không có thay đổi thì trả thành công nhưng không tạo audit hoặc đổi token.

Ngày dự kiến chỉ là kế hoạch, không phải ngày báo giá thực tế/ngày gửi mẫu. Có thể sửa hai ngày khi có quyền;
việc thiếu mốc nào để vào nhóm Cần lập lịch do read model theo nhánh quyết định.

Chế độ LabAdmin vẫn trả nút Cập nhật lịch cho hồ sơ đã có ngày, kể cả đang chờ Sale
hoặc duyệt giá, miễn hồ sơ còn mở và người xem có quyền quản lý lịch. Nhóm Cần lập lịch
chỉ lấy việc Lab thiếu mốc. Nhóm **Tiến độ Lab** chỉ lấy hồ sơ còn mở có bước tiếp theo thuộc Lab
(`WorkDecision.Role = Lab`), gồm cả đã có lịch; không lấy việc đang chờ Sale, duyệt giá hoặc rà soát LabAdmin.
LabAdmin mở nhóm này mặc định xem tất cả nhóm Lab, vẫn có thể lọc riêng R&D/Màu; lựa chọn team rõ ràng được giữ nguyên.
Dùng nhóm theo dõi khác để sửa lịch hồ sơ đang chờ bộ phận ngoài Lab.
Hồ sơ cần rà soát dữ liệu vẫn giữ nút ReviewSample; chỉnh lịch qua thao tác bổ sung trong drawer.
Chưa bổ sung ghi chú/lý do điều chỉnh.

## Quyền và lịch sử

Capability riêng `plm.sample-request.work-schedule.manage` có fallback cho LabUser, LabAdmin, Admin, Developer.
LabUser/LabAdmin được sửa lịch và nhận mẫu chuẩn kể cả token explicit cũ chưa có claim tương ứng;
SaleUser không được cấp hai quyền này. Lưu ý từ Lab tiếp tục dùng quyền workflow Lab hiện có.
Người gọi phải authenticated, có employee active cùng company và hồ sơ nằm trong write visibility scope.
Quyền quản lý lịch không mở quyền công thức, giá/cost hoặc sửa feedback. Hồ sơ Completed/Cancelled bị từ chối.

Mỗi lần thay đổi ghi AuditLogs với reason `SampleWorkScheduleUpdated`, employee, thời gian và old/new của đúng
các ngày thay đổi. Không đổi status, FormulaId, Trial, workflow event hoặc tạo notification.

Không thêm bảng/cột/migration. Baseline Identity và script idempotent
[seed-sample-work-schedule-permission.sql](../../deployment/seed-sample-work-schedule-permission.sql) bổ sung claim trên bảng sẵn có; không tự chạy script.
Sau khi vận hành chọn chạy seed, cần refresh/login để token có claim mới.

## Kiểm tra

`SampleWorkScheduleTests` kiểm tra ma trận role, anonymous, whitelist/clear conflict, ngày và contract hẹp.
`SampleWorkSchedulePostgresTests` dùng cluster cô lập để kiểm tra lưu ngày + audit, token cũ, company khác,
hồ sơ đóng và không thay status/VU/ngày báo giá thực tế. Cluster test không có bảng workflow/notification.
