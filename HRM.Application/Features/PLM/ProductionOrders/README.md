# Lệnh sản xuất MFG

Feature cung cấp thao tác đọc và tạo cho `manufacturing.MfgProductionOrders`.

## API kiểm tra Product đang sản xuất

`GET /api/v1/plm/production-orders/products/{productId}/is-in-production`

Endpoint yêu cầu đăng nhập và trả trạng thái kiểm tra cùng danh sách mã
`ManufacturingFormula` hiện hành của các lệnh thỏa điều kiện:

```json
{
  "isInProduction": true,
  "manufacturingFormulaExternalIds": [
    "VA260900001",
    "VA260900002"
  ]
}
```

- `isInProduction = true`: trong công ty hiện tại có ít nhất một
  `MfgProductionOrder` active của Product ở một trong các trạng thái đang sản xuất
  được quy định bên dưới.
- `isInProduction = false`: không có lệnh thỏa điều kiện, `productId` rỗng, hoặc
  current user không có `CompanyId` hợp lệ.
- `manufacturingFormulaExternalIds` lấy từ `ProductionSelectVersion` hiện hành
  (`ValidFrom != null`, `ValidTo == null`) của từng lệnh, lọc cùng công ty, bỏ mã
  rỗng, chống trùng không phân biệt hoa thường và sắp xếp tăng dần.
- Danh sách có thể rỗng dù `isInProduction = true` khi lệnh thỏa trạng thái nhưng
  chưa gắn `ManufacturingFormula` hiện hành.
- Dữ liệu luôn được lọc bằng `CompanyId` của current user để tránh đọc chéo công ty.

## Trạng thái MfgProductionOrder

Các trạng thái được khai báo trong enum `ManufacturingProductOrder`:

1. `Unknown`
2. `New`
3. `FormulaRequested`
4. `FormulaSuccess`
5. `Scheduling`
6. `Scheduled`
7. `Waiting`
8. `Unassign`
9. `Getback`
10. `QCinprogress`
11. `QCPassed`
12. `QCFail`
13. `BTPNew`
14. `Weighting`
15. `Weighted`
16. `Mixing`
17. `Mixed`
18. `Started`
19. `Canceled`
20. `Running`
21. `Finished`
22. `Change`
23. `Unassignfrommd`
24. `Reported`
25. `Done`
26. `Stocked`

Trong contract của endpoint này, các trạng thái sau mang nghĩa **đang sản xuất**:

`Unassign`, `Getback`, `QCinprogress`, `QCPassed`, `QCFail`, `BTPNew`, `Weighting`,
`Weighted`, `Mixing`, `Mixed`, `Started`, `Canceled`, `Running`.

Lưu ý: `Canceled` được tính là đang sản xuất theo contract nghiệp vụ hiện tại của
endpoint này.

Endpoint kiểm tra Product chỉ đọc database, không publish notification và không thay đổi SignalR/Web Push.

## API tạo lệnh sản xuất

Hai endpoint yêu cầu đăng nhập, EmployeeId/CompanyId hợp lệ và capability
`plm.production-order.create`. Capability cho phép thiết lập thông tin kỹ thuật,
snapshot công thức, đơn giá thỏa thuận và đơn giá dòng công thức khi tạo; response
chỉ trả ID/mã, không trả cost hoặc vật tư. Mọi reference DB phải active và cùng company;
VU phải thuộc đúng Product. Company/audit/ID/mã do backend xác định.

Fallback role: `Admin`, `Developer`, `President`, `PLPUUser`, `QLSXUser`, `ManufactureUser`.
`SaleUser`/`LabUser` không mặc nhiên có quyền tạo. Claim DB có thể cấp/thu hồi capability độc lập.
Baseline seed/rollback hiện có đã bổ sung capability; không thêm hoặc chạy migration.
Môi trường dùng permission DB phải cấp claim bằng màn hình quản lý role hoặc chạy lại seed
đã review. User cần login/refresh để token nhận quyền mới.

### Tạo nội bộ

`POST /api/v1/plm/production-orders/internal`

Request theo `CreateInternalProductionOrderRequest`: `ProductId`, `TotalQuantityRequest`
(số nguyên > 0), `BagType` không trắng; ngày, số mẻ, lượng thực tế, đơn giá và note
giữ semantics DTO cũ. `InitialStatus` null/trắng => `New`, giá trị khác phải là tên enum
`ManufacturingProductOrder` hợp lệ (sau trim). Không tự giới hạn về `New`/`Scheduling`.
`StepOfProduct` phải là enum hợp lệ. Không thêm rule giá/số mẻ/lượng thực tế ngoài code cũ.

Product/customer/VU snapshot lấy từ DB hiện tại. CustomerId/FormulaId null => không liên kết;
ID được gửi nhưng không hợp lệ, inactive, khác company hoặc VU sai Product bị từ chối.
Đây là bổ sung bảo vệ reference so với legacy vốn bỏ qua customer/VU không tìm thấy.
`MerchandiseOrderDetailId` vẫn nhận để tương thích DTO nhưng **không được sử dụng**,
đúng implementation `CreateInternalAsync` cũ.

Tạo một MFG mã `MFGyyMMxxxxx` và timeline trong cùng transaction. Không tạo link MFG–PO,
VA, phiên bản chọn, lịch hay reservation, kể cả InitialStatus là Scheduling.

Response HTTP 200:

```json
{"success":true,"message":"Tạo lệnh sản xuất nội bộ thành công.","data":"<mfgProductionOrderId>"}
```

### Tạo theo thông tin detail, có thể kèm công thức

