# Notifications

Nhóm service/rule dùng chung theo nghiệp vụ. Xem [tổ chức feature](../../README.md).

## Ngày hứa giao

`PromisedDeliveryNotifier` dùng chung cho API gửi ngày hứa giao và bốn đường sửa lệnh: cập nhật information/kế hoạch, cập nhật inform/công thức, tạo VA cho lệnh đã có và dùng hàng tồn. Handler sửa chụp ExpectedDate cũ sau khi khóa lệnh, rồi truyền ngày cũ/mới vào publisher sau SaveChanges nhưng trước commit. Các context Manufacturing/Notification cùng trỏ ApplicationDbContext nên notification/inbox/outbox và dữ liệu lệnh nằm trong cùng transaction; lỗi publish rollback toàn bộ.

Chỉ khác phần ngày mới phát topic 55 `sales.merchandise_order.promised_delivery_notified`, kể cả đổi sớm hơn hoặc ngày cũ null. Ngày mới null hoặc cùng phần ngày (dù khác giờ) không phát. Không tạo thông báo cho thao tác tạo/copy lệnh mới. Người nhận/payload/link dùng đúng contract [API ngày hứa giao](../../Commands/NotifyPromisedDelivery/README.md). Luồng sửa không phát thêm cảnh báo ngày legacy topic 18 qua `ExpectedDateWarningAsync`.

Nếu không có ĐHG active cùng company hoặc không có Sale/leader active thì publisher không tạo notification; thao tác sửa hợp lệ vẫn được lưu. API gửi riêng vẫn trả lỗi theo contract cũ trong trường hợp này. Không tự cấp người nhận hoặc mở rộng role để bắt buộc có thông báo.

## Handler sử dụng trực tiếp

- [CreateLabManufacturingFormula](../../Commands/CreateLabManufacturingFormula/)
- [CreateProductionOrderFormula](../../Commands/CreateProductionOrderFormula/)
- [ProcessManufacturingFormula](../../Commands/ProcessManufacturingFormula/)
- [UpdateProductionOrderInform](../../Commands/UpdateProductionOrderInform/)
