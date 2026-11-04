# Báo cáo PLPU

Báo cáo mua hàng và giao hàng PLPU được chuyển từ `ReportFeatures/Services/PLPUReports`
và `Repositories/ReportFeatures/PLPUReports` của VietausWebAPI. Các phép tính legacy
được giữ nguyên để đối soát số liệu 1–1 trên cùng dữ liệu, company, filter và ngày chạy.
Không thêm bảng/cột, không ghi dữ liệu khi xem báo cáo hoặc xuất Excel.

## API

Base route: `/api/v1/reports/plpu`. Tất cả là GET và yêu cầu đăng nhập, company hiện tại
và capability đọc `reports.plpu.view`. Chỉ role PLPUUser, President, Developer được xem; quyền quản lý PO không còn thay thế quyền đọc báo cáo.
Mapping runtime dùng permission claim; fallback tập trung tại `ApplicationPermissionRoleSets`
(Developer, President, PLPUUser). SaleUser/QCUser không được mở báo cáo chỉ nhờ role đó.
Token explicit permission set phải có claim reports.plpu.view và thuộc một trong ba role được phép; stale claim ở role Admin/Purchaser không mở được report. PLPUUser được xem report (bao gồm giá/cost trong report, drawer, chi tiết và finish Excel) và được quản lý PO theo role qua contract Purchasing hiện hành. Hai capability vẫn độc lập; quyền mua không tự thay thế reports.plpu.view. FE export không yêu cầu quyền Developer của route /dev. Với DB đã triển khai, chạy `20261015_SeedPlpuReportView.sql` (permission data, không đổi schema), sau đó đăng nhập lại/refresh token. Script chưa được chạy vào DB bởi thay đổi code này.

| Route | Response | Nguồn tương đương |
| --- | --- | --- |
| `/header` | `PLPUPurchaseOverviewSummaryDto` | Phần summary của `GetHeaderReportAsync` |
| `/body` | `PLPUPurchaseBodyDto` | Tám collection biểu đồ của `GetHeaderReportAsync` |
| `/orders` | Page của `PLPUPurchaseOrderRowDto` | `GetRowsAsync` |
| `/orders/{id}` | Header của một PO, Lines, Receipts, QcRows | `GetOrderDetailAsync` |
| `/lines` | Page của `PLPUPurchaseOrderDetailReportDto` | `GetDetailLinesAsync` |
| `/receipts` | Page của `PLPUPurchaseReceiptReportDto` | `GetReceiptsAsync` |
| `/qc` | Page của `PLPUPurchaseQcReportDto` | `GetQcAsync` |
| `/suppliers` | Danh sách hiệu suất và lý do rủi ro NCC | `GetSupplierPerformanceAsync` |
| `/finish` | Danh sách `FinishRow` | `GetFinishReportAsync` |
| `/finish/export` | Excel 18 cột, sheet Giao hàng hoàn tất | `ExportFinishReportExcel` |
| `/drilldown?metric=...` | Page PO/dòng đóng góp vào KPI, kèm tổng đối soát | Cùng allocation, detail/header builder và summary của `/header` |

Chưa đăng nhập trả 401; thiếu company/capability hoặc yêu cầu company khác trả 403.
PO không thuộc company hoặc không thuộc bộ lọc report trả 404 ở `/orders/{id}`.
ID rỗng, khoảng ngày đảo ngược, ngày To không thể cộng một ngày hoặc page gây overflow trả 400.
Handler cũng kiểm tra capability/company, không chỉ dựa vào controller.
Company được áp dụng lên PO, request/voucher/ledger kho, đơn bán, giao hàng, LSX và EventLog.
Join vật tư/NCC chỉ đọc bản ghi cùng company; liên kết legacy trỏ nhầm company không làm lộ tên/cost ngoài scope.
Giá, cost snapshot và thông tin khách/NCC chỉ được trả sau khi qua capability này;
không có chế độ mở report rồi che một vài field.

## Filter và phân trang

