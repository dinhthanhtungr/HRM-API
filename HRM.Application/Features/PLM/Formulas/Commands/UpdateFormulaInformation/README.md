# Cập nhật thông tin công thức

`PUT /api/v1/plm/formulas/{formulaId}` chỉ cập nhật phần thông tin công thức và danh sách material, không cập nhật giá sản xuất hoặc giá bán tiêu chuẩn.

`productId` là optional cho PUT:

- Nếu FE bỏ `productId`, gửi `null` hoặc gửi chuỗi rỗng, backend giữ `Formula.ProductId` hiện tại.
- Nếu FE gửi `productId` hợp lệ, backend validate Product active/cùng company rồi mới đổi.

`POST /api/v1/plm/formulas` vẫn bắt buộc có `productId` hợp lệ khi tạo công thức mới.

## Duyệt lại giá khi sửa thành phần

Nếu Formula Approved đang là nguồn giá chuẩn VND Approved hiện hành và NVL/định lượng thực sự đổi,
BE cập nhật CheckBy/CheckDate, ghi LabFormulaConfirmed cùng job workflow outbox trước SaveSnapshotAsync.
Công thức, phiên bản, sự kiện và job được lưu cùng SaveChanges; worker dùng luồng PriceQuoteRequest
hiện có để gửi President yêu cầu duyệt lại đúng công thức, có action mở màn hình định giá.
Không gửi thêm thông báo trực tiếp sau lưu. Status-transition vừa xác nhận vừa đổi thành phần chỉ
ghi một sự kiện/job xác nhận, không gửi hai yêu cầu trùng. Chỉ đổi header, thứ tự dòng hoặc gửi lại
thành phần không đổi không kích hoạt nhánh duyệt lại do NVL. Điều kiện NeedsPricing vẫn dựa trên
CheckDate và các nguyên nhân định giá hiện có, không phụ thuộc việc đã đọc thông báo.
