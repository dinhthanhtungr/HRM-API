# Manufacturing workflow

## Theo dõi lệnh sản xuất dành cho IMS

`GET /api/v1/plm/production-orders/tracking` và
`GET /api/v1/plm/production-orders/{id}/tracking` yêu cầu
`plm.production-order.tracking.view`. Scope gồm lệnh active, TP active, khách
active bên ngoài cùng công ty. Lệnh bán thành phẩm, khách nội bộ và lệnh thiếu
khách nằm ngoài phạm vi. List phân trang 25 dòng và tìm theo mã lệnh, mã/tên TP.

`requestedQuantity` là lượng yêu cầu của lệnh. `productionQuantity` là tổng
lượng sản xuất đã ghi trên lệnh, có thể là kế hoạch và không phải xác nhận đã
nhập thành phẩm. `null` nghĩa là chưa có giá trị. Trạng thái chi tiết ngoài
mới, hủy hoặc hoàn thành được gộp thành `InProgress`. Endpoint không trả công
thức, NVL, giá, mẻ, lô, ghi chú hoặc thông số sản xuất.

- [Bổ sung snapshot tên màu MFG còn NULL từ Product.ColourName](Commands/FillManufacturingColourNames/README.md): API xem trước/cập nhật riêng, không ghi đè snapshot đã có.

## Inform role/state access

Khóa sửa công thức dựa vào yêu cầu xuất kho thực tế, không dựa trạng thái MFG.
`OperationMaterialRequestLink` nối MFG/company tới `WarehouseRequest` bằng RequestId
(fallback RequestCode khi RequestId trống). Phiếu xuất chưa Cancelled khóa NVL, Std,
lot, loại hàng và totalQuantity/numOfBatches; phiếu đã Cancelled chỉ mở lại nếu chưa
có `TempStockIssueLedger` dương liên quan. Done/Finished không tự mở khóa. Open
reservation không phải yêu cầu xuất kho. Inform trả `formulaEditLockReason` và
`canEditItems=false`, bỏ hai field khối lượng/số mẻ khỏi editableFields khi khóa;
ghi chú/QC giữ quyền riêng. Các API tạo VA, xử lý VA, Information và nhập Excel
kiểm tra cùng rule tại BE. Lệnh NVL nội bộ cũng từ chối lưu/hủy khi đã khóa.
Mutation kiểm tra sau khi giữ khóa MFG trong transaction. Luồng tạo phiếu kho nằm
ngoài module này cần cùng giữ khóa MFG để loại trừ hoàn toàn tranh chấp tạo phiếu
đồng thời; không coi kiểm tra một lần ở FE là bảo đảm đồng thời.

PLPUUser được sửa `labNote` (Yêu cầu QA/sản xuất thực hiện) theo trạng thái/quyền quản lý lệnh;
`requirement` là Yêu cầu khách hàng và vẫn chỉ Developer được sửa.
PLPUUser không được đọc/tạo/sửa/tính thử/in phiếu chỉnh màu, kể cả token còn claim manage cũ.
Role này vẫn chọn TP đạt; chỉ Developer/QCUser/LabUser được chọn NVL lỗi hoặc TP lỗi.

New: chỉ PLPUUser/President/Developer có quyền quản lý MFG được sửa; LabUser chỉ đọc.
Sau New: LabUser có quyền quản lý VA chỉ sửa QC, ghi chú VA và NVL (Std/lot);
không sửa kế hoạch, bao bì, yêu cầu QA/sản xuất, yêu cầu khách hoặc ghi chú PLPU.
Tỷ lệ sử dụng/hạn dùng tiếp tục PATCH sample request theo quyền riêng, chỉ mở trên FE khi Inform editable.
Toggle chuẩn vẫn cần quyền quản lý VA/vật tư, VA thuộc lệnh và company scope.
Lab ở WaitingForStandardSample chỉ sửa nội dung kỹ thuật, không chuyển trạng thái MFG;
toggle chuẩn tại trạng thái này không active/chuyển VA. Các thao tác kế hoạch vẫn bị khóa chờ Sale.
Mọi grant còn yêu cầu capability hiện hành; role không vượt explicit permission denial.

