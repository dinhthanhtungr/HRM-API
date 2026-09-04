# BOM master

Module này định nghĩa nền dữ liệu master cho E-BOM và M-BOM, tách biệt với công thức thực thi của từng lệnh sản xuất.

## Phân biệt dữ liệu

- `BomDefinition` và `BomVersion` là dữ liệu BOM master theo sản phẩm.
- E-BOM (`Engineering`) mô tả cấu trúc kỹ thuật.
- M-BOM (`Manufacturing`) có thể tham chiếu E-BOM nguồn, bổ sung công đoạn và định mức hao hụt.
- `ManufacturingFormula` vẫn là công thức thực tế được sử dụng trong sản xuất; `SourceBomVersionId` chỉ lưu nguồn M-BOM đã dùng để khởi tạo công thức.
- `ManufacturingFormulaVersion` tiếp tục là lịch sử chỉnh sửa công thức thực thi, không phải phiên bản BOM master.

## Cấu trúc chính

- `BomDefinition` có nhiều `BomVersion`.
- `BomVersion` có các item, công đoạn M-BOM và rule hao hụt.
- `SourceEngineeringBomVersionId` liên kết một M-BOM version với E-BOM version nguồn.
- `ProductStandardBomVersion` lưu khoảng thời gian một M-BOM version là BOM chuẩn của Product.
- `ManufacturingLossType` là danh mục mở rộng theo công ty; thêm loại hao hụt mới không yêu cầu thêm cột BOM.
- `MfgProductionOrderLoss` giữ snapshot hao hụt kế hoạch và thực tế của lệnh sản xuất.

Các điều kiện xuyên bảng như cùng `CompanyId`, cùng Product, loại BOM phù hợp và trạng thái `Released` phải được kiểm tra tại Application layer khi triển khai use case ghi.

## E-BOM API

API hiện chỉ mở phạm vi `Engineering`; chưa expose M-BOM, công đoạn, hao hụt hay integration lệnh sản xuất.

```http
GET   /api/v1/plm/boms?productId={productId}&keyword={keyword}
GET   /api/v1/plm/boms/versions/{bomVersionId}
POST  /api/v1/plm/boms
PUT   /api/v1/plm/boms/versions/{bomVersionId}
PATCH /api/v1/plm/boms/versions/{bomVersionId}
```

`POST` tạo `BomDefinition` loại `Engineering`, kèm version `1` ở trạng thái `Draft` và toàn bộ item. `PUT` chỉ áp dụng cho Draft và thay thế trọn danh sách item; backend đánh lại `lineNo` theo thứ tự mảng. `PATCH` chỉ sửa metadata Draft; field không gửi giữ nguyên, field nullable chỉ được xóa khi đưa đúng code vào `clearFields` (`effectiveFrom`, `effectiveTo`, `changeReason`, `note`).

Mọi endpoint lọc theo `CurrentUser.CompanyId`; thao tác ghi yêu cầu `EmployeeId`. Material/Product component phải active và cùng công ty với BOM. E-BOM chỉ nhận item `Material` hoặc `Product`, không cho Product tự tham chiếu chính nó.

Migration tạo schema/bảng nằm ở `HRM.Infrastructure/DatabaseContext/Migrations/20260902_CreateBomMaster.sql`. Database chưa có endpoint release/obsolete, standard-BOM assignment, BOM explosion hoặc snapshot vào production order; các phần đó được giữ cho phase sau.
