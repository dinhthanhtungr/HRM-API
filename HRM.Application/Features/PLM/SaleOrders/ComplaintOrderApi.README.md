# API tạo ĐHG khiếu nại

Kết quả tìm ĐHG có thêm `vaCodes`, gồm mã VA sản xuất hoặc giao hàng trên các dòng của đơn, đã bỏ trùng. Đơn chưa có VA đủ điều kiện trả mảng rỗng. Khi chọn ĐHG, danh sách VA theo dòng vẫn được trả riêng để kiểm tra lựa chọn khi lưu.

Khi truy vấn theo `orderId`, mỗi dòng có thêm `productCode`, `formulaId`, `formulaCode` và `hasDeliveredQuantity`. Form dùng các dòng có `hasDeliveredQuantity = true` để điền sẵn sản phẩm và công thức VU. Giống chi tiết ĐHG, API tính các phiếu giao còn hiệu lực, chưa hủy và bỏ qua dòng đính kèm. Dòng cần có số lượng giao dương để được điền sẵn. Sale tự nhập số lượng khiếu nại và có thể bỏ trống VA nguồn.

- `GET /api/v1/plm/sale-orders/complaint-sources?customerId=...&keyword=DH001` trả `orders` gồm `id`, `code`, `date`. Thêm `orderId` để lấy `lines` gồm ID dòng gốc, sản phẩm và danh sách `va` gồm `manufacturingFormulaId`, `externalId`. Mỗi phản hồi có hai danh sách, danh sách ngoài nhánh đang truy vấn để trống. Đơn được giới hạn theo công ty và khách hàng.
- Khi POST ĐHG loại `Complaint`, gửi `sourceMerchandiseOrderId` ở đầu đơn. Gửi `sourceMerchandiseOrderDetailId` trên dòng nếu sản phẩm có nhiều dòng khớp trong đơn gốc. `replacementSourceManufacturingFormulaId` là tùy chọn và chỉ nhận VA thuộc dòng nguồn.
- API lưu phiếu nháp và dòng khiếu nại trong cùng giao dịch tạo ĐHG. Chi tiết ĐHG trả liên kết đơn gốc, dòng gốc và VA đã lưu trên từng dòng. Đơn cũ chưa có liên kết trả `null`.
- Khi tạo công thức cho lệnh có VA nguồn, API công thức lấy thành phần từ VA đã chọn và cấp mã hậu tố tăng dần. Dòng bỏ trống VA dùng luồng hiện hành. Với VA nguồn, tạo lệnh trước rồi gọi API tạo công thức trên lệnh.
- Khi lưu ĐHG khiếu nại có VA nguồn, API tạo một **PHIẾU YÊU CẦU NHẬP HÀNG TRẢ VỀ TỪ KH** trong cùng giao dịch. Phiếu ở trạng thái `Pending`, loại `ImportReturn` (11), và `codeFromRequest` là mã ĐHG khiếu nại mới. Phiếu chỉ gồm các dòng có VA nguồn, lấy mã VA làm mã lô dự kiến, số kg từ số lượng yêu cầu của dòng và phân loại chờ QC. Việc tạo phiếu yêu cầu chưa ghi nhận nhập kho thực tế.