Các API mua hàng dùng `From`, `To`, `CompanyId`, `PurchaseOrderId`, `SupplierId`,
`MaterialId`, `OrderType`, `Status`, `Keyword`, `IncludeInactive`, `OnlyNeedAction`,
`PageNumber`, `PageSize`. CompanyId bỏ trống được gán current company; không được mở rộng scope.
From/To lọc **ngày tạo PO**, bỏ giờ, To inclusive; bỏ trống không giới hạn đầu/cuối kỳ.
Receipt/QC của các PO đó lấy toàn bộ lịch sử liên quan, không giới hạn ngày nhập bằng From/To.
Keyword giữ phép Contains và phân biệt hoa/thường như nguồn.

Đơn nguồn luôn OrderType=`NCC`, vật tư snapshot bắt đầu `NVL` không phân biệt hoa/thường,
loại mã chứa `VPP` hoặc `BB`; loại PO có ExternalId bắt đầu `PO`. Truyền OrderType khác
NCC vẫn trả tập rỗng, như nguồn. Chỉ dòng PO active; mặc định PO active và request kho active.
`IncludeInactive=true` giữ cách lấy inactive của nguồn; không bỏ company/capability.

`OnlyNeedAction` chỉ lọc `/orders`, `/lines`; không thay đổi tổng quan, biểu đồ,
receipts, QC hoặc hiệu suất NCC, tương đương các method nguồn riêng biệt.
Detail một PO tự IncludeInactive=true và vẫn áp dụng bộ lọc mã/loại ở trên.

Page giữ schema nguồn: `items`, `totalCount`, `page`, `pageSize`, `totalPages`.
PageNumber <= 0 về 1; PageSize <= 0 về 15; không dùng giới hạn 100 của pagination module khác.
Sắp xếp, phân trang in-memory sau tính toán giữ nguyên nguồn để không đổi allocation và thứ tự.
Đây chưa phải tối ưu tải dashboard; mỗi API có thể đọc toàn bộ cohort PO/receipt theo filter.

## Header và body

