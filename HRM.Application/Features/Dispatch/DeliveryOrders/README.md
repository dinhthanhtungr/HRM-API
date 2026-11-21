# Delivery Orders

- [Lịch giao nhiều đợt, phân bổ một dòng phiếu vào nhiều đợt và cờ tương thích](../../PLM/SaleOrders/DeliverySchedules/README.md). Mặc định tắt; không tạo migration hoặc thay DB. Source model được người dùng đồng bộ tại F:/Backend.

- [Dashboard báo cáo giao hàng hoàn tất: quyền cá nhân/toàn công ty, nguồn Excel và contract](FINISH-DASHBOARD.md).

## Bắt đầu đọc module

### Kế hoạch giao hàng trên trang Đơn hàng

`GET /api/v1/dispatch/delivery-plan` giữ nguyên request/response và quyền đọc hiện có.
Sale/Leader không có role đọc toàn công ty của điều vận phải đi qua
`ICustomerVisibilityService.BuildReadScopeAsync` và `ApplyMerchandiseOrderVisibility`:
chỉ đọc đơn thuộc khách hàng được phân công/claim/nhóm lãnh đạo theo quy tắc CRM.
Quyền CustomerViewAll hiện có vẫn được service chung áp dụng. Thiếu employee hoặc scope lệch company/employee bị từ chối.
Lọc này chạy trước Count/Skip/Take; chỉ tải dòng, tồn và phiếu cho các ID đơn đã qua scope.
Các bộ lọc keyword/customer/includeAllOrders không mở rộng quyền. Trang Sale cho tạm dừng/tiếp tục và xác nhận giao xong,
không có tạo/sửa phiếu giao hoặc xuất Excel. Không thêm capability/role claim; chỉ mở riêng thao tác xác nhận giao xong cho SaleUser.
`POST /api/v1/dispatch/sale-orders/{id}/finish-delivery` dùng FinishRoles/CanFinish; Sale chưa có quyền quản lý điều vận
được lọc bằng BuildScopeAsync/ApplyMerchandiseOrderVisibility ngay trong câu UPDATE, cùng company/active/trạng thái.
Đơn ngoài phạm vi, đã hủy/Delivered/Completed hoặc Sale đang gặp PaymentHold bị từ chối. Thành công ghi Delivered và event log
trong cùng transaction; đây là xác nhận giao xong, không tự tạo phiếu/ledger hoặc tăng khối lượng thực giao.
Role điều vận/quản lý/kho/kế toán hiện có giữ phạm vi đọc theo công ty. Quyền tạo/sửa/hủy phiếu và export không đổi.


Module phục vụ điều vận: xem PO còn lượng cần lập phiếu, chọn lot thành phẩm, tạo phiếu,
sửa phiếu Pending, theo dõi tiến trình và hủy phiếu. Dữ liệu được đọc từ database của HRM.api;
đường dẫn source legacy không phải nguồn dữ liệu runtime. Chuyển code không tự chuyển dữ liệu cũ.

- [Hướng dẫn luồng và contract cho FE](DISPATCH-FLOW.md).
- [Đối chiếu module cũ, cấu trúc Clean Architecture và hướng nâng cấp](LEGACY-MAPPING.md).
- [Contract nghiệp vụ chuyển từ legacy](LEGACY-CONTRACT.md).
- [Lịch sử thay đổi](CHANGELOG.md).

Điểm cần nhớ: `deliveredQuantity` ở selectable-lines là **lượng đã phân bổ vào phiếu còn hiệu lực**,
gồm cả Pending, không phải xác nhận khách đã nhận hàng. Phiếu `Canceled`/`Cancelled` không chiếm
lượng PO. Query chọn đơn dùng `Queries/DeliveryOrderQuantityQuery.cs`; POST/PUT cho phép quantity vượt
lượng đặt hoặc lượng còn lại của PO, nhưng vẫn phải đủ tồn theo product/lot/stockType. Dropdown tạo mới
hiển thị cả dòng đã phân bổ đủ/vượt lượng và PO `Delivered`/`Completed` còn active;
POST cho phép tạo phiếu mới cho các PO này. RemainingQuantity vẫn trả tối thiểu 0 để mô tả lượng còn thiếu,
không dùng làm điều kiện chọn/tạo phiếu. Quyền, company, khách nội bộ, active và validation lot/tồn giữ nguyên.

