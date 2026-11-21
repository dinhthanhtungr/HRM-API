# Warehouse

## Tồn kho theo dõi dành cho IMS

`GET /api/v1/warehouse/stock-available/tracking` yêu cầu
`warehouse.stock.tracking.view` và nhân viên/công ty hiện tại. Trả `items` có mã,
tên, loại tồn, `onHandKg` và `availableKg`, tối đa 25 dòng mỗi trang. Tìm theo
mã hoặc tên bằng `keyword` tối đa 100 ký tự.

`onHandKg` là tổng trên kệ active cùng công ty, loại kho cân trộn, chỉ gồm TP
và NVL thường. `availableKg` của TP bằng tồn trừ giữ chỗ đang mở còn lại.
Trường này là `null` với NVL hoặc khi mã TP trùng mã vật tư vì bảng giữ chỗ
chưa tách loại tồn. Không trả lô, vị trí kệ, mã giữ chỗ, liên kết sản xuất,
công thức hoặc giá. Tồn kho đầy đủ, export và lịch sử biến động yêu cầu tài
khoản không mang hạn chế IMS. Token explicit cần role claim rồi refresh.

`reservedVaCodes[].mfgProductionOrderId` bổ sung ID MFG từ selection hiện hành
cùng company với `vaCode`, để FE mở chi tiết MFG ở tab mới. Thiếu selection/ID
hợp lệ thì trả null; không suy MFG từ VA. Quyền truy cập chi tiết MFG giữ nguyên.

Lệnh NVL nội bộ vẫn tạo/cập nhật giữ chỗ WarehouseTempStock khi tạo/sửa; riêng giới hạn
Max và kg từng lot lấy theo ShelfStock hợp lệ, không trừ reservation. Hủy lệnh giải phóng
Open của chính lệnh và xóa lịch trong transaction.
MFG thường vẫn dùng ProductionOrderReservationService như hiện tại.
Không trừ tồn thực, không tạo phiếu xuất; kg khả dụng/FIFO chỉ là dữ liệu tại thời điểm đọc.

## Lý do nhập TP lỗi và xuất tồn kho

`details[].defectReasons` trên list/detail voucher và
`stockDetailAvailables[].defectReasons` trên tồn kho là mảng
`{ reason, sourceKind, requestCode, voucherCode, recordedAt }`, mặc định `[]`.
Nguồn hiện xác minh được là `ImportRequestFlowLog.Note`, phần `REASON=` trong
dòng `[WIP_YIELD_CHECK]`, với flow đã APPLIED và chưa hủy. `sourceKind` là
`ImportConfirmation`: lý do xác nhận nhập TP lỗi, không phải kết luận QC.
Liên kết qua `ProductionOutputReceiptLedger` theo company/request/detail/voucher,
rồi VoucherDetailId, không suy đoán LineNo. Không trả log thô, supplier hoặc cost.
Tồn kho chỉ gắn lịch sử cho TP lỗi cùng company/mã/lot không rỗng; đây không phải
phân bổ lý do riêng cho lượng tồn còn lại. Không có liên kết tin cậy thì trả `[]`.

`GET /api/v1/warehouse/stock-available/export` trả XLSX gồm `Tong hop` và
`Chi tiet ke lot`, dùng toàn bộ bộ lọc stock-available và bỏ phân trang.
Tái sử dụng query tồn kho với company/ownership/mixing capability hiện hành;
kệ tắt vẫn xuất nhưng không cộng khả dụng. Giới hạn 100.000 dòng chi tiết,
vượt giới hạn báo lỗi yêu cầu thu hẹp bộ lọc, không cắt dữ liệu ngầm.
File có lý do và phiếu/yêu cầu nguồn; kg là số, chuỗi được ghi dạng text.
Excel áp dụng quyền cột giống màn tồn kho: chỉ PLPUUser/Developer có cột tồn kệ
đã tắt và trạng thái kệ (`ApplicationRoleSets.Warehouse.InactiveShelfFieldViewers`).
Role khác được bỏ hẳn hai cột khỏi workbook, không dùng hidden column. Không xuất
cột tổng tồn vật lý vì màn tồn kho không có cột này. Tổng tồn hoạt động/đang giữ/
khả dụng và chi tiết kệ/lot giữ nguyên scope của query; không cấp thêm quyền kho cân trộn.

Báo cáo [nhu cầu mua NVL](../Purchasing/MaterialRequirements/README.md) cân đối nhu cầu reservation
chưa dùng với tồn RawMaterial trên kệ active và lượng mua chưa nhập. API riêng thuộc Purchasing;
không thay đổi công thức `AvailableKg` của các endpoint Warehouse hiện có.

