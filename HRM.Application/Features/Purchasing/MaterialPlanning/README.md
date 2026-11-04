# Lập nhu cầu vật tư từ đơn bán

API read-only cho danh sách đơn và cây thành phần mở từng cấp. Tính trực tiếp từ dữ liệu nghiệp vụ;
không dùng `material_planning`, không tạo reservation, PO, migration hay background job.
API nhu cầu tổng hợp cũ `/api/v1/purchasing/material-requirements` giữ nguyên để tương thích.
Giai đoạn này chưa có tổng hợp cần mua, phân bổ tồn/PO hoặc giao diện mới.

## Phạm vi và quyền

- Đơn bán active, CompanyId của current user, mọi trạng thái (kể cả Completed, Delivered, Paused, Cancelled).
  Không yêu cầu đã có MFG/reservation; không áp dụng bộ lọc ba trạng thái của API tổng hợp cũ.
- Dòng đơn phải active và thuộc đơn trong phạm vi; thay đổi trạng thái không chặn mở dòng hoặc bung cây.
- Mọi endpoint yêu cầu đăng nhập và `Purchasing.PurchaseOrder.Manage`/capability
  `purchasing.purchase-order.manage`; handler kiểm tra lại capability.
- Xem dòng/MFG cần thêm `plm.production-order.view`; expand cần thêm `plm.formula-material.view`
  theo constant `ApplicationPermissions.PLM.ViewFormulaMaterials`. Không thay mapping role/claims.
- Controller trả 403 khi thiếu quyền bổ sung. Handler gọi trực tiếp vẫn fail closed.
  ID không tồn tại/khác company/ngoài phạm vi dùng cùng lỗi 400, không tiết lộ bản ghi chéo công ty.
- Không query/trả giá, cost, margin hoặc thông tin liên hệ khách hàng. Tên khách là snapshot của đơn.

## 1. Danh sách đơn

`GET /api/v1/purchasing/material-planning/orders?keyword=DHG&pageNumber=1&pageSize=15`

Keyword literal không phân biệt hoa thường trên mã đơn, số PO và tên khách snapshot.
Phân trang tại DB, mặc định 15 dòng, tối đa 100. Sort mã đơn giảm dần, ID tăng dần để ổn định.
Chưa tải công thức, tồn kho hoặc cây trong request này.

```json
{
  "items": [{
    "orderId": "11111111-1111-1111-1111-111111111111",
    "code": "DHG261000001", "poNumber": "PO-KH-01",
    "customerName": "Khách A", "status": "Approved", "lineCount": 2
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 15
}
```

`lineCount` đếm dòng active, không phải số Product phân biệt hay số MFG.

## 2. Mở đơn lấy sản phẩm

`GET /api/v1/purchasing/material-planning/orders/{orderId}/lines?pageNumber=1&pageSize=15`

Trả PagedResult như danh sách đơn, sort ID dòng tăng dần. Chỉ query tồn/MFG cho các dòng thuộc trang.
Mỗi item gồm:

```json
{
  "lineId": "22222222-2222-2222-2222-222222222222",
  "productId": "33333333-3333-3333-3333-333333333333",
  "code": "TP001", "name": "Sản phẩm A",
  "remainingQuantityKg": 100, "onHandQuantityKg": 20,
  "requiredDate": "2026-10-20T00:00:00",
  "productionOrders": []
}
```

- `remainingQuantityKg = Max(0, ExpectedQuantity - lượng giao hợp lệ)` theo rule phiếu giao của
  kế hoạch giao hàng; giao dư dòng khác không bù dòng này. Chưa trừ tồn TP.
- `requiredDate` là DeliveryRequestDate của dòng đơn, không phải ngày MFG.
- Mã/tên Product hiện tại active cùng company; snapshot dòng đơn chỉ fallback khi nguồn không còn.
  Product không hợp lệ không được dùng đọc tồn/expand.
- `productionOrders`: các liên kết MfgOrderPO active, dòng/đơn/MFG active cùng company,
  loại MFG Canceled/Cancelled; mỗi phần tử có productionOrderId, code và status. Rỗng là chưa có MFG hợp lệ.
  Không dùng tổng khối lượng MFG để gán toàn bộ cho từng đơn vì MFG có thể phục vụ nhiều đơn.