Module có luồng chọn đơn → tạo/sửa phiếu và tải PDF/Excel cho từng phiếu đã lưu.
POST/PUT/PATCH lưu phiếu tự đồng bộ RealQuantity và trạng thái dòng/header DHG; không cần in hoặc xuất PDF. GET xuất file chỉ đọc; POST print chỉ ghi HasPrinted và trả PDF. Ba báo cáo Excel kế hoạch/hoàn tất/vận chuyển đã có, xem LEGACY-CONTRACT.

## In PDF và xuất Excel từng phiếu

### Mã và tên hiển thị từ Product

GET `selectable-lines`, danh sách `delivery-orders` và detail `delivery-orders/{id}` bổ sung
`lines[].productDisplayCode`/`productDisplayName`: Product.ColourCode/Name hiện tại qua ProductId,
cùng company; hỗ trợ cả Product inactive của chứng từ lịch sử. Thiếu Product/khác company trả null,
không dùng snapshot thay thế. Ví dụ rút gọn một dòng:

```json
{"productExternalId":"OLD","productName":"Tên đã lưu","productDisplayCode":"CURRENT","productDisplayName":"Tên hiện tại"}
```

`productExternalId` vẫn là mã snapshot để đối chiếu tồn/Lot và tương thích payload cũ; `productName`
vẫn là tên snapshot legacy. FE chỉ dùng hai field display làm nhãn. Query projection lấy trực tiếp Product,
không gọi riêng từng sản phẩm; quyền đọc, company, capability và che giá vốn giữ nguyên.

Tên hiển thị trên danh sách/expand, detail và selectable-lines ưu tiên CustomerLabelDetail.FieldValue
có FieldKey=Name (trim, không phân biệt hoa thường), detail/header active, Header.CustomerId + Header.ProductId
khớp đúng khách hàng của phiếu/ĐHG và sản phẩm của dòng. Cả Customer và Product của header phải cùng company hiện tại.
Tên rỗng/trắng hoặc không có nhãn hợp lệ thì giữ Product.Name hiện tại; không fallback snapshot.
Nhiều header: ưu tiên UpdatedDate ?? CreatedDate mới nhất, rồi CreatedDate, Id header giảm dần,
LineNo/Id detail tăng dần. Chọn Name không trống; không lấy màu hoặc tên của khách hàng khác.
Query tên nhãn là subquery trong projection SQL hiện có, không gọi API/DB riêng cho từng dòng,
không join làm nhân quantity/Lot hoặc đổi phân trang. Không thêm bảng/cột, không ghi Product/snapshot.
FE căn giữa tên thành phẩm tại tạo/sửa, detail và expand; PDF căn giữa trong ô Tên SP, tự xuống dòng tên dài.
Tìm kiếm danh sách nhận cả mã/tên hiện tại và snapshot; selectable-lines giữ quy tắc phân nhánh keyword hiện hành,
nhánh tìm tổng quát nhận thêm tên Product hiện tại. Đổi nguồn hiển thị không sửa snapshot trong DB,
không đổi ProductId, lượng, Lot/reserve hay dữ liệu các báo cáo tổng hợp/kế hoạch giao hàng khác.

- `GET /api/v1/dispatch/delivery-orders/{id}/pdf`: file PDF A4, bảng hàng, tổng kg/bao và ô ký nhận.
- `GET /api/v1/dispatch/delivery-orders/{id}/excel`: file XLSX một sheet `Phieu giao hang`, có filter,
  cố định hàng tiêu đề, số lượng/ngày là dữ liệu typed, thiết lập in A4 ngang.
- Trả binary với MIME `application/pdf` hoặc
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, filename
  `delivery-order-{guid}.pdf/xlsx`; FE tải response dạng blob. Không bọc binary trong JSON.
- Dùng quyền đọc hiện có (`DeliveryOrderAccessRules.ReadRoles`), không thêm capability mới.
  Handler kiểm tra lại quyền và company; không tìm thấy, khác company hoặc phiếu inactive trả 404.
  Phiếu đã hủy còn active vẫn xuất được để đối chiếu; Excel hiển thị status, PDF giữ mẫu legacy không có ô status.
- Cả hai định dạng dùng cùng `DeliveryOrderDocumentQuery` và DTO riêng không chứa giá vốn/giá giao;
  SQL không đọc `UnitCostSnapshot`, `TotalCostSnapshot`, `DeliveryPrice` ở bất kỳ role nào.