ACCUser có quyền đọc toàn bộ tồn kho và lịch sử voucher trong công ty như President, kể cả khi đồng thời mang role Sale. Quyền ghi kho giữ nguyên; company và entity/reference scope vẫn được kiểm tra.

Kế hoạch giao hàng trả `lines[].importedQuantity` là tổng kg tồn hiện tại của sản phẩm trên kệ active
cùng company, gồm FinishedGood và DefectiveFinishedGood, chưa trừ reservation. Tên field giữ để tương thích;
timeline vẫn dùng lịch sử nhập. Chi tiết contract: [Delivery Plan](../Dispatch/DeliveryOrders/DELIVERY-PLAN.md).
`lines[].stockBreakdown` bổ sung phân rã tổng này thành Hàng đạt/Hàng lỗi; xem cùng contract Delivery Plan.

## Mục đích

Lịch sử biến động tồn trên kệ có API riêng, xem [Lịch sử biến động tồn kho](MOVEMENT-HISTORY.md).
Nguồn là ledger đã ghi nhận, bao gồm chuyển kho cân trộn, kiểm kê và điều chỉnh; không union thêm voucher.

Mua hàng có API [tạo yêu cầu xuất hàng trả NCC](../Purchasing/PurchaseOrders/RETURN_REQUESTS.md)
loại ExportReturn=12, codeFromRequest là mã ĐĐH. Đây là tạo yêu cầu, không phải ghi nhận thực xuất/ledger.

Feature Warehouse phục vụ màn hình xem tồn kho khả dụng và kiểm tra lịch sử phiếu kho. Endpoint tồn kho lấy tổng tồn, lượng đang giữ chỗ, lượng khả dụng và detail theo kệ/lô/công ty; endpoint voucher trả header và toàn bộ dòng phiếu đã lưu.

## Phạm vi

Feature này chỉ đọc dữ liệu tồn kho, không ghi nhận nhập/xuất kho, không tạo reserve và không thay đổi trạng thái kệ. Dữ liệu tồn được lấy từ `WarehouseShelfStock`; dữ liệu giữ chỗ được lấy từ `WarehouseTempStock`.

Luồng [tạo MFG kèm công thức](../PLM/ProductionOrders/README.md) ghi `WarehouseTempStock`
qua `ProductionOrderReservationService` trong transaction của MFG. Đây là giữ chỗ tổng theo
mã MFG/vật tư, không kiểm tra đủ tồn, không thay đổi stock/ledger/kệ hoặc xuất kho.
Các API Warehouse ở tài liệu này vẫn chỉ đọc; công thức tính tồn khả dụng không thay đổi.

Lịch sử voucher cũng là read-only, không thay đổi phiếu, ledger hoặc request và không cần migration. List chỉ lấy voucher có `RequestId` để giữ nghiệp vụ legacy; API detail vẫn cho phép xem voucher không có request nếu phiếu thuộc company hiện tại.

## Luồng nghiệp vụ

1. Controller nhận query và gọi MediatR query `GetStockAvailableQuery`.
2. Handler kiểm tra `CurrentUser.CompanyId`; nếu không có company thì trả lỗi.
3. Query tồn kho thô bao gồm kệ active/inactive để xem, nhưng bắt buộc stock và kệ cùng company hiện tại; bỏ dòng không có kệ hoặc mã hàng.
4. Tồn kho được group theo `Code + StockType`; `TotalOnHandKg` chỉ cộng kệ active, `PhysicalOnHandKg` cộng cả hai trạng thái, `InactiveShelfOnHandKg` chỉ cộng kệ inactive.
5. Handler enrich tên hàng và category từ `Material` hoặc `Product` theo `StockType`.
6. Detail được group theo `Code + StockType + ShelfStockCode + ShelfIsActive + CompanyName + LotNo`.
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
- `stockTypes`: danh sách enum `StockType`, truyền bằng query parameter lặp (OR). Một giá trị vẫn tương thích URL cũ; bỏ parameter nghĩa là không lọc loại. Giá trị lặp không nhân bản kết quả, enum không hợp lệ trả HTTP 400. Không dùng cú pháp CSV `stockTypes=3,4`.
- `onlyAvailableLeZero`: nếu `true`, chỉ lấy item có `AvailableKg <= 0`.
- `availableMax`: nếu có giá trị, chỉ lấy item có `AvailableKg <= availableMax`.
- `pageNumber`, `pageSize`: phân trang sau khi tính nghiệp vụ; mặc định theo `PaginationQuery` là `1` và `15`.

Response:

- `OperationResult<PagedResult<StockAvailableDto>>`.
- Mỗi item gồm `ShelfStockId`, `Code`, `StockType`, `CodeName`, `CategoryName`, `TotalOnHandKg`, `ReservedOpenAllKg`, `AvailableKg`, `ReservedVaCodes`, `StockDetailAvailables`.

