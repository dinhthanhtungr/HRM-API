# Delivery Orders

## Quyền truy cập và phạm vi dữ liệu

Các API dưới `/api/v1/dispatch` yêu cầu đăng nhập. Create, update, list, detail và selectable-lines luôn lấy `CompanyId` từ current user; backend không dùng `CompanyId`, `CreatedBy` hoặc `UpdatedBy` do FE gửi. Create/update yêu cầu tài khoản được liên kết với một employee để ghi audit. Query detail/update lọc đồng thời theo `Id` và company hiện tại để tránh IDOR.

## Contract lot giao hàng

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
- `UnitCostSnapshot` và `TotalCostSnapshot` không thuộc request DTO, FE không thể gán hai field này. Phase 1 để backend khởi tạo snapshot bằng `0` cho đến khi có rule tính cost riêng.

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
- `PUT /api/v1/dispatch/delivery-orders/{id}` chỉ sửa nội dung, lot và quantity khi phiếu active ở `Pending`. Gọi lại với nội dung giống hệt trả thành công mà không tạo detail/lot lịch sử mới.
- Hủy chỉ đổi status sang `Canceled`; không xóa cứng header, detail hoặc lot consumption.

### API lifecycle

- `PUT /api/v1/dispatch/delivery-orders/{id}/status` với body `{ "status": "InProgress" }` đổi trạng thái tiến trình; chuyển sang `Canceled` phải dùng API hủy để áp đúng quyền hủy.
- `POST /api/v1/dispatch/delivery-orders/{id}/cancel` hủy phiếu giao.

Mọi command lifecycle lọc theo `Id + current CompanyId`, yêu cầu current employee để ghi `UpdatedBy/UpdatedDate`, và kiểm tra role ở cả authorization attribute lẫn Application handler.

### Quyền

- Đọc list/detail/selectable-lines: `DispatchUser`, `SaleUser`, `KHOUser`, `ACUser`, `Leader`, `Edit`, `Delete`, `Admin`, `Developer`, `President`.
- Tạo, sửa nội dung, đổi trạng thái: `DispatchUser`, `Edit`, `Admin`, `Developer`, `President`.
- Hủy: `DispatchUser`, `Delete`, `Admin`, `Developer`, `President`.

## List và detail

`canEdit` chỉ bật khi phiếu đang active ở trạng thái `Pending` và current user có quyền manage. Role chỉ có quyền đọc không nhận gợi ý sửa từ list hoặc detail.

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

`lots` chỉ đọc các `DeliveryOrderDetailLotConsumption` active. Response không trả cost snapshot.

## Backfill dữ liệu lot cũ

`POST /api/v1/dispatch/delivery-orders/lot-consumptions/backfill` chuyển dữ liệu lịch sử từ `DeliveryOrderDetail.LotNoList` sang `DeliveryOrderDetailLotConsumption` trong company của current user.