- Tên sản phẩm trên PDF, Excel và POST in dùng cùng ưu tiên LabelDetail Name theo customerId/productId ở trên,
  fallback `Product.Name`. Mã màu tiếp tục lấy trực tiếp `Product.ColourCode`
  qua ProductId của dòng, cùng company với phiếu; kể cả Product đã ngừng hoạt động.
  Thiếu Product hoặc Product khác company trả null/hiển thị trống, không fallback snapshot.
  Đổi tên/mã Product sẽ đổi các lần xem/in sau, kể cả phiếu cũ; không sửa dữ liệu snapshot đã lưu.
- Thông tin phiếu, địa chỉ nhận là dữ liệu/snapshot đã lưu trên phiếu/dòng;
  tên khách hàng, đơn vị phát hành và người giao lấy từ danh mục hiện tại; phone dùng snapshot, fallback customer.Phone khi trống. Ngày in hiển thị là
  **ngày tạo phiếu** trong Excel. PDF theo nhãn legacy: `Ngày tạo/Create Date` là ngày xuất file trên server,
  `Ngày giao (Delivery date)` lấy ngày tạo phiếu đã lưu, không phải ngày xác nhận giao xong. Thiếu ngày đã lưu thì để trống.
- Mỗi dòng chứng từ là một delivery detail active; lot lấy consumption active, fallback `LotNoList`
  khi chưa có consumption. Không nhân quantity theo số lot; nhiều số VA hiển thị chung ô số lô và tự xuống dòng.
  Excel gắn nhãn hàng kèm và loại khỏi tổng kg/bao. PDF theo legacy: tên hàng không thêm nhãn, tổng cộng
  tính tất cả dòng được in, kể cả hàng kèm. Field tùy chọn null hiển thị trống; tổng 0 là số thực tính được.
- GET tải file nhiều lần không đổi `HasPrinted`, status, `RealQuantity`, timeline hay tồn kho. Không có
  SaveChanges/transaction ghi trong use case. Lượng giao được ghi nhận khi lưu POST/PUT/PATCH; xác nhận PO thủ công dùng command finish-delivery.
- Renderer PDF/Excel nằm ở Infrastructure, interface ở Application; DI đăng ký trong
  `PersistenceDependencyInjection`. PDF dùng font Open Sans và cấu hình license QuestPDF sẵn có.
- PDF dùng bố cục legacy `VA-WH-F18(05)`: A4 dọc, lề 12pt, header logo/nhà máy,
  bảng 7 cột song ngữ (có đơn vị Kg), ba ô ký Người lập phiếu/Thủ kho/Người nhận hàng và footer chứng nhận.
  Product code được nhóm, sắp giảm dần; ô quantity của công thức một nguyên liệu có nền xám.
  Logo/chứng nhận được đóng gói trong `Assets/Pdf`, không đọc source legacy khi chạy API.
  Báo cáo Excel nhiều phiếu và POST print được mô tả riêng trong LEGACY-CONTRACT.

## Quyền truy cập và phạm vi dữ liệu

`PLPUUser` có quyền Điều vận tương đương `DispatchUser` trong `DeliveryOrderAccessRules`:
đọc danh sách/detail/selectable-lines/available-lots/người giao/kế hoạch và xuất PDF/Excel;
tạo (bao gồm copy qua POST mới), PUT/PATCH, in, đổi trạng thái, hủy/xóa và xác nhận PO finish-delivery.
Controller dùng cùng ReadRoles/ManageRoles/CancelRoles với handler; không có policy Điều vận riêng.
Tạo danh mục người giao và backfill lot vẫn chỉ dành cho Admin. Không mở quyền xem cost.
Company, employee audit, active, canEdit, trạng thái, validation lot/tồn giữ nguyên.
Finish-delivery chỉ cập nhật trạng thái PO và timeline, không đổi lượng, tồn hoặc trạng thái phiếu.
Không có endpoint copy riêng; POST mới vẫn phải vượt qua mọi validation tạo phiếu.

Các API dưới `/api/v1/dispatch` yêu cầu đăng nhập. Create, update, list, detail và selectable-lines luôn lấy `CompanyId` từ current user; backend không dùng `CompanyId`, `CreatedBy` hoặc `UpdatedBy` do FE gửi. Create/update yêu cầu tài khoản được liên kết với một employee để ghi audit. Query detail/update lọc đồng thời theo `Id` và company hiện tại để tránh IDOR.

## Contract lot giao hàng

