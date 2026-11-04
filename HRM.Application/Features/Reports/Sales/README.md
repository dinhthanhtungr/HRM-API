# Báo cáo sale – Header

## API và phạm vi

### Tổng quan nhanh và phân tích riêng

- `GET /api/v1/reports/sales/header`: các KPI tổng quan, số PO theo loại/trạng thái
  và doanh số 6 tháng. Không phụ thuộc chiều phân tích đang chọn ở body.
- `GET /api/v1/reports/sales/body`: phân tích riêng, dùng chung From/To, CompanyId,
  SaleGroup/SalePerson với header. Chỉ Developer hoặc President; company khác trả
  403, chưa đăng nhập 401. GroupBy không hợp lệ hoặc ngày/filter không hợp lệ trả 400.
- Các endpoint Executive P&L cũ giữ nguyên để tương thích FE hiện tại.

Ví dụ: `/api/v1/reports/sales/body?From=2026-10-01&To=2026-10-09&GroupBy=1`.
`GroupBy`: 1 = Sale (nhóm sale -> nhân viên), 2 = Product (loại sản phẩm -> sản phẩm),
3 = CustomerType (Customer.CustomerGroup; không phải từng khách hàng).
Mặc định GroupBy=1, kỳ ngày mặc định giống header. Chưa phân loại trả UNKNOWN/
“Chưa phân loại”. Loại khách hàng dùng giá trị hiện hành, không phải snapshot lịch sử.

Body trả `companyId`, `from`, `to`, `currency`, `groupBy`, `canViewProfit`, `totals`,
`groups[]`. Mỗi group có `key`, `label`, `metrics`, `rows[]`; mỗi row có `key`,
`label`, `metrics`. Metrics gồm `revenue`, `deliveredQuantity`, `costOfSales`,
`profit`, `marginPercent`. FE dùng cùng schema cho bảng và biểu đồ; chuyển GroupBy
chỉ tải lại body. Không giới hạn topN: tổng rows/group luôn đối soát totals và header.
Margin tính từ tổng lợi nhuận/tổng doanh thu, không cộng hoặc trung bình các tỷ lệ.
Thiếu capability xem lợi nhuận thì cost/profit/margin null ở tất cả cấp; không đọc cost resolver.

Header bổ sung `completedPoCount`, `incompletePoCount`: PO thuộc ba loại Merchandise,
Complaint, SampleRequest **tạo trong kỳ**, active, không Cancelled, không khách nội bộ,
đúng attribution. Completed/Delivered là hoàn thành; các trạng thái còn lại là chưa
hoàn thành. Hai số cộng bằng merchandisePoCount + complaintPoCount + samplePoCount.
Đây là trạng thái hiện tại, không phải trạng thái snapshot tại cuối kỳ.

`monthlyRevenue[]`: 6 điểm tháng tăng dần, tháng chứa To và 5 tháng trước (trừ giới
hạn năm 0001). Mỗi điểm có `month`, `from`, `to`, `revenue`; tháng không có dữ liệu
trả 0. Tháng cuối chỉ tính đến ngày To; 5 tháng trước tính trọn tháng. Bộ lọc sale/group
và attribution giống header; khoảng From không giới hạn lịch sử của biểu đồ này.
`previousRevenue` vẫn chỉ tính kỳ trước có cùng số ngày, không cộng toàn bộ lịch sử
6 tháng. Các số PO theo loại đã có đủ để FE dựng biểu đồ bánh, không gọi thêm API.

Đối soát: cùng company/From/To/sale/group, `body.totals` bằng revenue, costOfSales,
profit, marginPercent của header. Nguồn revenue và cost policy dùng chung Executive P&L.

`GET /api/v1/reports/sales/header?From=2026-09-01&To=2026-09-30`