### Contract chọn Lot cho FE

```text
GET /api/v1/warehouse/stock-available?pageNumber=1&pageSize=20&keyword=NVL_NH_137&stockTypes=3&stockTypes=4
GET /api/v1/warehouse/stock-available?pageNumber=1&pageSize=20&keyword=MA_THANH_PHAM&stockTypes=1&stockTypes=2
GET /api/v1/warehouse/stock-available?pageNumber=1&pageSize=20&stockTypes=5
```

`1/2` là thành phẩm đạt/lỗi, `3/4` là NVL đạt/lỗi; `5` là loại khác, dùng riêng theo nhu cầu màn hình. Các giá trị legacy `6/7` vẫn hợp lệ. URL một loại như `stockTypes=3` giữ nguyên hành vi.

Phân trang trên tập kết quả chung sau filter loại và filter available. Mỗi item là một nhóm `Code + StockType`, không phải một Lot; `totalCount` đếm nhóm, `hasNextPage` theo số nhóm còn lại. Thứ tự nhóm là `Code ASC, StockType ASC`; detail theo mã kệ, Lot, tên công ty tăng dần. Thứ tự ổn định khi dữ liệu không đổi; offset pagination không cung cấp snapshot nếu tồn kho thay đổi giữa các request. `sortBy/sortDirection` không thay đổi thứ tự cố định của endpoint này.

Response rút gọn (bên trong `data.items`):

```json
[
  { "code": "NVL_NH_137", "stockType": 3, "totalOnHandKg": 12, "availableKg": 12,
    "stockDetailAvailables": [
      { "lotNo": "LOT-A", "shelfStockCode": "A.01", "companyName": "VietAus", "onHandKg": 7 },
      { "lotNo": "LOT-A", "shelfStockCode": "B.01", "companyName": "VietAus", "onHandKg": 5 }
    ] },
  { "code": "NVL_NH_137", "stockType": 4, "totalOnHandKg": 3, "availableKg": 0,
    "stockDetailAvailables": [
      { "lotNo": "LOT-A", "shelfStockCode": "A.01", "companyName": "VietAus", "onHandKg": 3 }
    ] }
]
```

Detail là tồn realtime cộng theo mã hàng + loại + mã kệ + trạng thái kệ + tên công ty + Lot. Cùng Lot khác loại/vị trí không gộp. FE flatten phải giữ `stockType` của nhóm cha; khóa option gồm mã hàng, loại, vị trí và Lot, không dùng riêng `lotNo` hoặc `shelfStockId` của nhóm cha (ID nhỏ nhất trong nhóm, chỉ đại diện nhóm, không phải quyền lấy hàng). `lotNo=null` là chưa có số Lot; kg bằng 0 là kết quả tồn hiện tại. Hàng lỗi luôn có `availableKg=0` theo nghiệp vụ hiện hành: dùng `stockDetailAvailables[].onHandKg` để hiển thị kg; quyền chọn hàng lỗi theo từng nghiệp vụ riêng. Response envelope giữ nguyên.

### Hiển thị tồn trên kệ đã tắt (FE)

Áp dụng mặc định cho `GET /api/v1/warehouse/stock-available`, không cần query mới hay migration.
Các field mới là dữ liệu realtime/tính toán, không phải snapshot hoặc số lượng đã xuất.

| Field JSON | Ý nghĩa |
| --- | --- |
| `totalOnHandKg` (giữ nguyên) | Tổng kg trên kệ **active**, chưa trừ giữ chỗ. |
| `physicalOnHandKg` (mới) | Tổng kg thực tế trên cả kệ active và inactive. Dùng cho cột “Tổng tồn thực tế”. |
| `inactiveShelfOnHandKg` (mới) | Tổng kg trên kệ đã tắt. Dùng cho cột “Tồn kệ đã tắt”. |
| `reservedOpenAllKg` (giữ nguyên) | Tổng reservation open còn dư theo mã hàng, không gắn từng kệ/lot. Không chia lại reservation theo trạng thái kệ. Hàng lỗi bằng 0. |
| `availableKg` (giữ nguyên) | Hàng không lỗi: `totalOnHandKg - reservedOpenAllKg`; có thể âm, không clamp. Hàng lỗi luôn bằng 0. Không cộng tồn kệ đã tắt. |
| `stockDetailAvailables[].shelfIsActive` (mới) | Trạng thái realtime từ `WarehouseShelves.IsActive`; `false` → hiển thị “Đã tắt”. |
| `stockDetailAvailables[].canUse` (mới) | `shelfIsActive && onHandKg > 0`. Chỉ phản ánh điều kiện kệ và lượng tồn; không cấp quyền, không kiểm tra reservation hoặc thay thế validation loại hàng của nghiệp vụ đích. |
| `stockDetailAvailables[].unavailableReason` (mới) | `ShelfInactive` ưu tiên khi kệ tắt; còn lại `NoOnHandStock` khi kg <= 0; `null` khi đạt điều kiện kệ và tồn. FE tự map nhãn. |