`GET /api/v1/dispatch/delivery-orders/available-lots` trả thêm `orderExternalIds` trên từng lot,
ví dụ `{ "lotNo": "VA261000001", "orderExternalIds": ["DHG261000044", "DHG261000012"] }`.
Lấy formula có ExternalId khớp lot (trim/không phân biệt hoa thường), đang được chọn trên MFG
(ProductionSelectVersion.ValidTo=null); formula, phiên bản chọn, MFG và PO cùng company,
formula/MFG/PO/dòng PO/liên kết MfgOrderPO active, dòng PO cùng ProductId được hỏi.
Không lọc trạng thái PO/MFG: đây là thông tin liên kết hiện hành, không phải eligibility tạo phiếu.
Trả mã DHG duy nhất, giảm dần; không có liên kết trả `[]`. Có thể chứa nhiều PO, không chỉ PO đang chọn.
Không suy từ lịch sử formula đã bỏ chọn; trường này không khóa lot hay thay đổi tồn/validation.

`POST /api/v1/dispatch/delivery-orders` và `PUT /api/v1/dispatch/delivery-orders/{id}` nhận từng phần tử `lines` theo contract:

```json
{
  "merchandiseOrderDetailId": "guid",
  "quantity": 10.5,
  "numOfBags": 2,
  "lots": [
    { "lotNo": "LOT-A", "quantity": 4.0 },
    { "lotNo": "LOT-B", "quantity": 6.5 }
  ]
}
```

- `lots` phải có ít nhất một phần tử; `lotNo` không được trống và `quantity` phải lớn hơn `0`.
- `line.quantity` phải đúng bằng tổng `lots[].quantity`; request sai bị từ chối. Giá trị lưu tại `DeliveryOrderDetail.Quantity` luôn lấy từ danh sách lot đã chuẩn hóa.
- Lot trùng nhau không phân biệt hoa/thường được gộp quantity. Nếu nhiều request line cùng `MerchandiseOrderDetailId`, backend gộp thành một delivery detail.
- `LotNoList` là field legacy/display và luôn được backend sinh lại từ lots đã chuẩn hóa.
- `UnitCostSnapshot` và `TotalCostSnapshot` không thuộc request DTO, FE không thể gán hai field này. Backend tính theo rule cost ở phần tồn kho bên dưới; dữ liệu không có nguồn cost phù hợp dùng fallback `0`.

### Tương thích FE cũ

Nếu `lots` không được gửi (`null`/absent), backend dùng `lotNoList` làm đúng một lot với quantity bằng `line.quantity`. Nếu FE gửi `lots: []`, request không hợp lệ và không fallback sang `lotNoList`.

Create và update ghi `DeliveryOrder`, `DeliveryOrderDetail` và `DeliveryOrderDetailLotConsumption` trong cùng một transaction. Update deactivate detail/lot cũ rồi tạo bộ detail/lot active mới trong transaction đó.

## Vòng đời phiếu giao

Phiếu mới luôn bắt đầu ở `Pending`; FE không được đặt trạng thái qua create/update nội dung. Transition hợp lệ:

```text
Pending -> InProgress -> Completed
   |            |
   +----------> Canceled
```

- `Completed` và `Canceled` là trạng thái kết thúc, không được chuyển sang trạng thái khác.
- Chuỗi legacy `Cancelled` được đọc như `Canceled`; mọi lần ghi mới dùng code chuẩn `Canceled`.
- Gọi đổi sang đúng trạng thái hiện tại hoặc hủy lại phiếu đã hủy trả thành công và không ghi update mới.
- `PUT /api/v1/dispatch/delivery-orders/{id}` chỉ sửa nội dung, lot và quantity khi phiếu active ở `Pending` và chưa in; nếu đã in dùng PATCH metadata/số bao hoặc DELETE để rollback. Gọi lại với nội dung giống hệt trả thành công mà không tạo detail/lot lịch sử mới.
- Cancel phiếu chưa in đổi status sang `Canceled`, hủy PRQ liên quan và giữ lịch sử. DELETE là use case xóa mềm legacy, có đồng bộ lại lượng đã lưu; xem LEGACY-CONTRACT.

### API lifecycle

- `PUT /api/v1/dispatch/delivery-orders/{id}/status` với body `{ "status": "InProgress" }` đổi trạng thái tiến trình; chuyển sang `Canceled` phải dùng API hủy để áp đúng quyền hủy.
- `POST /api/v1/dispatch/delivery-orders/{id}/cancel` hủy phiếu giao.