Yêu cầu authenticated, company hiện hành. `CompanyId` tùy chọn chỉ được bằng
current company (khác hoặc thiếu company trả 403). `SaleGroup`, `SalePerson` là
GUID tùy chọn, chỉ thu hẹp phạm vi được phép. Header cho Developer, President,
ACCUser, SaleUser, SaleAdmin, ACUser truy cập; role khác 403. Developer/President/
ACCUser xem toàn company. Sale dùng cùng predicate customer visibility của CRM:
assignment cá nhân/group làm admin, Work claim còn hạn, lead chưa bị claim và quyền
CustomerViewAll/Admin bổ sung nếu có. Quyền customer độc lập với nhân viên được
phân bổ doanh số. Thiếu employee trả tập rỗng. Xem chi tiết [Commons Reporting](../../../Commons/Reporting/README.md).

From/To là ngày inclusive, bỏ giờ. Mặc định To là ngày hiện tại từ IDateTimeProvider,
From là đầu tháng của To. Khoảng đảo ngược, không biểu diễn được kỳ trước hoặc quá
1096 ngày, GUID sale/group rỗng trả 400. Kỳ trước có cùng số ngày và kết thúc ngay
trước From. Bộ lọc này không phải FromMonth/ToMonth: để đối soát P&L tháng, truyền
ngày đầu tháng và ngày cuối tháng tương ứng.

## KPI

`averageSellingPrice` (ASP): revenue / deliveredQuantity, bình quân gia quyền theo
lượng giao thực tế trong kỳ, làm tròn 2 chữ số. Tiền VND không VAT, lượng theo đơn
vị lưu trên nguồn, chưa chuẩn hóa khác đơn vị. Lượng giao <= 0 trả null (FE hiển thị
“—”). Không phải trung bình cộng đơn giá từng dòng, không dùng giá PO chưa giao.

Số PO tạo trong khoảng From/To (inclusive): `merchandisePoCount` cho OrderType.Merchandise
(bằng newOrderCount), `complaintPoCount` cho OrderType.Complaint, `samplePoCount`
cho OrderType.SampleRequest. Đếm từng MerchandiseOrder một lần, không phải số dòng
hoặc số mã PONo distinct. Chỉ lấy active, không Cancelled, không khách nội bộ và
đúng company/customer attribution cùng bộ lọc sale/group. Không tính yêu cầu phối
mẫu trong bảng SampleRequests vào số PO mẫu.

`expectedPendingAmountInPeriod`: giá trị còn có thể giao trong kỳ, tính từ lượng
thiếu tại cuối kỳ * giá thỏa thuận * tỷ giá. Chỉ tính ngày ExpectedDeliveryDate
(fallback DeliveryRequestDate) nằm trong From/To; loại đơn Completed/Delivered/Paused.
`expectedPendingByMonth[]` trong cùng response `/api/v1/reports/sales/header` chia
chính tập dòng của `expectedPendingAmountInPeriod` theo tháng của ExpectedDeliveryDate
(fallback DeliveryRequestDate), tăng dần. Mỗi phần tử có `month` (ngày đầu tháng),
`amount` (VND không VAT, cùng phép tính/tỷ giá, không làm tròn thêm).
Chỉ xét lịch giao trong From/To; tháng đầu/cuối chỉ lấy phần ngày thuộc kỳ.
Lượng còn thiếu vẫn tính tại cuối toàn kỳ, không tính lại tại cuối từng tháng.
Tổng `amount` bằng `expectedPendingAmountInPeriod`; tháng không có dòng hợp lệ
không xuất hiện, tập rỗng trả `[]` và tổng 0. Scope company/customer/sale/group và
quyền xem giữ nguyên header; không query thêm hoặc trả cost/profit.
Ví dụ rút gọn:
```json
{
  "expectedPendingAmountInPeriod": 850000,
  "expectedPendingByMonth": [
    { "month": "2026-09-01T00:00:00", "amount": 500000 },
    { "month": "2026-10-01T00:00:00", "amount": 350000 }
  ]
}
```