Copy MFG trên FE là bản nháp chưa lưu, có thể sửa thông tin và NVL trước khi xác nhận. Chỉ nút Lưu tạo lệnh mới gọi `POST /production-orders/{id}/copy`; mở/sửa/hủy nháp không cấp mã hoặc tạo MFG/VA/lịch/reservation. API hiện có nhận optional `selectedFormulaId` và `items`, resolve vật tư/giá/category/lot tại BE và tạo toàn bộ dữ liệu trong transaction hiện hành, không cập nhật lệnh/VA nguồn. Payload `items` yêu cầu quyền xem vật tư cùng quyền quản lý MFG hoặc quản lý VA, ngoài quyền tạo MFG; chỉ xem không được sửa. Caller cũ bỏ `items` tiếp tục copy nguồn theo rule cũ. Không đổi schema/migration.

Inform trả `data.sampleInfo` chỉ đọc: `productId`, `sampleRequestId`, `externalId`, `usageRate` (%), `expiryType` (mã hạn sử dụng Product hiện hành). Nguồn là Product active của chính MFG, cùng company; mã mẫu lấy SampleRequest active cùng company mới nhất theo CreatedDate rồi ID. Product không hợp lệ trả null; thiếu dữ liệu giữ null. Không chứa công thức/giá, không tính ngày hết hạn hay tạo snapshot; vùng Thông tin mẫu trên FE nằm trước công thức và không gọi API riêng.

- [Inform editor: GET + POST action, trạng thái, concurrency và tương thích](EDITOR-CONTRACT.md).

`GET /api/v1/plm/production-orders/{id}` trả `data.poNo` (string/null) từ cùng đơn liên kết được chọn cho `merchandiseOrderId`. Áp dụng company scope/link/detail active hiện hành, đơn bán inactive trả PO null. FE dùng field chi tiết này để hiển thị PO, không tải riêng Sale Order; `data.poNo` ở Inform được giữ tương thích.

`GET /api/v1/plm/production-orders/{id}/inform` trả thêm `data.poNo` (string/null) cho nhãn số PO. Chỉ đọc từ liên kết MFG/detail/đơn bán active, cùng company và đúng `workspace.order.merchandiseOrderId`; không có liên kết hợp lệ trả null. FE không cần tải toàn bộ Sale Order chỉ để hiển thị PO.

- [Chỉnh màu: contract và đối chiếu legacy](Services/Adjustments/README.md): filter danh sách,
  cân/chỉnh mẻ, PDF preview và notification.

- [Thông báo ngày hứa giao theo mã MFG](Commands/NotifyPromisedDelivery/README.md): gửi cho sale/leader của ĐHG liên kết, không cập nhật ngày sản xuất.

Các API dưới đây đọc/ghi bảng Manufacturing hiện có. Ba bảng chỉnh màu và các
cột kiểm tra lệnh được mapping theo xác nhận của chủ hệ thống; không có migration
hay lệnh thay đổi schema. `F:\Backend` chỉ là nguồn tham khảo, không được ghi.

## Tổ chức code

Mỗi use case có thư mục riêng tại `Commands/<Action>` hoặc `Queries/<Action>`.
Command/query và handler nằm trong hai file riêng; model chỉ phục vụ use case
được đặt cùng thư mục. Không gộp list/detail hoặc nhiều handler trong một file.

Service dùng chung được nhóm theo trách nhiệm, namespace tương ứng đường dẫn:

| Thư mục | Trách nhiệm |
| --- | --- |
| `Services/Formulas` | Chọn nguồn, resolve input/giá/lot, đọc công thức, vật tư, lịch sử, snapshot/version và export rows |
| `Services/ProductionOrders` | Quy tắc action/trạng thái của lệnh và đọc tồn thành phẩm |
| `Services/Adjustments` | Điều chỉnh mẻ, range, tính lượng, mapping, recipient groups và tài liệu điều chỉnh |
| `Services/ColorChips` | Lưu, đọc và in color chip |
| `Services/Notifications` | Phát thông báo workflow |
| `Services/Access` | Kiểm tra current user/capability chung của Manufacturing |

Các partial của `ManufacturingAdjustmentService` cùng nằm trong
`Services/Adjustments`. Khi bổ sung logic chỉ dùng cho một handler, đặt gần
handler đó; chỉ đưa vào nhóm service khi có trách nhiệm dùng chung rõ ràng.

## Trạng thái chuyển đổi