Mọi command lifecycle lọc theo `Id + current CompanyId`, yêu cầu current employee để ghi `UpdatedBy/UpdatedDate`, và kiểm tra role ở cả authorization attribute lẫn Application handler.

### Quyền

- Đọc list/detail/selectable-lines: `DispatchUser`, `PLPUUser`, `SaleUser`, `KHOUser`, `ACCUser`, `Leader`, `Edit`, `Delete`, `Admin`, `Developer`, `President`.
- Tạo, sửa nội dung, đổi trạng thái: `DispatchUser`, `PLPUUser`, `Edit`, `Admin`, `Developer`, `President`.
- Hủy: `DispatchUser`, `PLPUUser`, `Delete`, `Admin`, `Developer`, `President`.

## List và detail

`canEdit` chỉ bật khi phiếu đang active ở trạng thái `Pending`, chưa in và current user có quyền manage. Role chỉ có quyền đọc không nhận gợi ý sửa từ list hoặc detail.

`GET /api/v1/dispatch/delivery-orders` hỗ trợ filter `status`, `customerId`, `poNo`, `from`, `to`, `lotNo`, `isActive` và keyword/paging/sort hiện có. `from`/`to` lọc theo ngày tạo và bao gồm trọn ngày `to`; khoảng ngày đảo ngược trả trang rỗng. Filter `Canceled` bao gồm dữ liệu legacy `Cancelled`.

List và detail trả `status`, `lineCount`, `totalQuantity`, `totalNumOfBags`, `canEdit`, các line và lots active. Tổng lượng/tổng bao không tính attachment line.

## Response

`GET /api/v1/dispatch/delivery-orders` và `GET /api/v1/dispatch/delivery-orders/{id}` trả trên mỗi line:

```json
{
  "lotNoList": "LOT-A, LOT-B",
  "quantity": 10.5,
  "lots": [
    { "lotNo": "LOT-A", "quantity": 4.0 },
    { "lotNo": "LOT-B", "quantity": 6.5 }
  ]
}
```

`lots` chỉ đọc các `DeliveryOrderDetailLotConsumption` active. Ví dụ trên là response không có quyền xem cost; snapshot chỉ được trả khi có capability `dispatch.delivery-cost.view` như mô tả bên dưới.

## Backfill dữ liệu lot cũ

`POST /api/v1/dispatch/delivery-orders/lot-consumptions/backfill` chuyển dữ liệu lịch sử từ `DeliveryOrderDetail.LotNoList` sang `DeliveryOrderDetailLotConsumption` trong company của current user.

- Endpoint yêu cầu role `Admin` theo policy hiện có.
- `dryRun` mặc định là `true`; gọi với `dryRun=false` để ghi dữ liệu.
- Chỉ dòng có đúng một lot sau chuẩn hóa được chuyển; quantity lot bằng quantity của detail.
- Dòng đã có lot consumption được bỏ qua để bảo đảm idempotent.
- Backfill không có nguồn cost lịch sử nên đặt hai cost snapshot bằng `0`.
- Response trả các bộ đếm source/created/skipped để đối chiếu.

Phase 1 không tạo migration; entity/configuration và schema lot consumption phải có sẵn trước khi deploy API này.

## Phase 3: lot khả dụng, tồn kho và cost snapshot

### API lot khả dụng

`GET /api/v1/dispatch/delivery-orders/available-lots?merchandiseOrderDetailId={guid}&productId={guid}` trả các lot thành phẩm còn khả dụng cho đúng product và dòng PO. Backend bắt buộc dòng PO active, PO active, product khớp dòng PO và thuộc `CompanyId` của current user; tổ hợp không hợp lệ hoặc khác company không được trả dữ liệu.

Response thành công là danh sách lot trực tiếp; response lỗi giữ contract `OperationResult` với `success=false` và `message`:

```json
{
  "lotNo": "LOT-A",
  "stockType": "FinishedGood",
  "canSelect": true,
  "onHandQuantity": 20.0,
  "reservedQuantity": 3.0,
  "availableQuantity": 20.0,
  "unitCostSnapshot": 12500.0
}
```

`unitCostSnapshot` bị omit nếu người gọi không có capability `dispatch.delivery-cost.view`. Khi không có quyền, handler cũng không query dữ liệu công thức/cost không cần thiết. Mapping role ban đầu của capability này là `Admin`, `Developer`, `President`, `PriceView`, `ACCUser`, `SeePriceUser`; việc thay đổi mapping sau này không cần sửa các query giao hàng.