`expectedRevenueInPeriod = revenue + expectedPendingAmountInPeriod`: doanh số kỳ
vọng có thể đạt, không phải doanh thu thực bán. Tham khảo báo cáo cũ nhưng dùng
nguồn lượng giao chuẩn P&L, company/customer scope hiện hành và tiền VND không VAT.

| Field | Ý nghĩa |
|---|---|
| revenue, deliveredQuantity | Tiền và lượng giao trong kỳ từ DeliveryRevenueQuery |
| previousRevenue, revenueGrowthAmount | Doanh thu kỳ trước; chênh lệch kỳ này trừ kỳ trước |
| revenueGrowthPercent | Chênh lệch / kỳ trước * 100, làm tròn 2 chữ số; null nếu kỳ trước bằng 0 |
| customerCount, deliveredOrderCount | Khách/đơn distinct có dòng giao trong kỳ; đơn cũ vẫn được tính |
| newOrderCount, newOrderAmount | Đơn active Merchandise không Cancelled/nội bộ, tạo trong kỳ; gồm chưa giao; tiền từ các dòng active |
| pendingQuantity, pendingAmount | Tổng max(đặt - lượng giao trước ToExclusive, 0) riêng từng dòng; tiền = lượng thiếu * giá thỏa thuận * tỷ giá |
| pendingOrderCount | Số đơn distinct còn ít nhất một dòng thiếu |
| overdueOrderCount | Số đơn distinct có dòng còn thiếu và ngày yêu cầu giao trước ngày To |
| costOfSales | Giá vốn của các dòng giao trong kỳ, cùng resolver/policy với P&L |
| profit, marginPercent | Revenue - CostOfSales; Profit / Revenue * 100, làm tròn 2 chữ số, 0 khi revenue bằng 0 |
| canViewProfit | Quyết định capability của backend, không dùng field số để suy đoán quyền |

Tiền trả VND, không VAT; lượng theo đơn vị lưu trên nguồn. Tiền tổng cộng không làm
tròn từng dòng. Tồn tính tại cuối kỳ: giao sau kỳ không làm giảm tồn của kỳ. Đơn tạm
dừng vẫn có thể còn tồn; đơn hủy bị loại. Dữ liệu không phải snapshot lịch sử bất biến:
trạng thái active/cancelled, assignment, định mức và giá nguồn được đọc tại thời điểm
request; đổi những dữ liệu này có thể đổi báo cáo kỳ cũ.

## Công thức và nguồn tính hiện hành

- Revenue: tổng lượng giao * UnitPriceAgreed * tỷ giá VND; ghi nhận theo
  DeliveryOrder.CreatedDate trong [From, To + 1 ngày), không theo ngày PO/thu tiền.
  Chỉ dòng giao active, phiếu active không Canceled, không khách nội bộ, có link
  MerchandiseOrderDetail và đúng company, customer attribution được phép.
- DeliveredQuantity: tổng lượng các dòng revenue. CustomerCount và DeliveredOrderCount
  đếm distinct customer/PO từ chính các dòng này, kể cả PO tạo trước kỳ và dòng có giá 0.
- PreviousRevenue: cùng nguồn revenue, trong kỳ ngay trước From có cùng số ngày.
  Không mặc định là tháng lịch trước. GrowthAmount = revenue - previousRevenue;
  GrowthPercent = GrowthAmount / previousRevenue * 100, làm tròn 2 số lẻ,
  previousRevenue = 0 trả null.
- Pending: chỉ PO Merchandise active không Cancelled/nội bộ, tạo từ **01/03/2026**
  đến trước hết To; loại Delivered và Completed bằng SalesPendingOrderRules.
  dòng active. Mỗi dòng còn thiếu = max(ExpectedQuantity - lượng giao hợp lệ từ
  đầu lịch sử đến hết To, 0). Giao dư một dòng không bù cho dòng khác. PendingAmount
  = tổng lượng thiếu * UnitPriceAgreed * tỷ giá; PendingOrderCount đếm distinct PO
  có ít nhất một dòng thiếu. Paused vẫn có thể xuất hiện trong pending nếu còn
  thiếu; Delivered/Completed bị loại dù dữ liệu giao liên kết lịch sử còn thiếu.
  Cutoff/status dùng chung cho lượng, tiền, số PO chờ giao, quá hạn và phần kỳ vọng.