Các tổng ở cấp item là canonical theo `Code + StockType` trên tập dữ liệu sau keyword/visibility filter.
`physicalOnHandKg = totalOnHandKg + inactiveShelfOnHandKg`; các tổng là decimal không làm tròn thêm.
`onHandKg` ở cấp detail là lượng thực tế của nhóm kệ/lot, kể cả kệ tắt. Số 0 không đồng nghĩa thiếu dữ liệu.
Nhóm chỉ có kệ tắt vẫn xuất hiện với `totalOnHandKg = 0`; `availableKg` bằng 0 hoặc âm nếu còn giữ chỗ.
Không có tồn phù hợp thì `items=[]`; không có reservation thì `reservedVaCodes=[]`.

Ví dụ item hàng đạt có 70 kg kệ active, 30 kg kệ tắt, giữ chỗ 20 kg:

```json
{
  "code": "ITEM", "stockType": 3,
  "totalOnHandKg": 70, "physicalOnHandKg": 100, "inactiveShelfOnHandKg": 30,
  "reservedOpenAllKg": 20, "availableKg": 50,
  "stockDetailAvailables": [
    { "shelfStockCode": "A.01", "lotNo": "LOT-A", "companyName": "VietAus", "onHandKg": 70,
      "shelfIsActive": true, "canUse": true, "unavailableReason": null },
    { "shelfStockCode": "B.01", "lotNo": "LOT-A", "companyName": "VietAus", "onHandKg": 30,
      "shelfIsActive": false, "canUse": false, "unavailableReason": "ShelfInactive" }
  ]
}
```

FE hiển thị cả hai dòng; khóa chọn dòng `canUse=false`. Với hàng lỗi, `canUse=true` chỉ nghĩa là kệ
hoạt động còn tồn; vẫn áp dụng rule loại tồn của nghiệp vụ (ví dụ giao hàng lỗi được phép riêng).
Không dùng `physicalOnHandKg` hoặc tổng detail để quyết định lượng khả dụng.
Backend giữ helper `ActiveShelfStocks`/`ForItemStock` và các reader chọn tồn MFG/giao hàng chỉ lấy kệ active;
việc mở rộng hiển thị chỉ nằm trong query tổng quan này. Mutation phải xác thực lại nguồn tồn hiện tại,
không tin `canUse`, kg hoặc ID nhóm do FE gửi. Repo hiện không có mutation trừ shelf stock/ledger;
endpoint chuyển MFG sang dùng hàng tồn chỉ đổi trạng thái, không phải xác nhận xuất kho thực tế.

### Mã công thức trong danh sách giữ chỗ

Trong `stock-available`, `reservedVaCodes[].vaCode` trả trực tiếp `ManufacturingFormula.ExternalId`
của công thức đang chọn của MFG giữ chỗ (mã VA), thay cho mã MFG trước đây.
Không trả field `manufacturingFormulaExternalId` riêng. FE tiếp tục dùng `vaCode` trong tooltip “Phiếu đang giữ”.
`WarehouseTempStock.VaCode` trong database vẫn giữ mã nguồn (thường là `MfgProductionOrder.ExternalId`);
chỉ map lại response, không thay cách group/tính `reservedKg`, số phần tử hoặc tổng giữ chỗ.

Backend resolve theo batch: mã giữ chỗ → MFG active cùng company → `ProductionSelectVersion` cùng company,
`ValidTo == null` → `ManufacturingFormula` active cùng company → `ExternalId`.
Nếu có nhiều lựa chọn mở, ưu tiên `ValidFrom` mới nhất rồi PSV ID giảm dần như danh sách MFG;
`ValidFrom=null` vẫn hợp lệ và xếp sau ngày có giá trị. Đây là mã công thức **đang chọn realtime**,
không phải snapshot công thức tại `createdDate` của reservation. Không đọc/trả giá hay thành phần công thức.

Không có MFG/link/công thức hợp lệ thì `vaCode=""`; giữ nguyên reservation để không mất lượng đang giữ.
FE có thể hiện “Chưa có công thức” khi chuỗi rỗng. Không fallback sang mã MFG hoặc dùng mã VA làm ID MFG.
Nhiều MFG cùng dùng một VA vẫn giữ các phần tử riêng theo nguồn reservation, không gộp lại chỉ vì trùng `vaCode`.
`createdDate` vẫn là ngày tạo dòng giữ chỗ, không đổi thành ngày tạo/chọn công thức.
Hàng lỗi vẫn trả `reservedVaCodes=[]` như trước. Scope xem hàng và company giữ nguyên.