## 3. Expand công thức một cấp

`POST /api/v1/purchasing/material-planning/orders/{orderId}/lines/{lineId}/expansion`

Mở gốc:

```json
{ "productionOrderId": null, "path": [] }
```

Mở Product con: thêm `sourceLineId` của node vào path cha. Path dùng ID dòng
ManufacturingFormulaMaterial (VA) hoặc FormulaMaterial (VU), không dùng ProductId để phân biệt cùng Product xuất hiện nhiều lần.

```json
{ "path": ["44444444-4444-4444-4444-444444444444"] }
```

Luôn giữ cùng productionOrderId gốc khi mở tiếp. FE không truyền quantity, company, formulaId;
backend replay đường dẫn, tự tính quantity và kiểm tra quyền/liên kết ở mọi request.

```json
{
  "formula": {
    "formulaId": "55555555-5555-5555-5555-555555555555",
    "code": "VA261000001", "source": "LatestProductionVa",
    "productionOrderId": "66666666-6666-6666-6666-666666666666"
  },
  "outputQuantityKg": 100, "issue": null,
  "items": [{
    "itemId": "77777777-7777-7777-7777-777777777777",
    "sourceLineId": "44444444-4444-4444-4444-444444444444",
    "itemType": 1, "code": "SR260900001", "name": "[TP002] Product con",
    "requiredQuantityKg": 50, "onHandQuantityKg": 10,
    "canExpand": true, "issue": null
  }]
}
```

### Chọn công thức VA và fallback VU

1. Nếu dòng có đúng một MFG hợp lệ, tự dùng MFG đó. Nếu nhiều MFG và chưa truyền ID,
   trả `issue=SelectProductionOrder`, items rỗng; FE cho chọn từ productionOrders của dòng.
2. Với MFG đã xác định, ưu tiên ProductionSelectVersion hiện hành (`ValidFrom != null`, `ValidTo == null`).
   VA active, cùng company; MFG cùng Product của dòng. Trả source `SelectedMfg`.
3. Nếu chưa có MFG/công thức được chọn, dùng lịch sử ProductionSelectVersion của Product:
   `ValidFrom` mới nhất, tie-break ProductionSelectVersionId giảm dần. Cho phép lịch sử đã đóng
   để tìm VA từng được chọn sản xuất; chỉ MFG active không hủy, VA active cùng company.
   Trả source `LatestProductionVa`. Chỉ khi không tìm được VA hợp lệ mới fallback Formula VU; không dùng ProductStandardFormula.
4. Fallback VU lấy `Formulas`/`FormulaMaterials` active, cùng Product và company; Product cũng phải active cùng company.
   Ưu tiên FormulaId đang gắn trực tiếp trên dòng đơn (source `SelectedOrderVu`), sau đó `IsSelect=true`,
   rồi `UpdatedDate ?? CreatedDate` mới nhất, tie-break FormulaId giảm dần (source `LatestVu`).
   Đây là công thức VU hiện tại, không phải snapshot của lệnh sản xuất mẫu ManufacturingVUFormula.
5. Product con dùng cùng rule VA lịch sử rồi fallback VU theo Product; không kế thừa MFG hoặc FormulaId của đơn gốc.
   VA hợp lệ nhưng rỗng vẫn trả EmptyFormula, không âm thầm đổi sang VU.

`formula.formulaId` là ManufacturingFormulaId khi nguồn VA, là FormulaId khi nguồn VU.
`formula.productionOrderId` là null với VU vì đây không phải công thức đã gắn một MFG.
FE giữ nguyên path theo sourceLineId backend trả về; không suy luận loại dòng từ GUID hoặc tự đổi sang ProductId.

### Khối lượng, tồn và tính đầy đủ

- Đây là **preview nhu cầu thô**, không phải số lượng cần mua hay reservation đã persist.
  Khối lượng gốc là lượng dòng đơn còn cần giao; mỗi cấp nhân hệ số Quantity (kg đầu vào/kg đầu ra).
  Không round trung gian, không tự normalize hệ số, không cộng hao hụt MFG hoặc trừ lượng đã dùng.
- Chưa trừ tồn TP/Product con trước khi bung thành phần, chưa trừ PO đang về; tổng hợp cân đối làm sau.
  Không cộng các số tồn hiển thị giữa các nhánh vì là nguồn chung.