[Đối chiếu legacy](LEGACY-MAPPING.md) phân biệt endpoint đã chạy với phần chưa
triển khai. DTO/entity/helper đã được đưa sang **không đồng nghĩa** endpoint đã
hoàn thành. Các luồng tạo lệnh trước đó nằm ở [ProductionOrders](../ProductionOrders/README.md).

## Scope và quyền

Lỗi đối chiếu lot khi lưu công thức trả message bắt đầu bằng
`[formulaItem:{itemId}:{itemType}:{URI-encoded lot}]`, tiếp theo là dòng NVL theo
thứ tự request, mã TP/NVL, mã kho/màu, lot, Hàng đạt/lỗi và lý do.
FE dùng metadata để tìm đúng dòng (không coi số dòng request là index của form),
ẩn prefix kỹ thuật và cuộn đến lot sai. Không thay đổi rule validation hoặc scope kho.

### Dán công thức từ Excel (Developer)

`POST /api/v1/plm/production-orders/{id}/excel-formula/resolve` chỉ đối chiếu,
không ghi DB, không giữ chỗ hay tạo VA. Request:
`{ "rows": [{ "colourCode": "BLUE", "name": "Tên tham khảo", "lotNo": "VA1", "quality": "TP lỗi" }] }`.
Giới hạn 1–200 dòng, 4 cột bắt buộc, tối đa 500 ký tự/cột; không cần kg sản xuất.
Developer được kiểm tra qua `ApplicationRoleSets.PLM.ProductionExcelImporters`,
đồng thời cần manage MFG, xem vật tư/hàng lỗi, company/employee hợp lệ,
lệnh active cùng company, liên kết đơn bán active và trạng thái cho sửa công thức.
Không mở thêm quyền cho President/PLPU/Lab hoặc đổi role claims.

Response thành công: `{ "success": true, "data": [{ "row": 1, "error": null,
"item": { "itemId": "...", "itemType": 3, "categoryId": "...", "code": "TP_...",
"name": "[BLUE] Tên hiện hành", "stockCode": "BLUE", "unit": "kg", "lotNo": "VA1",
"stockType": 2, "stockKg": 100, "percent": 100 } }] }`.
`stockKg` là tổng kg thực tế trên mọi kệ active của đúng mã màu/lot/loại hàng được warehouse
visibility cho phép (không phải số từ Excel, không tự trừ reservation tổng mã).
`percent = stockKg / tổng stockKg các dòng dán * 100`, cắt ở 10 chữ số thập phân;
dòng cuối nhận phần bù để tổng chính xác 100. Chỉ tính khi tất cả dòng hợp lệ;
có dòng lỗi thì percent tạm là 0 và FE không thêm dòng nào.
Std hiển thị bằng percent/100; các dòng có sẵn không nằm trong mẫu số, giữ nguyên.
Tên/mã dùng FormulaItemDisplayResolver;
không tin tên Excel, không trả giá. Dòng lỗi có `item=null` và error code:
InvalidQuality, ProductNotFound, AmbiguousProduct, NoStock, DuplicateItemLot,
InvalidProduct. Không tự đổi lot/quality hay chọn đại sản phẩm trùng mã.
HTTP 400 cho request/quyền/scope/state không hợp lệ; lỗi theo dòng vẫn HTTP 200
để FE hiển thị toàn bộ lỗi và chưa thêm dòng nào. HTTP 401 khi chưa đăng nhập.
FE thêm bản nháp, chỉ POST actions Save/SaveAndCreateFormula mới persist;
BE vẫn resolve giá/lot/vật tư và kiểm tra revision tại thời điểm lưu.

Tất cả endpoint mới có `[Authorize]`; handler kiểm tra capability và CompanyId
của current user trước khi đọc dữ liệu. Không nhận CompanyId từ FE. Mutation còn
yêu cầu EmployeeId để audit; không dùng identity UserId thay EmployeeId.

| Capability | Fallback role |
| --- | --- |
| `plm.production-order.view` | Admin, Developer, President, PLPUUser, QLSXUser, ManufactureUser, LabUser, LabAdmin, QCUser |
| `plm.production-order.check` | Admin, Developer, President, PLPUUser, QLSXUser, QCUser |
| `plm.production-order.manage` | Admin, Developer, President, PLPUUser, QLSXUser, ManufactureUser |
| `plm.manufacturing-formula.manage` | Developer, LabUser |
| `plm.production-order.requirement.edit` | Developer |
| `plm.manufacturing-adjustment.manage` | Admin, Developer, President, QLSXUser, QCUser, LabUser |
| `plm.manufacturing-color-chip.manage` | Admin, Developer, President, PLPUUser, QLSXUser, QCUser, LabUser |