```json
{
  "reservedVaCodes": [
    { "vaCode": "VA261000123", "reservedKg": 60.375, "createdDate": "2026-10-17T09:43:00" },
    { "vaCode": "", "reservedKg": 5, "createdDate": "2026-10-16T08:00:00" }
  ]
}
```

### Lịch sử voucher

IMSUser xem các phiếu nhập mua, trả nhà cung cấp và xuất giao khách khi loại phiếu khớp yêu cầu kho và mã nguồn xác định duy nhất một đơn mua hoặc đơn giao cùng công ty. List, gợi ý, tổng số và chi tiết dùng cùng phạm vi. Phiếu liên kết với cấp phát sản xuất, nhập đầu ra sản xuất hoặc kho cân trộn bị loại. Luồng IMS bỏ tên yêu cầu nhập tự do, ghi chú, lý do lỗi chi tiết và metadata nhà cung cấp trên phiếu. Tìm kiếm IMS giới hạn ở mã chứng từ, mã yêu cầu/mã nguồn, mã/tên hàng và số lô. Lịch sử biến động và truy vết theo ledger vẫn yêu cầu tài khoản không mang IMS.

`GET /api/v1/warehouse/vouchers` nhận `voucherType`, `reqType`, `status`, `fromDate`, `toDate`, `keyword`, `pageNumber`, `pageSize`. Không nhận `companyId`: company luôn lấy từ token hiện tại. `status` vẫn là chuỗi legacy và lọc không phân biệt hoa thường; các mã enum cũ giữ để tương thích và lọc, còn nhãn Loại phiếu dùng `businessType` do BE xác định.

Kết quả có dạng `OperationResult<PagedResult<WarehouseVoucherListItemDto>>`; mỗi item gồm header (`voucherId`, `voucherCode`, `status`, `createdDate`), request (`requestId`, `requestCode`, `reqType`, `reqStatus`, `codeFromRequest`), company/người tạo và `details`. Thứ tự là `createdDate DESC`, rồi `voucherId DESC`. `keyword` tìm voucher/request, mã/tên hàng và lot trên dòng, hoặc mã sample request suy ra `Product.ColourCode`; ký tự `%` và `_` được xử lý literal, không trở thành wildcard.

`GET /api/v1/warehouse/vouchers/{voucherId}` trả `OperationResult<WarehouseVoucherDetailResponseDto>` với cùng header, `businessType`, thông tin NCC/đơn mua và `details` như list. Nếu không tồn tại, voucher khác company, id không hợp lệ hoặc người gọi không có quyền Warehouse thì trả `404` để không lộ sự tồn tại của phiếu.

Mỗi `details[]` đại diện một `WarehouseVoucherDetail` đã persist. `movementDate` là thời điểm mới nhất (`MAX(WarehouseShelfLedger.CreatedAt)`) theo `voucherDetailId`; `null` nghĩa là chưa có ledger liên kết. Danh sách rỗng nghĩa là phiếu không có dòng. Thông tin NCC/đơn mua được xác định chung cho cả list và detail theo contract bên dưới.

### Loại nghiệp vụ phiếu kho thống nhất

`businessType` là nguồn nhãn duy nhất cho cột Loại phiếu trên FE, gồm `code` ổn định,
`name` tiếng Việt, `nameEn` tiếng Anh và `tone` màu ngữ nghĩa. BE tính tại thời điểm đọc, không ghi lại loại phiếu
hoặc tạo migration. `voucherType`, `reqType`, `details[].voucherType` giữ giá trị cũ:
loại đầu phiếu, loại yêu cầu và loại dòng/kết quả QC là các khái niệm khác nhau.

Thứ tự xác định theo nguồn đã kiểm chứng cùng company:

1. Đầu phiếu nhập NVL/khác/vật tư hoặc xuất trả (`5/9/13/12`) có `codeFromRequest`
   khớp duy nhất một đơn mua: nhập mua NVL/vật tư/hàng mua hỗn hợp hoặc xuất trả NCC.
   Phân nhóm dựa vào các dòng mua active và `Material.Category.Types`; thiếu phân nhóm với
   mã 9 thì dùng Nhập hàng mua từ NCC, không tự gán NVL. Mã 5/13 giữ nghĩa NVL/vật tư khi thiếu category.
2. Xuất TP đạt/lỗi (`2/4`) liên kết phiếu giao: Xuất thành phẩm giao khách/TP lỗi giao khách.
3. Xuất khác (`10`) có marker có cấu trúc `MfgFormulaAdjustment:<guid>` và bản chỉnh màu
   thực sự tồn tại cùng company: Xuất bổ sung chỉnh màu. Tên phiếu hoặc tiền tố mã không đủ.