- OverdueOrderCount: distinct PO còn thiếu, có DeliveryRequestDate.Date < ngày hiện tại từ IDateTimeProvider. Điều kiện này dùng chung cho header và drawer overdueOrder, độc lập với To.
  Ngày yêu cầu đúng ngày hiện tại chưa tính quá hạn; không dùng ExpectedDeliveryDate ở KPI này.
- ExpectedPendingAmountInPeriod: lọc từ phần thiếu trên, ngày ExpectedDeliveryDate
  (fallback DeliveryRequestDate) trong kỳ, loại Completed/Delivered/Paused.
  Không đánh giá tồn kho/năng lực sản xuất hay xác suất giao thành công.
  ExpectedRevenueInPeriod = revenue + expectedPendingAmountInPeriod.
- NewOrderAmount: tổng TotalPriceAgreed * tỷ giá của dòng active thuộc PO Merchandise
  tạo trong kỳ; không tính lại từ lượng giao, không cộng complaint/mẫu.
- CostOfSales: tổng cost của các dòng revenue, chung policy với Executive P&L.
  Policy hiện tại InventoryWeightedAverage: giá vốn đơn vị công thức = tổng
  định mức nguyên liệu * AvgCostAfter dương mới nhất trước hết tháng giao của
  từng nguyên liệu trong ledger cùng company (có thể lấy ledger từ tháng trước).
  Cần đủ giá cho các nguyên liệu có mã trong nguồn định mức để dùng nguồn này.
  Nếu thiếu, fallback tổng Quantity * UnitPrice của FormulaMaterials active loại
  Material; sau đó lot TotalCostSnapshot dương đã chuẩn hóa; cuối cùng BaseCostSnapshot
  của dòng PO * lượng giao. Giá vốn tháng có thể thay đổi khi ledger tháng cập nhật,
  kể cả báo cáo chọn To ở giữa tháng. Không phải giá vốn snapshot theo từng ngày giao.
- Profit = revenue - costOfSales. MarginPercent = profit / revenue * 100, làm tròn
  2 số lẻ; revenue = 0 trả 0 khi có quyền. Đây là lợi nhuận gộp, chưa trừ chi phí
  vận hành/thuế. Body dùng tổng revenue/profit để tính margin từng cấp, không trung
  bình cộng margin của các dòng con.
- ASP = revenue / deliveredQuantity, làm tròn 2 số lẻ, lượng <= 0 trả null.
  Bình quân gia quyền theo lượng, không trung bình cộng các đơn giá. Chưa chuẩn hóa
  lượng giữa các đơn vị khác nhau. Tiền VND không VAT; currency trống/VND hoặc tỷ
  giá thiếu/không dương thì dùng tỷ giá 1.

PO theo loại/trạng thái và xu hướng 6 tháng theo quy ước ở phần tổng quan phía trên.
Các chỉ số đọc dữ liệu hiện tại, không phải snapshot lịch sử bất biến.

## Tổ chức tính toán trong code

### Truy vấn và hiệu năng

Header chỉ lấy PO tạo trong kỳ hoặc PO còn có thể thuộc tập chờ giao (từ 01/03/2026,
chưa Delivered/Completed), thay vì tải các dòng PO toàn lịch sử rồi loại trong calculator.
Vẫn lấy dòng PO đã đóng tạo trong kỳ để giữ đúng NewOrderAmount. Lượng giao lịch sử
chỉ tổng hợp cho detail của PO đủ điều kiện chờ giao, không giới hạn ngày giao về
01/03 để không thay đổi công thức lượng thiếu.

