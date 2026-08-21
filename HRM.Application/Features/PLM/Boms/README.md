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

## Trạng thái triển khai

Hiện tại mới có Domain entity, navigation collection, DbSet và EF Core configuration. Chưa có API, calculator, migration, backfill hoặc luồng tự động snapshot BOM vào lệnh sản xuất.