4. Nhập TP đạt/lỗi (`1/3`) có liên kết `ProductionOutputReceiptLedger` và nguồn sản xuất
   cùng company: Nhập thành phẩm sản xuất/TP lỗi sản xuất.
5. Có biến động kệ `CT.0.1` cùng company theo `VoucherId`, hoặc qua `VoucherDetailId` nếu
   thiếu header ID: các biến động khác 0 đều dương → Nhập kho cân trộn; đều âm → Xuất kho cân trộn.
   Nếu đồng thời có `OperationMaterialBuffer` cùng company thì nhãn xuất là Xuất từ cân trộn cho sản xuất.
   Cả dương lẫn âm không đủ để đoán một chiều, giữ loại gốc.
6. Xuất NVL/NVL lỗi/khác/vật tư có `OperationMaterialBuffer` cùng company: Xuất cấp sản xuất.
7. Chưa có nguồn đủ rõ: trả đủ 14 loại gốc theo `voucherType`; mã lạ trả Unknown kèm số mã,
   không bỏ phiếu vì chưa nhận diện. Quy tắc list có RequestId vẫn giữ nguyên.

Nguồn nghiệp vụ cụ thể ưu tiên hơn vị trí kệ. Không suy rằng mọi phiếu tại CT.0.1 đều là
cấp cân trộn hoặc mọi xuất khác đều là chỉnh màu. Không gộp bộ lọc hiện tại: `reqType`/
`voucherType` vẫn lọc theo mã lưu, keyword không tự tìm theo nhãn tính toán.

`purchaseOrderCode` là mã đơn mua đã xác minh, `null` nếu thiếu/không phù hợp/trùng nguồn.
`canViewSupplier` là quyết định BE: phải đăng nhập, đúng company, có role `PLPUUser` và capability
`warehouse.voucher-supplier.view`. Mapping mặc định chỉ PLPUUser; Developer, President, Admin,
ACC, Lab, kho hoặc Sale không được đọc NCC nếu không đồng thời có role PLPUUser, kể cả có claim.
Token legacy dùng mapping fallback; môi trường explicit permission cần cấp capability trên trong cấu hình role,
rồi login/refresh. Không sinh/chạy SQL hoặc đổi DB trong task này.
`supplierName`, `supplierExternalId` lấy từ snapshot NCC của đơn đó; `comments` lấy từ đơn mua.
Khi không được phép, cả ba field trả `null` và BE không query snapshot/ghi chú đơn mua.
PLPU được phép nhưng chưa có dữ liệu thì chuỗi rỗng. `purchaseOrderCode` và businessType giữ nguyên
cho người đã được xem phiếu; không được dùng mã nguồn bất kỳ làm mã đơn mua.
Không lấy supplier/customer/giá từ công ty khác. Các nguồn lịch sử không bắt active ở header
để không mất liên kết của phiếu đã phát sinh; không cấp thêm quyền hoặc trả field giá.

```json
{
  "voucherType": 9,
  "businessType": { "code": "PurchaseRawMaterialReceipt", "name": "Nhập NVL mua từ NCC", "nameEn": "Purchased raw material receipt", "tone": "success" },
  "purchaseOrderCode": "PO-001",
  "canViewSupplier": true,
  "supplierName": "Nhà cung cấp A",
  "supplierExternalId": "NCC-A",
  "comments": "",
  "details": []
}
```

List enrich theo batch trên trang, detail dùng cùng `IWarehouseVoucherBusinessReader`, không query
từng phiếu. Dữ liệu trả là mô tả chứng từ kho hiện hữu; yêu cầu đang Pending không được biến thành
một phiếu đã nhập/xuất chỉ vì có liên kết nguồn. FE không dùng businessType để quyết định quyền ghi kho.

`businessType.tone` dùng các code màu shared: success = nhập, warning = xuất/trả NCC,
primary = cân trộn/chỉnh màu, danger = loại hàng lỗi, muted = chưa rõ. Đây là màu phân loại,
không đại diện trạng thái duyệt hoặc kết luận kiểm tra của toàn phiếu. FE giữ nhãn chữ và tự xuống dòng.

## Dữ liệu

- `WarehouseShelfStock`: tồn kho thực tế theo kệ/lô/loại kho.
- `WarehouseShelves`: dùng `IsActive` để tách tồn kệ hoạt động/kệ tắt và điều kiện sử dụng.
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

Controller có `[Authorize]`. Handler bắt buộc lọc dữ liệu theo `CurrentUser.CompanyId` cho tồn kho, kệ, reserved, material, product và sample request để tránh lộ tồn kho giữa các công ty. Riêng tổng quan tồn kho cho xem kệ inactive trong detail/tổng thực tế; không cộng vào tổng active hoặc available. Customer visibility áp dụng cho cả hai trạng thái kệ.