Doanh số 6 tháng dùng projection không có cost/nhãn sản phẩm/danh sách mã lô.
Cost snapshot chỉ đọc cho kỳ hiện tại. Header và by-sales bỏ display fields không
dùng nhưng giữ HasNormalizedLots/TotalCostSnapshot/BaseCostSnapshot khi tính cost.
Resolver gộp role-sale và group thành một query, vẫn giữ company/current employee,
filter sale/group và thứ tự chọn assignment. Định mức cost được đọc một lần dùng
cho cả fallback lẫn bình quân kho; ledger trong kỳ chỉ trả bản ghi dương mới nhất
theo nguyên liệu/tháng, thay vì tải mọi giao dịch. Không dùng cache hay đổi công thức.

Verification: harness read-only tại artifacts/verify-sales-report/perf chạy handler
trên database cấu hình, kỳ 01–31/10/2026, Developer cùng company. So sánh đầy đủ JSON
header và by-sales trước/sau; đo sau lượt warm-up. Đây là thời gian handler, không
bao gồm HTTP/auth/serialization và không phải cam kết latency cho mọi kỳ/dữ liệu.

| Thành phần | Trách nhiệm |
|---|---|
| SalesReportPeriod | Chuẩn hóa kỳ ngày, kiểm tra hợp lệ, xác định kỳ trước |
| DeliveryRevenueQuery (Commons) | Projection nguồn lượng giao và doanh số chuẩn |
| SalesAttributionScopeResolver (Commons) | Scope company/current user và phân bổ customer về sale/group |
| DeliveryCostResolver, DeliveryCostRules (Commons) | Giá vốn, nguồn ledger/formula và thứ tự fallback |
| ReportMetricRules (Commons) | Tỷ giá, tăng trưởng và biên lợi nhuận dùng chung |
| SalesReportHeaderReader | Đọc/projection và đếm PO theo loại/trạng thái trong scope |
| SalesReportHeaderCalculator | Tính KPI thuần: pending, kỳ vọng, ASP và ghép DTO header |
| SalesPendingOrderRules | Mốc PO 01/03/2026 và loại Delivered/Completed/Cancelled khỏi tập chờ giao |
| SalesReportRevenueTrend | Tổng hợp doanh số tháng, bù tháng 0 và giới hạn tháng cuối |
| SalesReportBodyReader / SalesReportBodyBuilder | Đọc chiều phân tích và tổng hợp row/group/totals |

Tách theo nhóm nghiệp vụ/trách nhiệm, **không phải mỗi field một helper**.
Các KPI phụ thuộc cùng tập dòng pending được tính chung để tránh lệch điều kiện.
Calculator/helper thuần không tự đọc DB/claims/config; reader/resolver xử lý dữ
liệu và scope. Controller chỉ kiểm tra boundary, gửi query và trả response.

## Quyền cost/lợi nhuận

Capability `reports.sales-profit.view` ánh xạ mặc định President, ACCUser, Developer
tại ApplicationPermissionRoleSets; token đã migrate dùng Identity role permission
claims. SaleUser/Admin không tự được xem. Capability không mở rộng company/customer
scope. Khi thiếu quyền, costOfSales/profit/marginPercent là **null**, canViewProfit=false;
không query FormulaMaterials/ledger/cost resolver. Có quyền nhưng không phát sinh thì
trả 0. Không trả cost hay profit bình quân cho phép tính ngược field bị che.

Baseline seed/rollback **hiện có** đã thêm claim này, không thêm schema/migration và
không tự chạy SQL. Môi trường đã migrate cần chạy lại baseline seed idempotent và
login/refresh token để nhận claim mới; token explicit permissions thiếu claim vẫn bị
che dù role thuộc nhóm mặc định. By-sales dùng cùng capability nhưng omit metric/series bị cấm; các P&L khác giữ contract hiện có.

## Response mẫu

