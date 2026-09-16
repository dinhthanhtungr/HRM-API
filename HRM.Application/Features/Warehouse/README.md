# Warehouse

## Mục đích

Feature Warehouse phục vụ màn hình xem tồn kho khả dụng và kiểm tra lịch sử phiếu kho. Endpoint tồn kho lấy tổng tồn, lượng đang giữ chỗ, lượng khả dụng và detail theo kệ/lô/công ty; endpoint voucher trả header và toàn bộ dòng phiếu đã lưu.

## Phạm vi

Feature này chỉ đọc dữ liệu tồn kho, không ghi nhận nhập/xuất kho, không tạo reserve và không thay đổi trạng thái kệ. Dữ liệu tồn được lấy từ `WarehouseShelfStock`; dữ liệu giữ chỗ được lấy từ `WarehouseTempStock`.

Lịch sử voucher cũng là read-only, không thay đổi phiếu, ledger hoặc request và không cần migration. List chỉ lấy voucher có `RequestId` để giữ nghiệp vụ legacy; API detail vẫn cho phép xem voucher không có request nếu phiếu thuộc company hiện tại.

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
- `GET /api/v1/warehouse/vouchers`
- `GET /api/v1/warehouse/vouchers/{voucherId}`

Query:

- `keyword`: tìm theo mã tồn kho, số lot (`LotNo`, `LotKey`), tên nguyên vật liệu, tên thành phẩm hoặc mã sample request thông qua màu sản phẩm.
- `stockTypes`: lọc theo enum `StockType`.
- `onlyAvailableLeZero`: nếu `true`, chỉ lấy item có `AvailableKg <= 0`.
- `availableMax`: nếu có giá trị, chỉ lấy item có `AvailableKg <= availableMax`.
- `pageNumber`, `pageSize`: phân trang sau khi tính nghiệp vụ; mặc định theo `PaginationQuery` là `1` và `15`.

Response:

- `OperationResult<PagedResult<StockAvailableDto>>`.
- Mỗi item gồm `ShelfStockId`, `Code`, `StockType`, `CodeName`, `CategoryName`, `TotalOnHandKg`, `ReservedOpenAllKg`, `AvailableKg`, `ReservedVaCodes`, `StockDetailAvailables`.

### Lịch sử voucher

`GET /api/v1/warehouse/vouchers` nhận `voucherType`, `reqType`, `status`, `fromDate`, `toDate`, `keyword`, `pageNumber`, `pageSize`. Không nhận `companyId`: company luôn lấy từ token hiện tại. `status` vẫn là chuỗi legacy và lọc không phân biệt hoa thường; `voucherType`, `reqType`, `reqStatus` và `detail.voucherType` là code enum ổn định để FE tự map nhãn.

Kết quả có dạng `OperationResult<PagedResult<WarehouseVoucherListItemDto>>`; mỗi item gồm header (`voucherId`, `voucherCode`, `status`, `createdDate`), request (`requestId`, `requestCode`, `reqType`, `reqStatus`, `codeFromRequest`), company/người tạo và `details`. Thứ tự là `createdDate DESC`, rồi `voucherId DESC`. `keyword` tìm voucher/request, mã/tên hàng và lot trên dòng, hoặc mã sample request suy ra `Product.ColourCode`; ký tự `%` và `_` được xử lý literal, không trở thành wildcard.

`GET /api/v1/warehouse/vouchers/{voucherId}` trả `OperationResult<WarehouseVoucherDetailResponseDto>` với cùng header và `details`, nhưng không bổ sung supplier/comment ImportOther như legacy list. Nếu không tồn tại, voucher khác company, id không hợp lệ hoặc người gọi không có quyền Warehouse thì trả `404` để không lộ sự tồn tại của phiếu.

Mỗi `details[]` đại diện một `WarehouseVoucherDetail` đã persist. `movementDate` là thời điểm mới nhất (`MAX(WarehouseShelfLedger.CreatedAt)`) theo `voucherDetailId`; `null` nghĩa là chưa có ledger liên kết. Danh sách rỗng nghĩa là phiếu không có dòng. Với request `ImportOther`, list mới điền `supplierName`, `supplierExternalId`, `comments` từ Purchase Order/Snapshot theo `codeFromRequest`; chuỗi rỗng nghĩa là không tìm thấy snapshot phù hợp hoặc voucher không thuộc nhánh đó.

## Dữ liệu

- `WarehouseShelfStock`: tồn kho thực tế theo kệ/lô/loại kho.
- `WarehouseShelves`: dùng `IsActive` để quyết định kệ còn được tính vào tồn hay không.
- `Material`: map tên nguyên vật liệu khi `StockType` là `RawMaterial` hoặc `DefectiveRawMaterial`.
- `Product`: map tên thành phẩm khi `StockType` là `FinishedGood` hoặc `DefectiveFinishedGood`.
- `WarehouseTempStock`: tính lượng đang giữ chỗ/reserved.
- `WarehouseVouchers`, `WarehouseVoucherDetails`: header và dòng lịch sử voucher.
- `WarehouseShelfLedgers`: nguồn canonical của `movementDate` từng dòng.
- `WarehouseRequests`: request liên kết và loại/trạng thái request.
- `PurchaseOrders` + `PurchaseOrderSnapshots`: chỉ dùng enrich supplier/comment cho list `ImportOther`.