Response giữ riêng hai phần tử nếu cùng mã lot có cả hàng đạt và hàng lỗi.
`stockType` là `FinishedGood` hoặc `DefectiveFinishedGood`; không suy loại hàng từ mã lot.
Cả hai loại được chọn khi `canSelect=true` và `availableQuantity>0`.
Ví dụ hàng lỗi: `{ "lotNo": "VA260700418", "stockType": "DefectiveFinishedGood", "canSelect": true,
"onHandQuantity": 209.5, "reservedQuantity": 0, "availableQuantity": 209.5 }`.
FE dùng khóa option theo product + stockType + mã lot chuẩn hóa. Tồn tổng hợp từ shelf active,
không yêu cầu chọn riêng từng shelf. Chi tiết tạo/sửa và phân bổ đã lưu: [Hàng đạt/Hàng lỗi](STOCK-ALLOCATIONS.md).

### Rule tồn khả dụng

- Chỉ đọc `WarehouseShelfStock` của current company, loại `FinishedGood` hoặc `DefectiveFinishedGood`, quantity dương và nằm trên shelf active. Mã product của tồn phải khớp snapshot mã product trên dòng PO. Lot dùng `LotNo`, fallback sang `LotKey` khi `LotNo` trống.
- Tồn lot khả dụng bằng `max(0, on-hand)`, không trừ reserve. `reservedQuantity` vẫn trả lượng reserve Open theo lot của Hàng đạt để tham khảo; FE không trừ lại field này.
- Đồng thời tổng quantity yêu cầu của mọi lot thuộc cùng product + stockType không được vượt tổng on-hand product/type. Reserve theo lot hoặc chưa gắn lot không chặn giao hàng.
- Create/update gom quantity theo product + lot + stockType trước khi validate. Lot khác product, khác company, không nằm trên shelf active, loại tồn không phải hai loại thành phẩm hoặc không đủ tồn đều bị từ chối. Update có nội dung hoàn toàn giống dữ liệu đang lưu vẫn idempotent và không tạo snapshot/history mới.
- Mọi query dùng cho lookup/validation là `AsNoTracking` và company-scoped. Backend không nhận company từ FE.

### Cost snapshot và thời điểm tác động kho

Backend tự lấy unit cost từ `ManufacturingFormula` active của current company có `ExternalId` khớp lot, theo rule COGS hiện có: tổng `ManufacturingFormulaMaterials.TotalPrice` active có `itemType = Material`. Nếu dữ liệu lịch sử không có công thức tương ứng, unit cost theo fallback hiện hành là `0`. `TotalCostSnapshot = round(lot.quantity * UnitCostSnapshot, 2, AwayFromZero)`.

Request DTO nhận `lotNo`, `quantity` và `stockType` tùy chọn. Hai code hợp lệ là
`FinishedGood` và `DefectiveFinishedGood`; null/không gửi mặc định FinishedGood để tương thích FE cũ.
Loại khác bị từ chối. Không nhận cost từ FE. Snapshot PGH gộp theo mã lot; total cost là tổng chi phí
của các phân bổ loại hàng, unit cost là total cost / tổng lượng. GET list/detail giữ capability cost hiện hành.
PRQ lưu từng mã lot/loại tồn bằng ItemStockType hiện có, không thêm schema.
WarehouseTempStock chưa có loại tồn: reservedQuantity chỉ phản ánh reserve Hàng đạt; Hàng lỗi trả 0. Cả hai loại không trừ reserve khi lookup/validate giao hàng; dữ liệu reserve kho không bị sửa.


Delivery Order hiện **không reserve và không consume tồn kho ở bất kỳ status transition nào**. Create/update kiểm tra point-in-time và ghi cost snapshot trong cùng transaction với detail/lots/PRQ; không tạo `WarehouseTempStock`, không sửa `WarehouseShelfStock`. Việc reserve/consume chỉ được bổ sung khi nghiệp vụ kho có rule và transaction boundary chính thức.

Phase 3 không tạo migration.

## Loại khách nội bộ khỏi danh sách chọn PO

`GET /api/v1/dispatch/delivery-orders/selectable-lines` loại PO của KH_VIETAUS theo mã khách snapshot
hoặc mã khách hiện tại, bỏ khoảng trắng và không phân biệt hoa/thường. Bộ lọc áp dụng trước đếm/phân trang,
kể cả request chỉ định customerId hoặc merchandiseOrderId; không thay đổi phiếu giao đã lưu.