```json
{
  "companyId": "11111111-1111-1111-1111-111111111111",
  "from": "2026-09-01T00:00:00",
  "to": "2026-09-30T00:00:00",
  "previousFrom": "2026-08-02T00:00:00",
  "previousTo": "2026-08-31T00:00:00",
  "currency": "VND",
  "revenue": 300,
  "expectedPendingAmountInPeriod": 700,
  "expectedRevenueInPeriod": 1000,
  "previousRevenue": 0,
  "revenueGrowthAmount": 300,
  "revenueGrowthPercent": null,
  "deliveredQuantity": 3,
  "averageSellingPrice": 100,
  "customerCount": 1,
  "deliveredOrderCount": 1,
  "newOrderCount": 0,
  "merchandisePoCount": 0,
  "complaintPoCount": 0,
  "samplePoCount": 0,
  "completedPoCount": 0,
  "incompletePoCount": 0,
  "monthlyRevenue": [
    { "month": "2026-04-01", "from": "2026-04-01", "to": "2026-04-30", "revenue": 0 },
    { "month": "2026-05-01", "from": "2026-05-01", "to": "2026-05-31", "revenue": 0 },
    { "month": "2026-06-01", "from": "2026-06-01", "to": "2026-06-30", "revenue": 0 },
    { "month": "2026-07-01", "from": "2026-07-01", "to": "2026-07-31", "revenue": 0 },
    { "month": "2026-08-01", "from": "2026-08-01", "to": "2026-08-31", "revenue": 0 },
    { "month": "2026-09-01", "from": "2026-09-01", "to": "2026-09-30", "revenue": 300 }
  ],
  "newOrderAmount": 0,
  "pendingQuantity": 7,
  "pendingAmount": 700,
  "pendingOrderCount": 1,
  "overdueOrderCount": 1,
  "canViewProfit": false,
  "costOfSales": null,
  "profit": null,
  "marginPercent": null
}
```

Nguồn/cost fallback/phạm vi đối soát: [Commons Reporting](../../../Commons/Reporting/README.md).
Header trả số liệu cho thẻ KPI, biểu đồ cơ cấu PO và xu hướng 6 tháng; bảng phân tích
riêng lấy từ body. Không trả dữ liệu export hay chỉ số thu tiền.

## Drawer chi tiết KPI header

Hai API GET chỉ đọc, authenticated và cùng company/customer attribution như header:

- `/api/v1/reports/sales/orders?metric=completedPo`: PO hoàn thành.
- `/api/v1/reports/sales/orders?metric=incompletePo`: PO chưa hoàn thành.
- `/api/v1/reports/sales/orders?metric=pendingOrder`: đơn còn chờ giao.
- `/api/v1/reports/sales/orders?metric=overdueOrder`: đơn quá hạn.
- `/api/v1/reports/sales/orders?metric=deliveredOrder`: đơn có giao trong kỳ.
- `/api/v1/reports/sales/orders?metric=merchandisePo`: PO bán hàng tạo trong kỳ (OrderType 1).
- `/api/v1/reports/sales/orders?metric=complaintPo`: PO complaint tạo trong kỳ (OrderType 2).
- `/api/v1/reports/sales/orders?metric=samplePo`: PO gửi mẫu tạo trong kỳ (OrderType 3), không phải yêu cầu mẫu Lab.
- `/api/v1/reports/sales/customers?metric=deliveredCustomer`: khách có giao trong kỳ.

`metric` bắt buộc, phân biệt hoa/thường. Nhận `From`, `To`, `CompanyId`, `SaleGroup`,
`SalePerson` giống header; `pageNumber` mặc định 1, `pageSize` mặc định 20, tối đa 100.
Ba metric cơ cấu PO lọc CreateDate từ From đến hết To, không yêu cầu còn thiếu hàng,
không loại Completed/Delivered; loại inactive, Cancelled và KH_VIETAUS theo rule header.
`totalCount` lần lượt đối soát merchandisePoCount, complaintPoCount và samplePoCount.
Trả cùng DTO orders (gồm totalQuantity, deliveredQuantity, promisedDeliveryDate);
pendingQuantity, earliestPendingRequestedDate và deliveredQuantityInPeriod null ở ba metric này.
Metric/ngày/filter/phân trang không hợp lệ trả 400; company khác/thiếu trả 403,
chưa đăng nhập trả 401. Filter sale/group chỉ thu hẹp phạm vi được phép.
Không cần role Developer/President của body để mở drawer; scope giống header.

