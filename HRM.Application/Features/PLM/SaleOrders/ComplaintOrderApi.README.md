# API tạo ĐHG khiếu nại

- `GET /api/v1/plm/sale-orders/complaint-sources?customerId=...&keyword=DH001` trả `orders` gồm `id`, `code`, `date`. Thêm `orderId` để lấy `lines` gồm ID dòng gốc, sản phẩm và danh sách `va` gồm `manufacturingFormulaId`, `externalId`. Mỗi phản hồi có hai danh sách, danh sách ngoài nhánh đang truy vấn để trống. Đơn được giới hạn theo công ty và khách hàng.
- Khi POST ĐHG loại `Complaint`, gửi `sourceMerchandiseOrderId` ở đầu đơn. Gửi `sourceMerchandiseOrderDetailId` trên dòng nếu sản phẩm có nhiều dòng khớp trong đơn gốc. `replacementSourceManufacturingFormulaId` là tùy chọn và chỉ nhận VA thuộc dòng nguồn.
- API lưu phiếu nháp và dòng khiếu nại trong cùng giao dịch tạo ĐHG. Chi tiết ĐHG trả liên kết đơn gốc, dòng gốc và VA đã lưu trên từng dòng. Đơn cũ chưa có liên kết trả `null`.
- Khi tạo công thức cho lệnh có VA nguồn, API công thức lấy thành phần từ VA đã chọn và cấp mã hậu tố tăng dần. Dòng bỏ trống VA dùng luồng hiện hành. Với VA nguồn, tạo lệnh trước rồi gọi API tạo công thức trên lệnh.
