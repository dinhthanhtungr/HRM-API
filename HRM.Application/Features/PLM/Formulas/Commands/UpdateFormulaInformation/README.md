# Cập nhật thông tin công thức

`PUT /api/v1/plm/formulas/{formulaId}` chỉ cập nhật phần thông tin công thức và danh sách material, không cập nhật giá sản xuất hoặc giá bán tiêu chuẩn.

`productId` là optional cho PUT:

- Nếu FE bỏ `productId`, gửi `null` hoặc gửi chuỗi rỗng, backend giữ `Formula.ProductId` hiện tại.
- Nếu FE gửi `productId` hợp lệ, backend validate Product active/cùng company rồi mới đổi.

`POST /api/v1/plm/formulas` vẫn bắt buộc có `productId` hợp lệ khi tạo công thức mới.