Response: `from`, `to`, `totalCount`, `pageNumber`, `pageSize`, `items`.
`totalCount` tính trước phân trang và khớp KPI tương ứng khi cùng bộ lọc và dữ liệu
nguồn chưa thay đổi. Trang vượt cuối trả items rỗng nhưng giữ totalCount.
Không phát sinh mutation, schema hoặc capability mới; không query cost/profit.

Ví dụ `/api/v1/reports/sales/orders?metric=pendingOrder&From=2026-09-01&To=2026-09-30&pageNumber=1&pageSize=20`:

```json
{
  "from": "2026-09-01T00:00:00",
  "to": "2026-09-30T00:00:00",
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 20,
  "items": [{
    "orderId": "11111111-1111-1111-1111-111111111111",
    "externalId": "ORDER-001",
    "poNo": "PO-001",
    "customerId": "22222222-2222-2222-2222-222222222222",
    "customerCode": "KH-001",
    "customerName": "Khách A",
    "orderType": 1,
    "status": "Processing",
    "createdDate": "2026-04-01T00:00:00",
    "promisedDeliveryDate": "2026-09-18T00:00:00",
    "totalQuantity": 10,
    "deliveredQuantity": 4,
    "deliveredQuantityInPeriod": null,
    "pendingQuantity": 6,
    "earliestPendingRequestedDate": "2026-09-15T00:00:00"
  }]
}
```

Order drawer đếm theo `orderId`, không distinct mã PONo, sắp ngày tạo PO giảm dần,
ID tăng dần để phân trang ổn định. Mã/tên khách lấy snapshot của PO, không đọc
thông tin liên hệ CRM. `orderType` là code OrderType hiện hành; `status` là trạng thái
hiện tại của PO. PO hoàn thành/chưa hoàn thành dùng ba loại tạo trong kỳ theo rule
header, kể cả PO không có dòng active. Chờ giao/quá hạn chỉ Merchandise từ 01/03/2026,
loại Completed/Delivered/Cancelled dù dòng còn thiếu, Paused vẫn có thể xuất hiện.
`totalQuantity` là tổng ExpectedQuantity của các dòng active trong toàn PO, không giới hạn
theo lịch giao trong kỳ. `deliveredQuantity` là tổng lượng giao hợp lệ của chính các dòng
đó từ trước kỳ đến hết To, cùng nguồn DeliveryRevenueQuery (loại phiếu hủy/inactive,
KH_VIETAUS và company khác). Hai field có ở cả tám metric orders, không có dữ liệu trả 0.
Lượng đã giao không bị chặn ở tổng lượng đặt, để phản ánh giao vượt; FE có thể chặn thanh
tiến trình ở 100% và không chia khi totalQuantity <= 0. Khác deliveredQuantityInPeriod
chỉ đếm lượng giao trong kỳ chọn. Không thêm hai field này vào tổng hợp customers.
`pendingQuantity` là tổng thiếu của mọi dòng thiếu trong đơn tại hết To; với drawer
quá hạn cũng trả tổng thiếu toàn đơn, không chỉ phần quá hạn.
`earliestPendingRequestedDate` là DeliveryRequestDate nhỏ nhất của dòng còn thiếu.
`promisedDeliveryDate` là ngày hứa giao từ `MfgProductionOrder.ExpectedDate`, nối dòng PO qua `MfgOrderPO` active.
Chỉ xét lệnh sản xuất active cùng company hiện hành.
Với pendingOrder/overdueOrder, lấy ngày sớm nhất của các dòng active còn thiếu;
với completedPo/incompletePo/deliveredOrder, lấy ngày sớm nhất của các dòng active trong PO.
Nhiều lịch giao được đại diện bằng ngày sớm nhất có dữ liệu; không có ngày hứa giao trả null,
không thay bằng ngày yêu cầu giao. Field có ở cả tám metric orders; customers là tổng hợp khách,
không trả một ngày hứa giao chung cho nhiều PO. Bổ sung field không đổi rule đếm/quá hạn.
Ngày yêu cầu đúng ngày hiện tại chưa quá hạn. Hai field này null ở drawer PO trạng thái/giao.
`deliveredQuantityInPeriod` chỉ có ở deliveredOrder, cộng lượng giao trong kỳ;
null ở các metric khác, 0 là giá trị thực nếu dòng giao có tổng lượng bằng 0.
Nguồn deliveredOrder giống header, không thêm bộ lọc trạng thái/active của PO.