## Truy vết tồn thành phẩm theo khách hàng

SaleOrder cung cấp endpoint đọc `GET /api/v1/plm/sale-orders/customer-product-stock`. Endpoint này vẫn dùng nguồn tồn kho chuẩn của Warehouse nhưng bổ sung phép quy thuộc lịch sử:

1. `WarehouseShelfStock.Code` phải khớp `Product.ColourCode` và `StockType=FinishedGood`.
2. Mã lot ưu tiên `WarehouseShelfStock.LotNo`, fallback sang `LotKey` khi `LotNo` trống.
3. Mã lot khớp `ManufacturingFormula.ExternalId`.
4. `ProductionSelectVersion` nối công thức sản xuất thực tế với `MfgProductionOrder`.
5. MFG cung cấp `CustomerId` và `ProductId` để xác định tồn có thể quy thuộc cho khách hàng đang chọn.

Nếu cùng một Manufacturing Formula từng được dùng cho MFG của nhiều khách hàng, tồn của lot đó được đánh dấu mơ hồ và không được cộng vào tổng tồn của riêng khách nào. Rule này tránh tính trùng tồn kho; không có thao tác ghi dữ liệu và không cần migration.

Riêng `customer-product-stock` loại kệ cân trộn `CT.0.1` khỏi tồn thực tế và detail trả về, vì tồn ở khu vực này chưa là tồn thành phẩm sẵn sàng quy thuộc/giao khách. Rule này không đổi API tồn kho Warehouse tổng quát.

## Phân quyền và bảo mật

Controller có `[Authorize]`. Handler bắt buộc lọc dữ liệu theo `CurrentUser.CompanyId` cho tồn kho, reserved, material, product và sample request để tránh lộ tồn kho giữa các công ty. Stock nằm trên kệ inactive bị loại trước bước group, nên không thể xuất hiện trong tổng tồn, detail hoặc available.

Sale (`SaleUser`/`SaleAdmin`) chỉ xem tồn của Product thuộc Sample Request/Formula trong customer scope của mình; NVL trong Formula của các Product đó cũng thuộc phạm vi. `KH_VIETAUS` luôn được đưa vào phạm vi của Sale, không phụ thuộc `keyword`. Lọc được thực hiện ngay trong Application query trước khi group tồn, không lọc ở FE. Admin, Developer, President, KHOUser và CustomerViewAll vẫn xem toàn bộ tồn trong company.

Rule này nằm ở `IWarehouseStockVisibilityService` và được dùng cho cả `stock-available` và Material Preview, để không phát sinh một cách lọc riêng theo từng màn hình.

Voucher history cho phép thêm Sale đọc nhưng Sale chỉ thấy phiếu có dòng Product/NVL trong customer scope của mình; Sale mở phiếu detail cũng bị kiểm tra lại, không chỉ dựa vào list. `KH_VIETAUS` luôn được xem, còn `keyword` chỉ phục vụ tìm kiếm. Warehouse/Admin/Developer/President vẫn đọc toàn bộ. Cả list và detail đều filter `WarehouseVoucher.CompanyId == CurrentUser.CompanyId`; detail dùng đồng thời `VoucherId + CompanyId` nên không bị IDOR. Request, employee, company và Purchase Order enrich cũng bị giới hạn cùng company.

## Quy tắc nghiệp vụ

- Kệ inactive không được tính tồn, không hiện detail và không ảnh hưởng available.
- `CT.0.1` là kho cân trộn; khi trả detail đổi nhãn hiển thị thành `CT.0.1 - KHO CÂN TRỘN`.
- Reserved chỉ tính dòng open còn dư: `QtyRequest - QtyUsed > 0`.
- Hàng lỗi không trừ reserved và luôn trả available bằng `0`.
- Không phân trang trực tiếp ở database vì dữ liệu còn phải enrich tên hàng, detail, reserved và filter theo available.
- Sale không được trả toàn bộ stock rồi mới lọc ở client; mọi màn hình mới hiển thị tồn phải dùng chung rule customer scope này.

## Kiểm thử

- Kệ active có tồn phải xuất hiện trong tổng và detail.
- Kệ inactive có tồn không được xuất hiện trong tổng, detail hoặc kết quả export/list.
- Reserved open còn dư phải trừ khỏi available của hàng không lỗi.
- Reserved consumed/cancelled hoặc đã dùng hết không được tính.
- Hàng lỗi luôn có reserved và available bằng `0`.
- Filter `onlyAvailableLeZero` và `availableMax` phải chạy sau khi tính available.
- Voucher list chỉ chứa phiếu có `RequestId`; filter ngày dùng `fromDate` inclusive và hết ngày `toDate` inclusive.
- Voucher list tải page header trước, sau đó batch tải details và ledger; không có N+1 theo voucher/detail.
- Regression phải kiểm tra Sale không thấy Product ngoài phạm vi và luôn thấy dữ liệu `KH_VIETAUS` cả khi không truyền `keyword`.