`/header` bổ sung object `accounting` cho toàn bộ PO trong view kế toán, xem mục
[Header đối chiếu kế toán](#header-đối-chiếu-kế-toán). Các field NVL ở cấp ngoài giữ nguyên.

Ví dụ rút gọn, các field khác vẫn có trong DTO:

```json
{
  "totalPO": 1,
  "completedPO": 0,
  "needActionPO": 1,
  "totalPurchaseValue": 10000,
  "receivedPurchaseValue": 8000,
  "orderedQuantity": 100,
  "warehouseReceivedQuantity": 80,
  "acceptedQuantity": 60,
  "pendingQcQuantity": 20,
  "rejectedQuantity": 0,
  "remainingToAccept": 40,
  "completionPercent": 60
}
```

Header là tổng quan độc lập pagination và OnlyNeedAction; không chứa danh sách PO/biểu đồ.
Số lượng và tỷ lệ là kết quả tính realtime, không cập nhật trạng thái PO đã lưu.
`POStatus` ở detail/list là trạng thái persisted; `ReportStatus`/`LineReportStatus` là trạng thái tính toán.
Các status/flags/action giữ nhãn tiếng Việt của nguồn. Không dùng các nhãn này để quyết định quyền.
`PLPUComment`, `Comment`, tên/mã vật tư lấy snapshot/cột nguồn đúng projection legacy.

Body có `purchaseValueByMonth`, `quantityByMonth`, `purchaseValueBySupplier`,
`quantityBySupplier`, `quantityByMaterial`, `qcResultBySupplier`, `statusDistribution`,
`priceVarianceByMaterial`. Mỗi chart point là một tháng/NCC/vật tư/trạng thái, gồm label/id,
lượng, giá trị và tỷ lệ liên quan. Tháng dựa trên ngày tạo PO, không phải ngày nhập
kho thực tế. Riêng `/body`, hai chuỗi tháng trả tối thiểu 6 tháng liên tục kết thúc
tại tháng To (bỏ To thì dùng tháng hiện tại). Khoảng chọn dài hơn 6 tháng trả toàn
bộ các tháng từ tháng From đến tháng To. Ví dụ chọn 10/2026 trả 05–10/2026;
chọn 01–12/2026 trả 12 tháng. Query riêng cho chuỗi tháng lấy trọn tháng, giữ
company/capability và các filter NCC/vật tư/PO/trạng thái như request. Tháng không
có PO trả điểm số liệu 0, PeriodStart/PeriodEnd và Label yyyy-MM. Header và các
biểu đồ khác vẫn giữ From/To gốc; tổng chuỗi tháng có thể khác tổng header.
Supplier chart lấy top 15 theo giá trị mua trước; quantity/QC supplier chart tiếp tục dùng tập này.
Material chart lấy top 20; variance chart lấy top 20 theo trị tuyệt đối ảnh hưởng tiền.
Vì có top N, không coi tổng các chart point là toàn bộ tổng header.

## Nguồn lượng, giá, ngày và công thức legacy

- Nhập kho = tổng `WarehouseShelfLedger.DeltaKg > 0` cho phiếu ImportRawMaterial/Other/Material.
  Không thay bằng RealQuantity của PO. Liên kết qua mã PO → codeFromRequest → voucher/detail/ledger.
- Lấy QC mới nhất theo CreatedDate trên dòng phiếu; thiếu quyết định thì fallback VoucherDetailType.
  QCPass/Special đạt; QCFail lỗi; Waiter/null chờ. Không bổ sung tie-break mới cho QC trùng thời điểm.
- Gom receipt cùng PO/dòng phiếu/mã hàng/lô/quyết định; ngày nhóm là ngày nhập đầu tiên.
  Phân bổ theo LineNo rồi PurchaseOrderDetailId; lượng vượt đẩy vào dòng cuối cùng cùng PO/mã vật tư.
  Accepted/Pending/Rejected được chia tỷ lệ và làm tròn 6 chữ số như nguồn.
- Category bao bì `3bed94ed-da05-4e5f-ac04-c7647aaa63d6` có CreatedDate được auto-accept toàn bộ
  lượng nhập như nguồn, kể cả QC khác. Không tự thêm cutoff mặc dù comment legacy nhắc cutoff.
- Thành tiền đặt = TotalPriceAgreed, fallback lượng đặt × (UnitPriceAgreed hoặc 0).
  Thành tiền nhập = lượng nhập × đơn giá effective, làm tròn 2 chữ số AwayFromZero.
  Effective price ưu tiên UnitPriceAgreed, rồi TotalPriceAgreed/lượng đặt > 0, rồi BaseCostSnapshot hoặc 0.
  Không đổi sang fallback giá của Purchasing Excel vì có thể làm lệch số liệu nguồn.
- PriceVariance = UnitPriceAgreed − BaseCostSnapshot khi cả hai có giá trị, ngược lại null.
  PriceVariancePercent null khi thiếu giá gốc hoặc giá gốc = 0. PriceImpactAmount = variance × lượng đặt.
- Đủ hàng khi đạt ít nhất 98% lượng đặt; giao vượt khi >102%. Tổng còn thiếu được tính từ tổng lượng
  cùng cấp, không đổi thành tổng thiếu của từng dòng. Phần trăm chia mẫu số 0 trả 0, làm tròn 2 số.
- Ngày yêu cầu ưu tiên DeliveryDate của dòng rồi RequestDeliveryDate của header.
  GetDateWhenEnough vẫn yêu cầu cộng dồn 100%; ngày accepted-enough vẫn dùng ReceiptDate, không dùng ngày QC.
  Số ngày trễ/tuổi chờ QC dùng DateTime.Today theo nguồn. Đây là trạng thái tại ngày chạy,
  không phải snapshot tại To; phải chạy hai hệ thống cùng ngày/múi giờ để đối soát.
- OTD chỉ xét dòng có ngày yêu cầu và đã có ngày đủ 100%. Risk score cộng: overdue PO 30,
  QC fail dòng 25, shortage dòng 20, pending QC dòng 10, overdelivery dòng 5; giữ reason rows nguồn.
- Null ngày = chưa có ngày nguồn/receipt; null giá = không có snapshot dùng cho chỉ số đó.
  0 lượng/tỷ lệ = phép tính không có lượng hoặc mẫu số 0; chuỗi rỗng = thiếu tên/mã/ghi chú nguồn.
  Tập rỗng trả page/list rỗng và KPI 0, không tự tạo PO hoặc QC.

Không sửa các khác biệt legacy về ngưỡng 98%/ngày đủ 100%, auto-accept bao bì,
fallback giá, bù dư/thiếu giữa dòng hoặc mutation do dịch nhãn trong GetOverviewAsync.
API mới không gọi overview gộp; header/body tương đương method header riêng ở nguồn.

## Finish và Excel

From/To bắt buộc. Giữ filter `MerchandiseOrderDetail.DeliveryRequestDate >= From && < To.AddDays(1)`
**có cả phần giờ** như nguồn. Không đổi thành ngày giao thực tế, ngày tạo PO hay ngày Finished.
Loại đơn bán inactive, Cancelled, khách nội bộ KH_VIETAUS; dòng giao và phiếu giao active.
Mỗi row là kết quả join dòng giao × liên kết LSX; không tự gộp/deduplicate khi nhiều LSX.
Không thêm điều kiện đã giao đủ hoặc trạng thái hoàn tất dù tên legacy là báo cáo hoàn tất.

Ngày nhận đơn = MfgProductionOrder.CreatedDate; ngày dự kiến = ExpectedDate;
ngày giao thực tế = DeliveryOrder.CreatedDate; ngày sản xuất hoàn thành = EventLog Finished active đầu tiên.
LateDays JSON = ngày thực giao − ngày yêu cầu của dòng, có thể âm.
Excel giữ nguyên 18 cột, thứ tự, format, ghi chú và summary nguồn; cột trễ trong Excel dùng ngày dự kiến.
Summary Excel group theo mã đơn bán, so ngày dự kiến nhỏ nhất với ngày thực giao đầu tiên;
thiếu ngày được coi đạt. Không sửa thành giao đủ/lần giao cuối trong lần chuyển này.

## Verification

`HRM.Application.Tests/Features/Reports/PLPU/Reference` chứa snapshot implementation nguồn,
chỉ điều chỉnh namespace, persistence interface và enum string để compile trên domain đích.
Không tham chiếu hoặc chạy build trong F:/Backend.
Tests so sánh toàn bộ JSON của các method với snapshot trên cùng fixture: empty, pending/fail/pass/special,
trùng mã vật tư, phân bổ, thiếu/giao vượt, 98/100/102%, giá thiếu, inactive/closed, OnlyNeedAction và page.
Header/body kiểm tra đối chiếu với cùng dashboard nguồn. Finish/Excel so row và cell/style/merge;
SQL ToQueryString kiểm tra PostgreSQL translate và các bộ lọc legacy/company.
Security tests kiểm tra role được/không được quyền, explicit permission thiếu claim, unauthenticated,
company khác và input không hợp lệ không đọc dữ liệu.

Các test fixture và SQL translation không chứng minh DB production hai bên giống nhau.
Đối soát cuối trên cùng bản dữ liệu PostgreSQL: cùng company/filter/ngày chạy, so từng KPI, row ID,
lượng, tiền, ngày, pagination và Excel. Khác biệt có chủ đích duy nhất về phạm vi là bổ sung
authorization/company scope; không so response anonymous toàn công ty của nguồn với một company đích.

## Drawer KPI (drill-down)

Ví dụ:

```text
GET /api/v1/reports/plpu/drilldown?metric=completedPo&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
GET /api/v1/reports/plpu/drilldown?metric=pendingQcQuantity&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
```

Không thay contract `/orders` hoặc các API cũ. Endpoint dùng cùng capability, company scope,
bộ lọc mã/loại, ngày tạo PO và dữ liệu realtime như header. FE truyền **cùng filter** với request
header. `OnlyNeedAction` được bỏ qua như header; muốn drawer PO cần xử lý dùng `metric=needActionPo`.
Metric bắt buộc, phân biệt hoa/thường; metric lạ/thiếu trả 400 trước khi đọc DB.

| Metric | KPI đối soát | RowType |
| --- | --- | --- |
| `totalPo` | totalPO | order |
| `activePo` | activePO | order |
| `cancelledPo` | cancelledPO | order |
| `closedPo` | closedPO | order |
| `completedPo` | completedPO | order |
| `incompletePo` | totalPO − completedPO | order |
| `needActionPo` | needActionPO | order |
| `overduePo` | overduePO | order |
| `qcFailPo` | qcFailPO | order |
| `pendingQcPo` | pendingQCPO | order |
| `overDeliveryPo` | overDeliveryPO | order |
| `orderedQuantity` | orderedQuantity | line |
| `warehouseReceivedQuantity` | warehouseReceivedQuantity | line |
| `acceptedQuantity` | acceptedQuantity | line |
| `pendingQcQuantity` | pendingQcQuantity | line |
| `rejectedQuantity` | rejectedQuantity | line |
| `totalPurchaseValue` | totalPurchaseValue | line |
| `receivedPurchaseValue` | receivedPurchaseValue | line |

`incompletePo` là phần bù của completed trong cohort, gồm cả các trạng thái huỷ/đóng nếu filter
đang bao gồm chúng; không dùng trạng thái persisted để tự suy ra completed. Cảnh báo PO có thể
giao nhau: một PO vừa quá hạn vừa có QC lỗi, nên không cộng các nhóm cảnh báo để tính tổng PO.

Response rút gọn:

```json
{
  "companyId": "f54b3c96-4faa-43d1-8446-9d98c459c630",
  "metric": "pendingQcQuantity",
  "rowType": "line",
  "metricValue": 8850,
  "pageValue": 500,
  "totalCount": 7,
  "page": 1,
  "pageSize": 1,
  "totalPages": 7,
  "items": [{
    "contribution": 500,
    "reportStatusCode": "WaitingQC",
    "lineStatusCode": "PartiallyDelivered",
    "order": { "poExternalId": "DDH261000020", "supplierName": "Nhà cung cấp", "pendingQcQuantity": 1000 },
    "line": { "lineNo": 1, "materialCode": "NVL_001", "pendingQcQuantity": 500 }
  }]
}
```

- `metricValue` là **toàn bộ giá trị KPI** cùng cohort, không phụ thuộc trang; lấy từ BuildSummary.
- `totalCount` là số PO đối với order hoặc số dòng có đóng góp khác 0 đối với line; **không phải lượng hàng**.
- `contribution`: mỗi PO đóng góp 1; mỗi line đóng góp đúng lượng/tiền của metric.
  Cộng contribution của tất cả trang bằng metricValue. Giữ đóng góp âm nếu dữ liệu legacy có giá/lượng âm.
- `pageValue`: tổng contribution trang hiện tại, tránh nhầm tổng trang với tổng KPI.
- `order`: thông tin PO/NCC, trạng thái, lượng đặt/nhập/QC, tiến độ, ngày và cảnh báo để dựng card.
  Các con số trong order là **tổng PO**, có thể xuất hiện lặp ở nhiều line; không cộng chúng để đối soát metric dòng.
- `line`: dòng vật tư và các mốc ngày/lượng/giá tương ứng; null với rowType=order.
- `reportStatusCode`/`lineStatusCode` là mã ổn định trước khi dịch nhãn; FE dùng để map badge.
  Các field status/flags/action bên trong order/line giữ nhãn tiếng Việt tương thích nguồn.
- Không có dữ liệu: items rỗng, totalCount/metricValue/pageValue/totalPages bằng 0.
  Qua trang cuối vẫn trả metricValue/totalCount toàn tập, pageValue=0 và items rỗng.
- Pagination mặc định và normalization như các API PLPU; sắp xếp có ID làm tie-break để ổn định giữa các trang.
  Dữ liệu là realtime, không có snapshot xuyên nhiều request nếu PO/kho/QC thay đổi trong lúc phân trang.

Các tỷ lệ, ngày trễ và remaining tổng có tolerance/bù dư thiếu **chưa hỗ trợ metric drill-down**.
Không lấy tổng thiếu từng dòng thay KPI thiếu tổng. FE chỉ gắn hành động drawer cho các metric trong bảng.
Trong drawer dùng purchaseOrderId mở `/orders/{id}`; API đích vẫn kiểm tra scope/capability độc lập.

Tests đối soát đủ 18 metric với summary, cộng contribution qua nhiều trang, không trùng ID,
QC lượng khác số PO, localize PO dùng chung một lần, page vượt cuối, empty cohort, unknown metric,
company khác và capability thiếu. Tests parity legacy tiếp tục kiểm tra header cũ không đổi.

## Drawer biểu đồ Phân bố trạng thái PO

`GET /api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=Completed&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20`

`ReportStatus` là query parameter tùy chọn **chỉ hỗ trợ Metric=totalPo**, phân biệt hoa/thường.
Không truyền giữ nguyên semantics metric cũ. Truyền rỗng, nhãn tiếng Việt, code dòng hàng hoặc metric khác
trả 400 với thông báo `Invalid ReportStatus...` hoặc `ReportStatus is supported only with Metric=totalPo.`
Không bỏ qua filter. 401/403 giữ nguyên; auth được kiểm tra trước validation và truy vấn.
`Status` hiện có vẫn lọc trạng thái PO persist trong DB; `ReportStatus` lọc trạng thái chính tính toán.

Các code dưới đây lấy từ ResolveHeaderPrimaryStatus, chính là code trước khi BuildStatusChart dịch Label;
không dùng cờ HasQCFail/IsOverdue thay thế nhóm chính. Header có thể Completed nhưng vẫn có cờ QC fail/quá hạn.
Danh sách hợp lệ và nhãn chart dùng chung PLPUReportStatuses; không có code chỉ thuộc dòng hàng như
AcceptedEnough hoặc OverdueNotDelivered trong filter PO này.

| Label trong statusDistribution | ReportStatus |
| --- | --- |
| Hoàn thành | Completed |
| Quá hạn còn thiếu | OverdueWithShortage |
| Chưa giao | NotDelivered |
| Có hàng lỗi QC | HasQCFail |
| QC đạt một phần | PartiallyAccepted |
| Đã hủy | Cancelled |
| Đã đóng | Closed |
| Đã đóng còn thiếu | ClosedWithShortage |
| Không có số lượng | NoQuantity |
| Đã giao đủ, chờ QC | DeliveredEnoughWaitingQC |
| Chờ QC | WaitingQC |
| Đã giao đủ | DeliveredEnough |
| Giao một phần | PartiallyDelivered |

Ví dụ từng nhóm được yêu cầu (cùng filter company/ngày/keyword/NCC/vật tư/loại/Status/IncludeInactive với /body):

```text
/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=Completed&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=OverdueWithShortage&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=NotDelivered&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=HasQCFail&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
/api/v1/reports/plpu/drilldown?Metric=totalPo&ReportStatus=PartiallyAccepted&From=2026-10-01&To=2026-10-31&PageNumber=1&PageSize=20
```

Response contract không đổi, ví dụ rút gọn:

```json
{"companyId":"20000000-0000-0000-0000-000000000001","metric":"totalPo","rowType":"order",
 "metricValue":2,"pageValue":1,"totalCount":2,"page":1,"pageSize":1,"totalPages":2,
 "items":[{"contribution":1,"reportStatusCode":"Completed","lineStatusCode":null,
 "order":{"purchaseOrderId":"30000000-0000-0000-0000-000000000001","poExternalId":"DDH-1","reportStatus":"Hoàn thành"},"line":null}]}
```

Với cùng cohort và thời điểm dữ liệu: totalCount = metricValue = purchaseOrderCount của nhóm /body.
Lọc sau cùng allocation và BuildHeaderRows như /body, trước summary/tổng đóng góp/sắp xếp/phân trang.
Mỗi PO một item, contribution=1; pageValue là số PO trang hiện tại, không thay tổng toàn nhóm.
Nhóm không có PO trả items=[], metricValue=totalCount=totalPages=pageValue=0.
Response không thêm ReportStatus, FE giữ filter đang chọn và dùng reportStatusCode từng item.

Swagger/OpenAPI khai báo ReportStatus optional string enum với đủ 13 code, ví dụ parameter/request/response,
response 200 PLPUDrilldownDto và 400/401/403. Test đối chiếu đủ 13 nhóm với body, nhiều trang không trùng/thiếu,
nhóm rỗng khi kết hợp Status persist, filter ngày/NCC/vật tư/keyword, cờ chồng lấp, validation trước DB,
company/quyền, và regression toàn bộ 18 metric cũ. Fixture/SQL translation không thay đối soát dữ liệu production.

## Header đối chiếu kế toán

Endpoint và query không đổi:

```text
GET /api/v1/reports/plpu/header?CompanyId=f54b3c96-4faa-43d1-8446-9d98c459c630&From=2026-01-01&To=2026-12-31&IncludeInactive=false
```

Response bổ sung `accounting`; các KPI NVL ngoài object này không thay đổi công thức hoặc phạm vi.
Đọc **toàn bộ tập PO mà view `purchase.v_po_ap_invoice_summary` trả về**, không tự thêm điều kiện
OrderType=NCC/PO, prefix PO/NVL hoặc loại VPP/BB. Chỉ áp thêm company và filter request khi có.
Không tạo bảng, không sửa/tạo view, không đổi công thức DB, không ghi dữ liệu.
Keyless EF model chỉ map view hiện có. Nếu môi trường thiếu view, truy vấn báo lỗi, không thay bằng số 0 giả.

Reader luôn scope cả view và PO theo company hiện tại sau gate PLPUUser/President/Developer + reports.plpu.view.
From/To lọc ngày tạo PO (inclusive, bỏ giờ); không lọc ngày hóa đơn hoặc ngày nhập kho.
PurchaseOrderId/SupplierId/OrderType/Status thu hẹp cohort PO. MaterialId lọc PO có dòng active chứa vật tư đó;
**tiền/lượng của PO được chọn vẫn lấy nguyên tổng view**, không tính lại chỉ trên dòng vật tư đã lọc.
Keyword tìm mã PO, snapshot tên NCC, mã/tên vật tư active trong PO, qua WhereKeyword như contract tìm kiếm.
PageNumber/PageSize/OnlyNeedAction không làm thay tổng header.

View hiện tại tự loại PO inactive và dòng inactive, loại mã PO trống. `accounting.includesInactive=false`
thể hiện giới hạn cố hữu của nguồn, kể cả request IncludeInactive=true cho cohort NVL. Không dựng lại
view để mở rộng inactive hoặc tự thay các phép COALESCE trong DB. Hóa đơn liên kết được lấy theo trạng thái
hiện tại, không phải snapshot cuối kỳ From/To. View hiện nối company + related_doc_no với mã PO, loại VOID;
không chuyển sang ap_doc_po_links hoặc bỏ các quy tắc hiện hành.

| Nhóm field trong accounting | Ý nghĩa/source |
| --- | --- |
| source, includesInactive | Tên view và phạm vi active cố hữu của nguồn |
| totalPo, supplierCount, poWithoutSupplierCount | Số PO trong cohort, số NCC khác null, số PO chưa có source_supplier_id |
| totalLines | Tổng po_line_count trong view |
| poQuantity | Tổng po_qty; view ưu tiên RealQuantity rồi RequestQuantity, **không đổi thành lượng nhập kho/lượng đặt** |
| poAmountExclVat | Tổng po_amount_excl_vat, giữ nguyên fallback/rounding trong view |
| poAmountInclVat | Tổng PurchaseOrderSnapshot.TotalPrice đã lưu; không tự dựng lại bằng tỷ lệ VAT |
| poVatAmount | Tổng (snapshot TotalPrice − po_amount_excl_vat), chỉ khi đủ snapshot tổng tiền |
| missingPoTotalCount | Số PO thiếu snapshot TotalPrice; nếu >0, hai tổng poAmountInclVat/poVatAmount trả null thay vì tổng thiếu |
| linkedInvoiceCount | Tổng invoice_count của view theo PO, không tự đếm lại/deduplicate chứng từ |
| poWithInvoiceCount, poWithoutInvoiceCount | PO có invoice_count >0 hoặc =0; không có liên kết không chứng minh chưa nhận hóa đơn thực tế |
| matchedPoCount, partialPoCount, overPoCount | Count reconcile_status MATCHED/PARTIAL/OVER từ DB; không suy đoán theo POStatus hoặc tự so lại tiền |
| invoiceQuantity | Tổng ap_qty |
| invoiceAmountExclVat, invoiceVatAmount, invoiceAmountInclVat | Tổng ap_amount_excl_vat, ap_vat_amount, ap_amount_incl_vat của view |
| quantityDifference, amountDifferenceExclVat | Tổng diff_qty/diff_amount_excl_vat, chiều **hóa đơn − PO**; âm là hóa đơn thấp hơn PO |
| uninvoicedPoAmountExclVat | Tổng tiền trước VAT của các PO invoice_count=0 |
| poWithInvoicePercent, matchedPoPercent | Số PO tương ứng / totalPo ×100, làm tròn 2; mẫu số 0 trả 0 |
| poStatuses | Count theo po_status persisted trong view, không phải reportStatus tính toán của biểu đồ NVL |

Tiền giữ đơn vị nguồn, không tự quy đổi tỷ giá hay hard-code currency. DB view dùng số liệu dòng hóa đơn
theo công thức hiện tại; API không tự trừ discount hoặc đổi cách tính invoice totals.
Empty cohort trả object đầy đủ với tổng/count/tỷ lệ 0, status list rỗng. Null chỉ dùng khi thiếu snapshot
PO cho tổng sau VAT/VAT. Snapshot từ PO có cùng company; không trả EF entity, chứng từ hoặc thông tin ngân hàng.

Ví dụ rút gọn:

```json
{
  "totalPO": 100,
  "receivedPurchaseValue": 1000000,
  "accounting": {
    "source": "purchase.v_po_ap_invoice_summary",
    "includesInactive": false,
    "totalPo": 2,
    "supplierCount": 1,
    "poAmountExclVat": 1000,
    "poAmountInclVat": 1080,
    "poVatAmount": 80,
    "missingPoTotalCount": 0,
    "linkedInvoiceCount": 1,
    "poWithInvoiceCount": 1,
    "poWithoutInvoiceCount": 1,
    "matchedPoCount": 1,
    "partialPoCount": 0,
    "overPoCount": 0,
    "invoiceAmountExclVat": 400,
    "invoiceVatAmount": 32,
    "invoiceAmountInclVat": 432,
    "amountDifferenceExclVat": -600,
    "uninvoicedPoAmountExclVat": 600,
    "poWithInvoicePercent": 50,
    "matchedPoPercent": 50,
    "poStatuses": [{ "status": "Approved", "count": 2 }]
  }
}
```

FE dựng nhóm “Đối chiếu kế toán” riêng từ accounting. Không dùng tổng NVL ngoài cùng thay cho tổng PO kế toán.
Các metric drilldown, /body và Excel Purchasing hiện có vẫn thuộc cohort NVL đã mô tả; không tự gắn
drawer/export đó cho accounting vì phạm vi và công thức khác. Không cộng tỷ lệ hoặc tổng chart để suy ra totals mới.

Tests kiểm tra tổng nguyên gốc view, snapshot thiếu/null, empty, cohort đủ loại PO không bị lọc NVL,
company hai phía join, filter ngày/loại/trạng thái, keyless/view mapping và PostgreSQL translation.