Customer drawer sắp CustomerId tăng dần; mỗi item gồm `customerId`, `customerCode`,
`customerName`, `deliveredOrderCount`, `deliveredQuantityInPeriod`.
Mã/tên lấy snapshot từ PO tạo mới nhất trong các PO có giao của khách trong kỳ,
trùng ngày chọn ID tăng dần; không lấy tên CRM hiện hành. Chuỗi rỗng nghĩa là snapshot
nguồn trống/thiếu. Ví dụ item:

```json
{
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerCode": "KH-001",
  "customerName": "Khách A",
  "deliveredOrderCount": 1,
  "deliveredQuantityInPeriod": 3
}
```

Khách/đơn có dòng giao giá 0 hoặc lượng 0 vẫn được đếm như header. Lượng theo đơn vị
lưu nguồn, chưa chuẩn hóa đơn vị. Ngày giao ghi nhận theo DeliveryOrder.CreatedDate.
Các ID phục vụ điều hướng; API chi tiết đích vẫn phải kiểm tra quyền riêng.
FE gửi cùng bộ lọc header khi bấm KPI, dùng totalCount cho tổng drawer và items cho
trang hiện tại. Dữ liệu/trạng thái/assignment thay đổi giữa hai request có thể làm
header và drawer khác số; tải lại header khi cần đối soát.


## Tổng công ty và khách chưa phân bổ sale

Header, body và hai drawer dùng scope mở rộng khi người gọi có quyền xem toàn công ty
(President, Developer, ACCUser) và không truyền SaleGroup/SalePerson.
Khách ngoài công ty có đơn thuộc company hiện tại được đưa vào KPI dù không có assignment,
nhân viên đã inactive, thiếu role/membership hoặc khách đã inactive. Việc đổi assignment
không làm mất doanh số lịch sử khỏi tổng công ty. Các điều kiện active/trạng thái của PO,
phiếu giao và dòng giao vẫn theo từng KPI như phần trên.

Body chiều Sale dùng group key `UNASSIGNED`, label “Chưa phân bổ sale”, row key là
Guid.Empty dạng chuỗi cho khách không có attribution hợp lệ; không phải nhân viên thật.
Product/CustomerType vẫn cộng các dòng này. Header, body, drawer dùng cùng scope;
monthlyRevenue cũng gồm phần chưa phân bổ. Không thay đổi schema response.

Khi chọn SaleGroup/SalePerson, vẫn yêu cầu attribution hợp lệ sau khi áp customer
visibility. Không lọc thì customer thuộc scope nhưng thiếu attribution vào UNASSIGNED.
Sale không được mở rộng sang customer ngoài CRM visibility; drawer dùng cùng scope
để khớp KPI. Company và capability reports.sales-profit.view vẫn kiểm tra độc lập.

Khách hiện tại có ExternalId KH_VIETAUS tiếp tục bị loại, kể cả snapshot phiếu giao sai.
Các giao dịch bán ra ngoài dùng mã khách nội bộ chưa được tự chuyển thành doanh số ngoài.
P&L tổng legacy vẫn dùng nguồn DeliveryRevenueQuery nên có thể lệch phần snapshot khách
nội bộ không chuẩn; by-sales dùng cùng scope mới với header.