- Endpoint yêu cầu role admin/super-user theo policy hiện có.
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
  "onHandQuantity": 20.0,
  "reservedQuantity": 3.0,
  "availableQuantity": 17.0,
  "unitCostSnapshot": 12500.0
}
```

`unitCostSnapshot` bị omit nếu người gọi không có quyền xem giá công thức. Khi không có quyền, handler cũng không query dữ liệu công thức/cost không cần thiết. Các role được xem cost là nhóm `FormulaPriceViewers`: `Admin`, `Developer`, `President`, `PriceView`, `ACUser`, `SeePriceUser`.

### Rule tồn khả dụng

- Chỉ đọc `WarehouseShelfStock` của current company, loại `FinishedGood`, quantity dương và nằm trên shelf active. Mã product của tồn phải khớp snapshot mã product trên dòng PO. Lot dùng `LotNo`, fallback sang `LotKey` khi `LotNo` trống.
- Tồn lot khả dụng bằng on-hand của lot trừ reserve `WarehouseTempStock` trạng thái `Open` có cùng `LotKey`.
- Đồng thời tổng quantity yêu cầu của mọi lot thuộc cùng product không được vượt tổng on-hand product trừ toàn bộ reserve `Open`, kể cả reserve chưa gắn `LotKey`. Vì vậy reserve tổng không thể bị né bằng cách chia quantity qua nhiều lot hoặc nhiều line.
- Create/update gom quantity theo product + lot trước khi validate. Lot khác product, khác company, không nằm trên shelf active, lot lỗi/không phải thành phẩm hoặc không đủ tồn đều bị từ chối. Update có nội dung hoàn toàn giống dữ liệu đang lưu vẫn idempotent và không tạo snapshot/history mới.
- Mọi query dùng cho lookup/validation là `AsNoTracking` và company-scoped. Backend không nhận company từ FE.

### Cost snapshot và thời điểm tác động kho

Backend tự lấy unit cost từ `ManufacturingFormula` active của current company có `ExternalId` khớp lot, theo rule COGS hiện có: tổng `ManufacturingFormulaMaterials.TotalPrice` active có `itemType = Material`. Nếu dữ liệu lịch sử không có công thức tương ứng, unit cost theo fallback hiện hành là `0`. `TotalCostSnapshot = round(lot.quantity * UnitCostSnapshot, 2, AwayFromZero)`.

Request DTO chỉ có `lotNo` và `quantity`; không có field cost để FE bind. GET list/detail chỉ trả `UnitCostSnapshot` và `TotalCostSnapshot` cho role được phép; với role khác, hai field bị omit khỏi JSON.

Delivery Order hiện **không reserve và không consume tồn kho ở bất kỳ status transition nào**. Create/update chỉ kiểm tra point-in-time và ghi cost snapshot trong cùng transaction với detail/lots; không tạo `WarehouseTempStock`, không sửa `WarehouseShelfStock`. Việc reserve/consume chỉ được bổ sung khi nghiệp vụ kho có rule và transaction boundary chính thức.

Phase 3 không tạo migration.

## Phase 4: đọc entity-first và đối chiếu legacy

- `DeliveryOrderDetailLotConsumption` active là nguồn lot chuẩn cho list, detail, filter, keyword, timeline, complaint source và Executive PnL.
- `LotNoList` chỉ được đọc khi delivery detail không có lot consumption active. API list/detail sinh `LotNoList` display từ consumption; dữ liệu lịch sử chưa backfill mới trả chuỗi legacy. Với dòng legacy, `lots` có thể rỗng vì không thể suy ra quantity chính xác cho từng lot từ chuỗi cũ.
- Executive PnL dùng tổng `TotalCostSnapshot` của các lot active. Dòng lịch sử chưa backfill giữ fallback cũ: dashboard sales/product type và report tổng hợp resolve formula cost theo `LotNoList`; dashboard customer/trend dùng `BaseCostSnapshot`.
- Timeline hiển thị lot sinh từ consumption, fallback legacy. Complaint source trả lot consumption; dòng lịch sử chưa chuyển trả một lot fallback với `lotConsumptionId = null` và toàn bộ quantity của delivery detail.
- Repo hiện không có PDF/Excel exporter trực tiếp cho Delivery Order. Complaint PDF đọc snapshot lot đã lưu trong Complaint Report, nên không đọc trực tiếp `DeliveryOrderDetail.LotNoList`.

### Backfill và reconciliation

`POST /api/v1/dispatch/delivery-orders/lot-consumptions/backfill?dryRun=true` chỉ role `Admin` được gọi. Endpoint luôn company-scoped và idempotent; detail đã có consumption không được tạo lại.

Response gồm các bộ đếm create/skip và `legacyRowCount`, `convertedRowCount`, `alreadyConvertedCount`, `skippedRowCount`, `quantityMismatchRowCount`, `legacyQuantityTotal`, `consumptionQuantityTotal`, `quantityDifference` (`consumption - legacy`). `dryRun=true` không ghi dữ liệu và reconciliation phản ánh trạng thái hiện tại; `wouldCreateCount` cho biết số dòng một-lot có thể chuyển. Sau lần chạy thật, reconciliation bao gồm consumption vừa tạo. Dòng legacy có nhiều lot bị bỏ qua vì không có dữ liệu phân bổ quantity đáng tin cậy.

Phase 4 không xóa `LotNoList` và không tạo migration.