Đọc/ghi chỉnh màu còn yêu cầu `plm.formula-material.view` vì kết quả chứa công
thức vật tư. Có quyền chỉnh màu riêng chưa đủ để xem công thức. Color-chip chỉ
chứa form kỹ thuật, không cấp quyền đọc vật tư hay giá.

Các command nhận đầu vào cơ bản mới được mô tả tại [Server workflow](SERVER-WORKFLOW.md).
Mapping thay lời gọi và nghiệp vụ FE nằm tại [FE migration](FE-MIGRATION.md).
Giữ ma trận quyền hiện tại: PLPUUser không tự có quyền xem vật tư/cost;
người cần dùng được cấp claim vật tư riêng, không tự cấp claim giá.

Đây là mapping mới cho API có bảo vệ; controller MFG legacy cho anonymous nên
không có ma trận phân quyền an toàn để sao chép nguyên trạng. Claim DB trong
`AspNetRoleClaims` là nguồn quyền khi token có explicit permission set. Fallback,
catalog và baseline seed/rollback đã đồng bộ. Baseline SQL chỉ được cập nhật,
**chưa chạy vào DB**; tài khoản dùng claim DB cần được cấp quyền tương ứng và
login/refresh token sau khi thay đổi claim.

Quyền xem lệnh không cấp quyền xem giá: `UnitPriceAgreed` trong chi tiết chỉ được
query/trả khi `IPricingVisibilityService.GetAccess().CanViewApprovedSellingPrice`
cho phép (`pricing.approved-selling-price.view`); trường này là `null` khi bị che.

## Danh sách

`GET /api/v1/plm/production-orders`

Query: `pageNumber` mặc định 1, `pageSize` mặc định 15/tối đa 100, `keyword`,
`mfgProductionOrderId`, `statuses`, `from`, `to`, `isLastUpdate`.
Ngày lọc là **CreatedDate của lệnh**, bao gồm cả hai đầu. Khoảng ngày đảo ngược
bị từ chối. `isLastUpdate=true` sắp UpdatedDate rồi CreatedDate giảm dần;
mặc định chỉ CreatedDate giảm dần. Không thêm filter CustomerId/ProductId mà
legacy khai báo nhưng không thực thi. FE không gửi hai field đó.

Keyword tìm mã/tên snapshot, màu, khách, VU snapshot, VA qua PSV, mã đơn hàng và
Sample Request active cùng công ty. Mã Formula hiện tại của Product cũng được
tìm theo ILike theo contract PLM.

```json
{
  "success": true,
  "data": {
    "items": [{
      "mfgProductionOrderId": "00000000-0000-0000-0000-000000000001",
      "externalId": "MFG261000001",
      "mfgFormualaExternalIdSnapshot": "VA261000001",
      "merchandiseOrderId": "00000000-0000-0000-0000-000000000000",
      "merchandiseOrderExternalId": null,
      "requestedQuantity": 1000,
      "totalQuantity": null,
      "isPrintedStock": false,
      "checkName": "",
      "checkedDate": null,
      "mfgFormulaHistories": []
    }],
    "totalCount": 1, "pageNumber": 1, "pageSize": 15, "totalPages": 1
  }
}
```

Mã/tên sản phẩm và khách là snapshot đã lưu trên MFG. Tên field
`mfgFormualaExternalIdSnapshot` giữ lỗi chính tả của DTO cũ để dễ đối chiếu, nhưng
giá trị lấy từ **VA đang chọn** qua PSV `ValidTo == null`, ưu tiên ValidFrom mới
nhất; không phải cột snapshot trên MFG. Không bắt `ValidFrom != null` như endpoint
kiểm tra Product đang sản xuất. Đơn hàng lấy liên kết cùng company có GUID đơn
hàng lớn nhất như legacy; summary không lọc link/detail active, detail có lọc.
Không có liên kết: ID Guid.Empty, mã null.