Sale (`SaleUser`/`SaleAdmin`) chỉ xem tồn của Product thuộc Sample Request/Formula trong customer scope của mình; NVL trong Formula của các Product đó cũng thuộc phạm vi. `KH_VIETAUS` luôn được đưa vào phạm vi của Sale, không phụ thuộc `keyword`. Lọc được thực hiện ngay trong Application query trước khi group tồn, không lọc ở FE. Admin, Developer, President, KHOUser và CustomerViewAll vẫn xem toàn bộ tồn trong company.

Rule này nằm ở `IWarehouseStockVisibilityService` và được dùng cho cả `stock-available` và Material Preview, để không phát sinh một cách lọc riêng theo từng màn hình.

Voucher history cho phép thêm Sale đọc nhưng Sale chỉ thấy phiếu có dòng Product/NVL trong customer scope của mình; Sale mở phiếu detail cũng bị kiểm tra lại, không chỉ dựa vào list. `KH_VIETAUS` luôn được xem, còn `keyword` chỉ phục vụ tìm kiếm. Warehouse/Admin/Developer/President vẫn đọc toàn bộ. Cả list và detail đều filter `WarehouseVoucher.CompanyId == CurrentUser.CompanyId`; detail dùng đồng thời `VoucherId + CompanyId` nên không bị IDOR. Request, employee, company và Purchase Order enrich cũng bị giới hạn cùng company.

`LabUser` được đọc toàn bộ lịch sử phiếu kho trong company hiện tại qua cả
`GET /api/v1/warehouse/vouchers` và `GET /api/v1/warehouse/vouchers/{voucherId}`,
kể cả khi đồng thời mang role Sale. Gate đọc dùng `WarehouseVoucherAccessRules` và role set Lab tập trung;
không thêm capability/role claim mới hoặc cấp quyền ghi kho. LAB phải đăng nhập và có company hợp lệ;
phiếu khác company vẫn không xuất hiện trong list và detail trả 404. LAB không được đọc NCC;
field supplier tuân theo `canViewSupplier` ở contract phía trên. Không bổ sung field giá hoặc dữ liệu
công thức. Phạm vi tồn kho của API khác không đổi.

## Quy tắc nghiệp vụ

Kết quả [IQC](../DevAndQA/IQC/README.md) cập nhật `WarehouseVoucherDetail.VoucherType` theo quyết định Waiter/Pass/Fail/Special trong transaction cùng QC và PO. IQC dùng company scope và capability riêng; các API Warehouse ở đây vẫn read-only. Đồng bộ WarehouseShelfStock của nguồn IQC đang tắt và được giữ tắt, nên quyết định QC không tự chuyển loại stock/kệ hoặc tạo ledger.

- Kệ inactive hiện trong detail và tổng thực tế của tổng quan tồn; không cộng vào `TotalOnHandKg`/available và không được chọn sử dụng.
- `CT.0.1` là kho cân trộn; trong `stock-available` chỉ người có capability
  `warehouse.mixing-stock.view` được xem trong detail và các tổng. Mapping mặc định là `PLPUUser`,
  `Developer`, `President`; các role khác tiếp tục bị loại tồn cân trộn trước group/filter/phân trang.
  Kiểm tra cả `ShelfStockCode` và `WarehouseShelves.SlotCode`. Company/customer scope vẫn áp dụng;
  quyền này không cấp quyền lấy hàng/xuất kho và không đổi Material Preview/customer-product-stock.
  Kệ cân trộn inactive được xem như kệ tắt khác, không cộng `totalOnHandKg`/`availableKg`.
  Khi trả detail mã CT.0.1 đổi nhãn thành `CT.0.1 - KHO CÂN TRỘN`.

Capability được khai báo trong `ApplicationPermissions.Warehouse`, role fallback tại
`ApplicationRoleSets.Warehouse.MixingStockViewers` và đăng ký trong `ApplicationPermissionCatalog`.
Baseline seed/rollback role claims đã bổ sung mapping; không có migration schema, không tự chạy SQL.
Môi trường dùng permission DB cần đồng bộ claim `warehouse.mixing-stock.view` cho ba role trên qua
cấu hình role hoặc [script cấp quyền riêng](seed-mixing-stock-permission.sql), rồi login/refresh token.
Script không đổi schema hoặc cấp các permission khác. Token explicit chưa có claim vẫn bị ẩn;
token legacy không có explicit permission set dùng mapping fallback. Response DTO không thêm field mới.
- Reserved chỉ tính dòng open còn dư: `QtyRequest - QtyUsed > 0`.
- Hàng lỗi không trừ reserved và luôn trả available bằng `0`.
- Không phân trang trực tiếp ở database vì dữ liệu còn phải enrich tên hàng, detail, reserved và filter theo available.
- Sale không được trả toàn bộ stock rồi mới lọc ở client; mọi màn hình mới hiển thị tồn phải dùng chung rule customer scope này.

