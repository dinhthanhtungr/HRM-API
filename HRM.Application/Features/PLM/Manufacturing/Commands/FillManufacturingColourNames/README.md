# Bổ sung snapshot tên màu MFG còn NULL

`POST /api/v1/plm/production-orders/fill-colour-names?dryRun=true` xem trước.
`dryRun=false` mới ghi DB. Không có request body, không nhận company/actor từ client.

Yêu cầu đăng nhập, policy `PLM.ProductTechnicalInfo.Edit`, capability
`plm.production-order.view`, company và employee hợp lệ. Chỉ xét MFG active của
công ty hiện tại có `ColorName IS NULL`; chuỗi rỗng/khoảng trắng/giá trị cũ giữ nguyên.
Nguồn duy nhất là `Product.ColourName` không rỗng, Product active cùng công ty và
đúng `MFG.ProductId`, giống nguồn khi tạo MFG. Product thiếu màu/không hợp lệ thì bỏ qua;
không tự suy từ tên, không sửa Product hoặc snapshot tên/mã sản phẩm.

Nên chạy API fill-colour-names của Product trước nếu Product còn thiếu màu.
Chạy API này độc lập, không làm thay đổi contract hay side effect của API Product.
Không giới hạn trạng thái MFG: chỉ bổ sung metadata màu, không sửa NVL/Std/lot,
công thức, lịch, kho hoặc trạng thái. Không migration, không gửi notification.

Response `data`: `dryRun`, `examined` (MFG NULL đã xét), `matched` (có nguồn màu hợp lệ),
`updated` (đã ghi, bằng 0 khi xem trước), `concurrentlySkipped` (nguồn/đích đổi trước UPDATE),
`items` (tối đa 100 ứng viên), `hasMoreItems` (còn ứng viên ngoài danh sách mẫu).
Mỗi item gồm `orderId`, `externalId`, `productId`, `productNameSnapshot` đã lưu và
`proposedColourName` từ Product hiện tại; null nghĩa không có nguồn hợp lệ.
Ví dụ: `{ "orderId": "...", "externalId": "MFG261100001", "productId": "...", "productNameSnapshot": "HẠT MÀU ĐỎ", "proposedColourName": "Red" }`.

Đọc keyset 250 dòng/lượt theo ID; khi ghi recheck target NULL, company/active,
ProductId và màu nguồn. Cập nhật `UpdatedBy`/`UpdatedDate` và EventLog cùng transaction;
lỗi ghi/audit rollback toàn bộ. Chạy lại bỏ qua các dòng đã có màu.
GET danh sách MFG/tìm kiếm đang dùng snapshot này sẽ thấy màu sau khi dữ liệu được tải lại.
