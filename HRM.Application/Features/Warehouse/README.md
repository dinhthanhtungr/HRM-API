# Warehouse

## Mục đích

Feature Warehouse phục vụ màn hình xem tồn kho khả dụng. Endpoint chính hiện tại là API lấy danh sách tồn kho khả dụng theo mã hàng và loại kho để FE hiển thị tổng tồn, lượng đang giữ chỗ, lượng khả dụng và detail theo kệ/lô/công ty.

## Phạm vi

Feature này chỉ đọc dữ liệu tồn kho, không ghi nhận nhập/xuất kho, không tạo reserve và không thay đổi trạng thái kệ. Dữ liệu tồn được lấy từ `WarehouseShelfStock`; dữ liệu giữ chỗ được lấy từ `WarehouseTempStock`.

## Luồng nghiệp vụ

1. Controller nhận query và gọi MediatR query `GetStockAvailableQuery`.
2. Handler kiểm tra `CurrentUser.CompanyId`; nếu không có company thì trả lỗi.
3. Query tồn kho thô được build bằng `BuildShelfStockQuery`, luôn lọc `WarehouseShelves != null` và `WarehouseShelves.IsActive == true` trước khi group.
4. Tồn kho được group theo `Code + StockType` để tính `TotalOnHandKg`.
5. Handler enrich tên hàng và category từ `Material` hoặc `Product` theo `StockType`.
6. Detail được group theo `Code + StockType + ShelfStockCode + CompanyName + LotNo`.
7. Reserved open được tính từ `WarehouseTempStock` với `ReserveStatus == Open` và `QtyRequest - QtyUsed > 0`.
8. Hàng lỗi (`DefectiveRawMaterial`, `DefectiveFinishedGood`) luôn có `ReservedOpenAllKg = 0`, `AvailableKg = 0` và không trả `ReservedVaCodes`.
9. Filter `OnlyAvailableLeZero` và `AvailableMax` chạy sau khi đã tính `AvailableKg`.
10. Handler phân trang in-memory sau toàn bộ enrich/filter nghiệp vụ.

## API

- `GET /api/v1/warehouse/stock-available`

Query:

- `keyword`: tìm theo mã tồn kho, số lot (`LotNo`, `LotKey`), tên nguyên vật liệu, tên thành phẩm hoặc mã sample request thông qua màu sản phẩm.
- `stockTypes`: lọc theo enum `StockType`.
- `onlyAvailableLeZero`: nếu `true`, chỉ lấy item có `AvailableKg <= 0`.
- `availableMax`: nếu có giá trị, chỉ lấy item có `AvailableKg <= availableMax`.
- `pageNumber`, `pageSize`: phân trang sau khi tính nghiệp vụ; mặc định theo `PaginationQuery` là `1` và `15`.

Response:

- `OperationResult<PagedResult<StockAvailableDto>>`.
- Mỗi item gồm `ShelfStockId`, `Code`, `StockType`, `CodeName`, `CategoryName`, `TotalOnHandKg`, `ReservedOpenAllKg`, `AvailableKg`, `ReservedVaCodes`, `StockDetailAvailables`.

## Dữ liệu

- `WarehouseShelfStock`: tồn kho thực tế theo kệ/lô/loại kho.
- `WarehouseShelves`: dùng `IsActive` để quyết định kệ còn được tính vào tồn hay không.
- `Material`: map tên nguyên vật liệu khi `StockType` là `RawMaterial` hoặc `DefectiveRawMaterial`.
- `Product`: map tên thành phẩm khi `StockType` là `FinishedGood` hoặc `DefectiveFinishedGood`.
- `WarehouseTempStock`: tính lượng đang giữ chỗ/reserved.

## Truy vết tồn thành phẩm theo khách hàng

SaleOrder cung cấp endpoint đọc `GET /api/v1/plm/sale-orders/customer-product-stock`. Endpoint này vẫn dùng nguồn tồn kho chuẩn của Warehouse nhưng bổ sung phép quy thuộc lịch sử:

1. `WarehouseShelfStock.Code` phải khớp `Product.ColourCode` và `StockType=FinishedGood`.
2. Mã lot ưu tiên `WarehouseShelfStock.LotNo`, fallback sang `LotKey` khi `LotNo` trống.
3. Mã lot khớp `ManufacturingFormula.ExternalId`.
4. `ProductionSelectVersion` nối công thức sản xuất thực tế với `MfgProductionOrder`.
5. MFG cung cấp `CustomerId` và `ProductId` để xác định tồn có thể quy thuộc cho khách hàng đang chọn.

Nếu cùng một Manufacturing Formula từng được dùng cho MFG của nhiều khách hàng, tồn của lot đó được đánh dấu mơ hồ và không được cộng vào tổng tồn của riêng khách nào. Rule này tránh tính trùng tồn kho; không có thao tác ghi dữ liệu và không cần migration.

## Phân quyền và bảo mật

Controller có `[Authorize]`. Handler bắt buộc lọc dữ liệu theo `CurrentUser.CompanyId` cho tồn kho, reserved, material, product và sample request để tránh lộ tồn kho giữa các công ty. Stock nằm trên kệ inactive bị loại trước bước group, nên không thể xuất hiện trong tổng tồn, detail hoặc available.

## Quy tắc nghiệp vụ

- Kệ inactive không được tính tồn, không hiện detail và không ảnh hưởng available.
- `CT.0.1` là kho cân trộn; khi trả detail đổi nhãn hiển thị thành `CT.0.1 - KHO CÂN TRỘN`.
- Reserved chỉ tính dòng open còn dư: `QtyRequest - QtyUsed > 0`.
- Hàng lỗi không trừ reserved và luôn trả available bằng `0`.
- Không phân trang trực tiếp ở database vì dữ liệu còn phải enrich tên hàng, detail, reserved và filter theo available.

## Kiểm thử

- Kệ active có tồn phải xuất hiện trong tổng và detail.
- Kệ inactive có tồn không được xuất hiện trong tổng, detail hoặc kết quả export/list.
- Reserved open còn dư phải trừ khỏi available của hàng không lỗi.
- Reserved consumed/cancelled hoặc đã dùng hết không được tính.
- Hàng lỗi luôn có reserved và available bằng `0`.
- Filter `onlyAvailableLeZero` và `availableMax` phải chạy sau khi tính available.