## Kế hoạch giao hàng theo ngày hứa

`GET /api/v1/dispatch/delivery-orders/selectable-lines` hỗ trợ thêm keyword theo
`Product.ColourCode` và `SampleRequest.ExternalId` liên kết product. Keyword được trim và chọn nhánh theo thứ tự:
- Bắt đầu bằng `DHG`: chỉ tìm `MerchandiseOrder.ExternalId`.
- Bắt đầu bằng `TP_`: chỉ tìm `SampleRequest.ExternalId`, không tìm mã màu.
- Bắt đầu bằng hai chữ cái ASCII khác (ví dụ `HD`, `OD`, `SK`, `TP`): chỉ tìm `SampleRequest.ExternalId` hoặc `Product.ColourCode`.
- Còn lại: tìm tổng quát theo các tiêu chí cũ.

Tiền tố và kết quả tìm không phân biệt hoa/thường. Trong mỗi nhánh vẫn tìm chứa toàn bộ keyword,
escape ký tự wildcard (`_` trong `TP_` là ký tự literal); product/sample request phải active cùng company.
Quy tắc field/nhánh nằm ở `Queries/GetSelectableDeliveryLines/SelectableDeliveryKeywordSearch.cs`;
helper chuẩn hóa keyword và áp dụng expression dùng chung được hướng dẫn tại
[`Commons/Searching`](../../../Commons/Searching/README.md).
Keyword tiếp tục lọc dòng trước khi gộp PO và phân trang, nên `lines` chỉ chứa các dòng còn đủ điều kiện và khớp keyword.
`lines[].bagWeightKg` lấy BagWeight dương từ yêu cầu mẫu active mới nhất của product/khách cùng company;
nếu thiếu hoặc BagWeight không dương thì trả mặc định 25 kg/bao. Đây là gợi ý cho FE, không ghi ngược vào yêu cầu mẫu.
Mỗi PO trả thêm `promisedDeliveryDate` (DateTime nullable), là MAX ExpectedDate của các MFG
active, chưa hủy, cùng company qua liên kết/dòng PO active; dùng chung rule với kế hoạch giao.
Ngày tính trên toàn bộ liên kết hợp lệ của PO, không chỉ các dòng khớp keyword. Không có ngày thì trả null.
Ví dụ `{ "externalId": "DHG261000001", "promisedDeliveryDate": "2026-10-13T00:00:00" }`.
Không thay đổi company scope, quyền, eligibility hoặc lượng/tồn. Đây là field đọc, không thêm vào payload tạo phiếu.

`GET /api/v1/dispatch/delivery-plan` mặc định ngày mai, gộp PO theo ngày hứa MFG muộn nhất,
chỉ giữ PO còn mở đúng ngày và PO quá hạn còn thiếu; loại PO Delivered/Completed/Canceled/Cancelled
kể cả phiếu chưa đủ lượng. Lượng đã giao đồng nhất timeline:
cộng mọi phiếu active chưa hủy, kể cả PO đang giao một phần.
Contract, filter và quy tắc lượng: [DELIVERY-PLAN.md](DELIVERY-PLAN.md).

## Phase 4: đọc entity-first và đối chiếu legacy

- `DeliveryOrderDetailLotConsumption` active là nguồn lot chuẩn cho list, detail, filter, keyword, timeline, complaint source và Executive PnL.
- `LotNoList` chỉ được đọc khi delivery detail không có lot consumption active. API list/detail sinh `LotNoList` display từ consumption; dữ liệu lịch sử chưa backfill mới trả chuỗi legacy. Với dòng legacy, `lots` có thể rỗng vì không thể suy ra quantity chính xác cho từng lot từ chuỗi cũ.
- Executive PnL dùng tổng `TotalCostSnapshot` của các lot active. Dòng lịch sử chưa backfill giữ fallback cũ: dashboard sales/product type và report tổng hợp resolve formula cost theo `LotNoList`; dashboard customer/trend dùng `BaseCostSnapshot`.
- Timeline hiển thị lot sinh từ consumption, fallback legacy. Complaint source trả lot consumption; dòng lịch sử chưa chuyển trả một lot fallback với `lotConsumptionId = null` và toàn bộ quantity của delivery detail.
- PDF/Excel Delivery Order đọc consumption active, fallback chuỗi legacy khi chưa có consumption. Complaint PDF đọc snapshot lot đã lưu trong Complaint Report.