`checkName` là tên Employee cùng company, rỗng nếu chưa có checker/quan hệ không
hợp lệ. `checkedDate` là lúc kiểm tra, không phải lúc cập nhật status. Không có
VA lịch sử thì collection rỗng. Mỗi history là một PSV, không chống trùng VA;
CreatedDate = ValidFrom, fallback CreatedDate của VA.

History lượng được gom **theo mã VA**, không phân chia theo MFG, đúng legacy.
Total = sum(WeightStockedKg ?? NetWeight ?? 0). Good = cùng lượng khi header
ProductStatus=Reported hoặc detail ProductType=`Thanh pham`; Error = cùng lượng
khi header ProductStatus=Error/NG. Good và Error có thể chồng điều kiện, không
giả định Total=Good+Error. Header và detail đều phải có mã nằm trong tập VA của
trang; chúng không bắt buộc cùng mã với nhau, giữ predicate thực tế của legacy.
Shift-report không có CompanyId, nên bổ sung scope qua Employee của Operator;
bản ghi không xác định được Operator trong công ty không được cộng để tránh đọc
chéo công ty. Không tìm được lượng thì trả 0, không phải null.

## Chi tiết

Lot trong `/inform` chỉ lấy kho hiện tại hoặc Lot lưu trên VA của chính lệnh; nguồn VA tham khảo không mang Lot cũ sang. Quyền xem hàng lỗi, FIFO và N/A đạt được mô tả tại [contract Lot](Services/Formulas/README.md#lot-trong-inform-và-nguồn-công-thức).

Workspace và `/inform` trả `formulaOptions[].plpuNote`, `labNote`, `requirement` theo lệnh nguồn bên cạnh `note` công thức. Nguồn dữ liệu và semantics từng origin: [contract ghi chú option](Services/Formulas/README.md).

`GET /api/v1/plm/production-orders/{id}`

Trả `OperationResult<GetMfgProductionOrder>`: thông tin snapshot của lệnh, VU
khách chọn từ FormulaId/FormulaExternalIdSnapshot, VA đang chọn từ PSV, notes,
ngày sản xuất/dự kiến/yêu cầu, lượng yêu cầu/lượng sản xuất/batches, bag, QC, step.
Sample Request code lấy bản active cùng company mới nhất của Product.
VA đang chọn ưu tiên ValidFrom rồi PSV ID giảm dần; code chỉ lấy VA active cùng
company. Không có chọn thì ID/code null. Có hơn một PSV mở của lệnh thì trả
`success=false` như hành vi thực tế của service legacy, không tự sửa dữ liệu.
`totalQuantity=null` nghĩa chưa có lượng sản xuất; không thay thành 0. Giá bị che
khác với giá bằng 0: `unitPriceAgreed=null` khi thiếu quyền giá.

## Kiểm tra công thức đã lưu

`PATCH /api/v1/plm/production-orders/{id}/check`

```json
{ "isPrintedStock": true }
```

Value true/false cập nhật cờ; không gửi mặc định false như DTO cũ. Explicit null
bị từ chối vì cột is_printed_stock non-nullable; legacy cũng không lưu được null.
Handler mới từ chối trước SaveChanges thay vì để DB báo lỗi. Mọi lần gọi hợp
lệ đều ghi Checker=current EmployeeId và CheckedDate=clock.Now, kể cả cờ không
đổi. Không đổi status, không viết timeline, không cập nhật UpdatedBy/UpdatedDate
của MFG, đúng legacy. Đây là hành vi check, không phải duyệt chất lượng.

## Cập nhật thông tin

`PATCH /api/v1/plm/production-orders/{id}/information`

```json
{ "stepOfProduct": null, "totalQuantity": 1000, "numOfBatches": 10,
  "status": "Scheduling", "expectedDate": "2026-10-08" }
```

Field áp dụng: StepOfProduct, ExpectedDate, TotalQuantity, NumOfBatches, Status,
LabNote, Requirement, PlpuNote, QcCheck. Những field khác legacy nhận nhưng
không áp dụng (ManufacturingDate, RequiredDate, IsActive, ManufacturingFormulaId)
không nằm trong request mới. Status/step phải là giá trị enum hợp lệ.

StepOfProduct, ExpectedDate, TotalQuantity và NumOfBatches **bắt buộc có trong
JSON**, nullable; null được hiểu là xóa. Legacy luôn gán bốn field này ngay cả
khi client bỏ quên do SetIfNullable cho phép null; contract mới bắt presence
để tránh vô tình xóa khi PATCH. FE gửi giá trị hiện tại để giữ nguyên.
Với các field chuỗi: không gửi hoặc null thì không đổi; chuỗi rỗng là value mới
được lưu, không trim. Status null/không gửi không đổi.

Cần liên kết MfgOrderPO active trong company; lệnh nội bộ không có link bị từ
chối như legacy. Mỗi lần hợp lệ ghi audit MFG. Khi status thay đổi: ghi timeline;
khi chuyển Scheduling và chưa có schedule: tạo schedule, note = LabNote ??
PlpuNote, giữ schedule StepOfProduct null. Có schedule thì cập nhật DeliveryPlanDate
từ ExpectedDate. Không tự tạo phiếu xuất hay giữ thêm tồn ở luồng này.
Status thay đổi còn đưa đơn hàng New/Approved sang Processing; status không
parse được coi là New như legacy. Các trạng thái đơn hàng khác giữ nguyên.
MFG, schedule, đơn hàng và timeline lưu trong cùng transaction.

Thay đổi ExpectedDate có ngày mới khác ngày cũ phát thông báo ngày hứa giao topic 55 qua publisher chung với API notify-promised-delivery: giữ đúng Sale phụ trách/leader nhóm và payload ngày cũ/mới. So sánh chỉ phần ngày; ngày mới null hoặc không đổi không phát. Áp dụng cả information/kế hoạch, inform/công thức, tạo VA trên lệnh đã có và dùng hàng tồn. Publisher chạy trước commit trong cùng transaction; lỗi publish rollback. Không có liên kết/người nhận active thì không tạo thông báo và vẫn lưu chỉnh sửa hợp lệ. Xem [contract publisher](Services/Notifications/README.md).

## Kết thúc lệnh

`PATCH /api/v1/plm/production-orders/{id}/finish` không cần body.

Cần link MfgOrderPO active, MFG active và cùng company. MFG đã Finished thì trả
thành công, không save/log lại. Nếu chưa: ghi MFG Finished/audit, đưa schedule
hiện có sang Finished, viết timeline và commit cùng transaction. Không tạo
schedule mới, không đổi đơn hàng, không xuất/nhập kho hay tính điều chỉnh QC.
Giữ kiểm tra chống log từ job legacy với note chính xác
`Ket thuc lenh san xuat automation 25/03/2026`; không đổi thành kiểm tra mọi event
Finished vì sẽ làm khác nghiệp vụ cũ.

## Verification

Npgsql model test kiểm tra đúng tên bảng/cột, foreign key không sinh shadow,
graph chỉnh màu. Regression kiểm tra quyền, company, che giá, nhiều PSV mở,
checker, Scheduling/Processing, finish idempotent, transaction rollback và công
thức tính lượng chỉnh màu. Chưa thay thế integration với dữ liệu PostgreSQL
thật; không có truy cập/ghi DB production trong quá trình port.

### Tự kích hoạt VA khi lưu Scheduling
Mọi đường lưu trạng thái Scheduling qua editor/Inform và API information cũ đều tự đảm bảo VA đang chọn có ValidFrom. BE tải tracking các PSV mở (ValidTo=null), VA active cùng company: nếu có một lựa chọn đã hiệu lực thì giữ nguyên; nếu chưa có và chỉ có một VA chờ thì gán ValidFrom bằng thời điểm xử lý từ clock. Không sửa ngày đã có, không kích hoạt thêm các VA chờ khi đã có VA hiệu lực. Thiếu VA hoặc nhiều lựa chọn không xác định thì fail trong transaction, không báo lưu thành công.
Gán ngày cùng transaction với đổi trạng thái, lịch, giữ chỗ và audit. Sau SaveChanges, trước publish/commit, Inform/information đọc lại để bảo đảm có lựa chọn VA hiệu lực active cùng company; không đạt thì trả lỗi và transaction không commit. Nhánh tạo VU lần đầu và sao chép dùng CreateSelection có ValidFrom=now từ trước. Quyền và điều kiện chuyển trạng thái giữ nguyên; tạo VA chờ vẫn có thể ValidFrom=null nếu chưa chuyển Scheduling. Không backfill DB cũ; API không nhận ngày hiệu lực từ FE cho thao tác này.