- Tồn là số hiện tại theo mã kho: Material.ExternalId cho NVL, Product.ColourCode cho Product,
  trim/uppercase. Chỉ kệ active cùng company; RawMaterial cho NVL, FinishedGood cho Product.
  Gồm kho cân trộn; loại hàng lỗi/chưa QC. Không có dòng tồn trả 0, không phải đã xác nhận thiếu cần mua.
- Tên/mã item dùng FormulaItemDisplayResolver hiện hành: Product hiển thị mã Sample Request gần nhất
  và tên có ColourCode; mã hiển thị có thể khác mã dùng join tồn kho.
- Material/Product active cùng company mới có tồn/expand. Chỉ hỗ trợ itemType Material (0), Product (1),
  unit kg, hệ số dương. Item lỗi/hàng lỗi, unit khác hoặc hệ số không hợp lệ được báo issue,
  requiredQuantityKg=null (chưa tính được), không tự quy đổi. Giá trị 0 hợp lệ nghĩa là lượng gốc còn cần bằng 0.
- `canExpand=true` nghĩa là Product đủ điều kiện thử mở tiếp, chưa khẳng định có công thức VA/VU con.
  Thiếu cả VA và VU trả `MissingFormula`; FE giữ cảnh báo thay vì coi nhu cầu bằng 0.

### Giới hạn và lỗi

Tối đa 10 bước path, 200 dòng trực tiếp/công thức. Vòng Product theo đường dẫn bị chặn.
Path sai/ID rỗng/truy cập ngoài scope trả 400. Path lỗi vì công thức thay đổi yêu cầu tải lại cây.
Các trường hợp nghiệp vụ trả HTTP 200 với issue rõ: `SelectProductionOrder`, `MissingFormula`,
`ProductUnavailable`, `EmptyFormula`, `CycleDetected`, `DepthLimitReached`, `NodeLimitExceeded`,
`ItemUnavailable`, `UnsupportedItemType`, `UnsupportedUnit`, `InvalidQuantity`.
NodeLimitExceeded không trả kết quả bị cắt âm thầm. Decimal overflow trả 400.
Không bảo đảm snapshot xuyên request: tồn, trạng thái và công thức có thể đổi khi người dùng đang mở cây.
Chưa cache, chưa tạo chỉ mục/schema mới; đánh giá execution plan trên tải thực trước khi tối ưu thêm.

## Xem toàn bộ đơn không đồng nghĩa mọi đơn đều cần mua

Cây ở màn hình này chỉ là preview BOM theo lượng còn phải giao, không phải đề nghị mua đã xác nhận.
Đơn Completed/Delivered/Cancelled/Paused vẫn xem được; trạng thái, ngày cần và lượng chưa giao cần được
FE hiển thị để người dùng phân biệt lịch sử, dự kiến và nhu cầu đang thực hiện. Không tự gán nhãn cần mua
chỉ vì cây có số lượng dương; đơn hủy/đã đóng có thể vẫn còn lượng chưa giao trong dữ liệu lịch sử.

API cân đối hiện có `GET /api/v1/purchasing/material-requirements` lấy nhu cầu reservation sản xuất còn mở,
tồn kho và PO InProgress chưa nhập đủ. `pendingPurchaseQuantityKg` là đã đặt và đang chờ nhập, không phải đã nhập.
`purchaseQuantityKg = Max(0, requiredQuantityKg - onHandQuantityKg - pendingPurchaseQuantityKg)` là số thiếu
ở mức tổng NVL. Không trừ cùng một tồn/PO cho từng đơn hoặc cộng cả reservation và BOM của cùng nhu cầu.

API cân đối này chưa xét PO về kịp ngày cần, chưa phân bổ nguồn cung theo từng đơn và chưa bao phủ đơn chưa có
reservation. Vì vậy chưa đủ để khẳng định đơn nào chắc chắn đủ hàng đúng hạn; số cần mua bằng 0 không chứng minh
nguồn đang về sẽ đến kịp. Phân loại cần ngay/sắp cần cần kế hoạch sản xuất, ngày sử dụng NVL và thời gian NCC giao;
không tự đặt ngưỡng ngày hoặc tạo allocation/reservation trong thay đổi này.