## Kiểm thử

- Kệ active có tồn phải xuất hiện trong tổng và detail.
- Kệ inactive có tồn xuất hiện trong detail/tổng thực tế, không cộng tổng active/available; nhóm chỉ có kệ inactive vẫn hiển thị. Reader chọn tồn nghiệp vụ vẫn loại kệ inactive.
- Reserved open còn dư phải trừ khỏi available của hàng không lỗi.
- Reserved consumed/cancelled hoặc đã dùng hết không được tính.
- Hàng lỗi luôn có reserved và available bằng `0`.
- Filter `onlyAvailableLeZero` và `availableMax` phải chạy sau khi tính available.
- Voucher list chỉ chứa phiếu có `RequestId`; filter ngày dùng `fromDate` inclusive và hết ngày `toDate` inclusive.
- Voucher list tải page header trước, sau đó batch tải details và ledger; không có N+1 theo voucher/detail.
- Regression phải kiểm tra Sale không thấy Product ngoài phạm vi và luôn thấy dữ liệu `KH_VIETAUS` cả khi không truyền `keyword`.

## Yêu cầu xuất kho từ DeliveryOrder

[DeliveryOrders](../Dispatch/DeliveryOrders/LEGACY-CONTRACT.md) tạo PRQ Pending loại ExportFinishedGood cùng transaction với PGH; group dòng theo product/lot/ItemStockType; mỗi dòng PRQ lưu một lot. FinishedGood và DefectiveFinishedGood được tách riêng. PGH vẫn lưu snapshot lot/tổng lượng không phân biệt loại tồn. PUT phiếu chưa in sync PRQ còn Pending; cancel/DELETE hủy PRQ và dòng liên quan theo company + codeFromRequest. Không reserve/consume, sửa shelf stock, temp stock hay ledger. Các API Warehouse tại đây vẫn chỉ đọc.

Lookup giao hàng tính khả dụng Hàng lỗi theo tồn shelf active và không trừ WarehouseTempStock (reserve hiện chưa có stock type). Đây là semantics riêng của Delivery Orders; API tồn kho tổng hợp Warehouse giữ contract hiện hành. Xem [phân bổ giao hàng](../Dispatch/DeliveryOrders/STOCK-ALLOCATIONS.md).

PGH nhận số bao từng lot và ghi BagNumber đúng theo ItemStockType trên PRQ. PATCH số bao sync PRQ Pending khi phân bổ rõ ràng; không đổi kg/loại tồn và không ghi shelf/ledger. Xem STOCK-ALLOCATIONS của Delivery Orders.

## Tiến độ nhập theo ĐĐH cho Mua hàng

GET `/api/v1/purchasing/purchase-orders/{purchaseOrderId}/receipt-progress` cung cấp projection chỉ đọc
các request/phiếu/QC/ledger gắn nguồn ĐĐH, kiểm tra quyền mua hàng và company của parent trước khi đọc kho.
Không cấp quyền ghi kho hoặc quyền mở toàn bộ màn Warehouse. Không thay API tồn/voucher hiện hành;
[contract và quy tắc net ledger chống cộng trùng](../Purchasing/PurchaseOrders/RECEIPT_PROGRESS.md).
# Tổng hợp nhu cầu NVL từ PLM

Màn nhu cầu/chuyển đổi NVL dùng GetStockAvailableQuery theo batch ExactCodes, giữ company/shelf visibility và không trả giá.
Nguồn nhu cầu là giữ chỗ Open chưa dùng của MFG active; khi tính thiếu cộng lại đúng phần nhu cầu đang thống kê trước
khi so với tồn, tránh trừ giữ chỗ hai lần. Hàng lỗi tách riêng và cần capability hiện hành; endpoint này chỉ đọc, không xuất/giữ chỗ thêm.



## Tìm kiếm gợi ý cho trang danh sách

Tồn kho: mã/tên NVL, mã/tên sản phẩm, ColorCode, lô. Phiếu kho: mã chứng từ, mã/tên hàng, lô. Biến động: mã hàng, lô, mã tồn kho, mã yêu cầu; badge lọc cả nhóm biến động.

Contract tùy chọn `searchSelections`/`suggestionKeyword`, OR/AND badge, giới hạn 10 và hướng dẫn thêm field: [Searching](../../Commons/Searching/README.md). Không mở rộng quyền hoặc áp dụng tự động cho lookup.