`POST /api/v1/plm/production-orders/inform`

Request theo `CreateProductionOrderInformRequest`; bắt buộc OrderId, DetailId, ProductId,
RequiredDate khác default. Detail active phải thuộc đúng order active, cùng company và
khớp Product. Không tự thêm kiểm tra detail đã có MFG: legacy cho phép nhiều MFG trên detail.
Customer/VU/VA nguồn và item/category được kiểm tra company/active trước khi ghi.
VA nguồn chỉ được sử dụng/kiểm tra khi có dòng công thức active với Quantity > 0.

Snapshot mã/tên product/customer/VU/VA và vật tư lấy từ request như code cũ, không tự thay
bằng dữ liệu hiện tại. `MerchandiseOrderExternalId` không dùng để sinh mã hoặc tìm order.
FormulaCustomerSelect = Guid.Empty => MFG.FormulaId và VA.SourceVUFormulaId null.
BagType null => chuỗi rỗng. Nullable date/quantity/note được lưu đúng giá trị gửi vào.

- Chỉ dòng `IsActive=true && Quantity>0` được dùng; dòng inactive/quantity <= 0 bị bỏ qua.
- Không có dòng hợp lệ: chỉ tạo MFG `New`, link MFG–PO và timeline. Không có VA/lịch/reservation.
- Có dòng hợp lệ: tổng tỷ lệ phải bằng 1 với dung sai 0.0001; tạo MFG `Scheduling`,
  VA `Checking`, `Name=COPY`, `SourceType=FromVA` kể cả không gửi VA nguồn.
- VA.TotalPrice = tổng UnitPrice × Quantity của các dòng hợp lệ; không làm tròn trong Application.
  Precision database hiện có vẫn áp dụng khi persist. Dòng sắp theo LineNo gửi vào rồi đánh số lại từ 1.
- Lot trim; trắng hoặc N/A không phân biệt hoa thường => null. StockType của lot quyết định
  MaterialFailure/ProductFailure, không dựa vào IsDefective/QualityStatus hoặc LotNo.
  Không có lot lỗi thì MaterialFailure/ProductFailure trở lại Material/Product.
- Tạo ProductionSelectVersion hiệu lực ngay (`ValidFrom=now`, `ValidTo=null`) và một
  SchedualMfg `Scheduling`, note ưu tiên LabNote rồi PlpuNote, delivery plan lấy ExpectedDate.
- Có VA và TotalQuantity > 0 mới giữ chỗ; null/0/âm không gọi reservation như legacy.
- Reservation dùng **mã MFG** trong cột VaCode. Material lấy mã snapshot; Product/ProductFailure
  lấy ColourCode hiện tại. Trim/uppercase mã, nhóm theo mã và cộng Quantity × TotalQuantity.
  Bỏ mã trống; nếu không còn mã hợp lệ thì toàn bộ transaction thất bại.
  Không lưu lot reservation, không kiểm tra tồn thực tế, không phát sinh xuất kho.
- Khi đồng bộ reservation đã có: ưu tiên dòng không lot rồi TempId nhỏ nhất; giữ dòng trùng,
  không chặn giảm dưới QtyUsed; mã không còn trong target => Cancelled, QtyRequest=0, giữ QtyUsed.
- Giữ thứ tự query link trước SaveChanges như `CreateInformAsync` cũ. Vì link đang Added
  chưa có trong DB, bước sync SaleOrder sang Processing không chạy trong lần tạo thông thường.
  Không sửa hành vi này trong task chuyển 1–1. Nếu query thấy link đã persist thì chỉ chuyển
  New/Approved (status không parse được fallback New) sang Processing và ghi timeline.
- Không publish cảnh báo giá/notification: helper cảnh báo giá trong service cũ không được
  CreateInformAsync gọi. Không tự tạo FormulaVersion hoặc snapshot bổ sung.

Response HTTP 200, data là ID/mã **đã persist**, không phải preview:

```json
{
  "success": true,
  "message": "Tạo lệnh sản xuất và công thức thành công.",
  "data": {
    "mfgProductionOrderId": "<guid>", "externalId": "MFG261000001",
    "manufacturingFormulaId": "<guid>", "manufacturingFormulaExternalId": "VA261000001"
  }
}
```

Hai field manufacturingFormula là null khi không tạo VA. Lỗi validation/capability/reference
hoặc ghi dữ liệu trả HTTP 400 với success=false; chưa authenticated trả 401 theo Authorize.
Cancellation được propagate. Transaction dispose rollback khi return fail/cancellation;
exception ghi dữ liệu rollback và trả lỗi chung, không lộ exception/payload/giá.
MFG, link, VA/items, selection, schedule, reservation và timeline dùng một scoped
ApplicationDbContext và một transaction, SaveChanges/Commit chỉ tại handler.

## Tương thích với luồng SaleOrder đã có

Approve/auto approve vẫn gọi `SaleOrderManufacturingService`: một MFG New và một link cho mỗi
detail, giữ FormulaId VU của detail; không tạo VA/selection/lịch/reservation tại bước duyệt.
Snapshot product lấy DB; RequiredDate lấy delivery request (fallback now với default trong model mới),
ExpectedDate/TotalQuantity/NumOfBatches null. Không thay đổi rule duyệt hoặc contract SaleOrder.
Repository cũ được thay bằng DbContext abstraction ở Application theo kiến trúc repo hiện tại.

Phạm vi này chuyển **các luồng tạo MFG**, không chuyển các API sửa, hoàn tất, QC, danh sách,
detail, điều chỉnh công thức, color-chip, PDF/Excel của toàn bộ Manufacturing cũ.
