# Tạo công thức

`POST /api/v1/plm/formulas` bắt buộc có `productId` hợp lệ.

Sản phẩm phải active, đúng company và đã lưu cả `Product.ColourCode` (mã màu) lẫn
`Product.Name` (tên sản phẩm). Null/rỗng/chỉ khoảng trắng bị từ chối trước khi cấp mã VU,
thêm Formula/material hoặc snapshot. `request.name` là tên **công thức**, không thay thế tên sản phẩm.
Áp dụng cả tạo từ dữ liệu copy. Không tự cập nhật/xóa công thức đã có.

`name` là optional khi tạo mới:

- Nếu FE gửi `name`, backend trim và validate tối đa 200 ký tự.
- Nếu FE bỏ trống `name`, backend tự sinh theo các công thức active cùng `companyId` và `productId`.
- Tên tự sinh có dạng `F001`, `F002`, `F003`, ... Backend lấy số lớn nhất trong các tên đúng dạng `F` + số rồi cộng 1.

Ví dụ product hiện có công thức `F001`, lần tạo tiếp theo không gửi `name` sẽ sinh `F002`.

## Quyền lưu công thức

Các API lưu thông tin công thức dùng policy `PLM.Formula.Manage`:

```http
POST   /api/v1/plm/formulas
PUT    /api/v1/plm/formulas/{formulaId}
PATCH  /api/v1/plm/formulas/{formulaId}/status
DELETE /api/v1/plm/formulas/{formulaId}
```

Policy này cho phép `Admin`, `Developer`, `President`, `LabUser` và `LabAdmin`.

API chỉnh pricing vẫn dùng policy riêng `PLM.FormulaPricing.Update`:

```http
PATCH /api/v1/plm/formulas/{formulaId}/pricing
```

Policy pricing chỉ dành cho nhóm được phép chỉnh giá, không tự mở theo quyền Lab.