### Backfill và reconciliation

`POST /api/v1/dispatch/delivery-orders/lot-consumptions/backfill?dryRun=true` chỉ role `Admin` được gọi. Endpoint luôn company-scoped và idempotent; detail đã có consumption không được tạo lại.

Response gồm các bộ đếm create/skip và `legacyRowCount`, `convertedRowCount`, `alreadyConvertedCount`, `skippedRowCount`, `quantityMismatchRowCount`, `legacyQuantityTotal`, `consumptionQuantityTotal`, `quantityDifference` (`consumption - legacy`). `dryRun=true` không ghi dữ liệu và reconciliation phản ánh trạng thái hiện tại; `wouldCreateCount` cho biết số dòng một-lot có thể chuyển. Sau lần chạy thật, reconciliation bao gồm consumption vừa tạo. Dòng legacy có nhiều lot bị bỏ qua vì không có dữ liệu phân bổ quantity đáng tin cậy.

Phase 4 không xóa `LotNoList` và không tạo migration.

Số bao từng lot (POST/PUT/PATCH) và bagWeightKg cho FE: xem [phân bổ giao hàng](STOCK-ALLOCATIONS.md#số-bao-từng-lot-và-gợi-ý-khối-lượng-đóng-gói-2026-10-12).

## Đồng bộ DHG khi lưu

POST tạo, PUT và PATCH lưu phiếu tính RealQuantity của từng dòng DHG bằng tổng quantity từ mọi dòng phiếu active, không phải hàng kèm, thuộc cùng company và có header active chưa Canceled/Cancelled. Pending cũng được tính, không phụ thuộc HasPrinted. Dòng có lượng 0 về Processing, chưa đủ ExpectedQuantity về Delivering, đủ ExpectedQuantity>0 về Delivered. Header Delivered khi tất cả dòng active đủ lượng; Delivering khi còn lượng giao; Processing khi không còn lượng.

Đồng bộ trong cùng transaction với phiếu/PRQ. PUT đồng bộ cả DHG cũ bị bỏ khỏi phiếu; lưu lại nội dung giống hệt vẫn đồng bộ nhưng không tạo dòng lịch sử mới. Cancel/DELETE tính lại sau khi loại phiếu, không trừ theo HasPrinted. In lại không cộng lượng. Các điều kiện sửa Pending chưa in và hủy chưa in giữ nguyên.

Phiếu cũ chưa in được tính khi một phiếu liên quan cùng DHG được lưu/hủy/xóa; không có backfill toàn database. RealQuantity trên DHG chịu tác động được đồng bộ theo các phiếu còn hiệu lực, không giữ lượng chỉnh tay không có phiếu nguồn. FE refresh DHG sau POST/PUT/PATCH thành công; status phiếu giao vẫn theo lifecycle riêng, không tự chuyển Completed và không xuất kho thực tế.
## Theo dõi lệnh giao dành cho IMS

Báo cáo hoàn thành giao hàng tại `/api/v1/dispatch/reports/finish-dashboard`
yêu cầu `reports.delivery-finish.view` cho IMS. Chỉ dùng dòng đơn bán active
cùng khách và công ty, loại khách nội bộ, khách inactive, đơn nội bộ và dòng
đính kèm. Doanh thu không được tính và trả `null`. Lô, số PO và ghi chú trả
`null`. Số lượng vẫn dùng ngày tạo phiếu theo quy ước của báo cáo.

`GET /api/v1/dispatch/delivery-orders/tracking` trả danh sách tối đa 25 lệnh
mỗi trang. `GET /api/v1/dispatch/delivery-orders/{id}/tracking` trả đầu lệnh
và dòng hàng. Hai API yêu cầu `dispatch.delivery.tracking.view`, ngữ cảnh
nhân viên/công ty và khách active cùng công ty. Khách nội bộ bị loại.

Đầu lệnh gồm mã, tên khách, ngày tạo và trạng thái vận hành. Chi tiết gồm
mã/tên và lượng của dòng active liên kết đơn bán active cùng công ty/cùng
khách. Bỏ dòng đính kèm và đơn nội bộ. API không tải cước, thanh toán, ghi chú,
lô hoặc công thức. API lệnh giao đầy đủ và các bản in/xuất yêu cầu tài khoản
không mang hạn chế IMS. Token explicit cần role claim và refresh sau khi cấp.
