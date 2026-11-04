# Nguồn tính báo cáo dùng chung

`DeliveryRevenueQuery` là nguồn doanh thu cho Executive P&L (tổng, Trend, Customer,
Sales, Product Type), CRM Activity và Sales Header. Không sao chép công thức sang
handler, controller hoặc frontend.

## Doanh thu và tiền tệ

- Ngày ghi nhận: `DeliveryOrder.CreatedDate`, khoảng `[From, ToExclusive)`.
- Chỉ lấy phiếu/dòng giao active, phiếu không `Canceled`, có liên kết dòng đơn hàng,
  loại phiếu của khách nội bộ `KH_VIETAUS`. Giữ nguyên điều kiện nguồn P&L hiện hành;
  không tự thêm filter `IsAttach`, trạng thái đơn hoặc loại đơn cho doanh thu.
- Doanh thu: `DeliveryOrderDetail.Quantity * MerchandiseOrderDetail.UnitPriceAgreed`.
- Quy đổi VND bằng tỷ giá của đơn. Currency rỗng/VND, tỷ giá thiếu hoặc không dương
  dùng hệ số 1 theo quy tắc tương thích hiện có. Không cộng VAT.
- `MerchandiseOrderId` và `CustomerId` dùng đếm distinct, không đếm số dòng làm số đơn.
- `includeCosts: false` bỏ projection lot/cost snapshot; nhánh không có quyền không
  gọi resolver giá vốn.

## Phân bổ sale

`SalesAttribution/SalesAttributionScopeResolver` được Sales Header và P&L Sales/
Product Type dùng chung. Phân bổ theo assignment active hiện tại của khách, không
theo `ManagerById` trên đơn. Chọn assignment theo UpdatedDate rồi CreatedDate giảm
dần trong tập sale được phép; sale có role hợp lệ và membership group hợp lệ.
Header và `GET /api/v1/reports/executive/pnl/dashboard/by-sales` cho Developer,
President, ACCUser, SaleUser, SaleAdmin, ACUser truy cập. Các role khác nhận 403;
chưa đăng nhập nhận 401. Handler kiểm tra cùng role set tập trung trước khi đọc dữ liệu.

Developer/President/ACCUser xem toàn company. Sale dùng predicate của CRM CustomerVisibility:
assignment cá nhân, assignment trong group mình làm admin, Work claim còn hạn của
cá nhân/group, lead chưa bị claim và quyền CustomerViewAll/Admin bổ sung nếu có.
Quyền xem customer tách khỏi attribution: sale có thể thấy customer thuộc sale khác
khi CRM cho phép. Thiếu EmployeeId đối với người không có quyền toàn company trả scope rỗng.
Bộ lọc SalePerson/SaleGroup chỉ thu hẹp tập customer được thấy, không cấp thêm quyền.
Company lấy từ current user; company khác không được truy cập. Scope áp dụng cả kỳ so sánh.

Header và by-sales dùng capability `reports.sales-profit.view` hiện có. Thiếu quyền:
header trả cost/profit/margin null; by-sales omit các metric/series costOfSales, profit,
marginPercent và profitGrowthPercent (kể cả dòng subtotal/tổng). Không đọc cost snapshot
hoặc gọi cost resolver. Có quyền thì giữ cách tính cũ. Role claims đã nằm trong token:
thay đổi claim cần login/refresh; token explicit thiếu permission vẫn bị che.

## Giá vốn

`Costing/ReportCostSourcePolicy.Current` là công tắc duy nhất cho tất cả P&L và
Sales Header. Hiện dùng **InventoryWeightedAverage**; API không nhận lựa chọn nguồn.

- InventoryWeightedAverage: giá bình quân NVL cuối tháng giao (có carry-forward
  ledger trước kỳ), nhân định mức Formula. Thiếu giá của bất kỳ NVL được xét thì
  fallback FormulaSnapshot.
- FormulaSnapshot: tổng Quantity * UnitPrice của FormulaMaterials active loại
  Material, nhân số lượng giao.
- WarehouseActual: cost buffer/sản lượng LSX khi resolver tìm được nguồn hợp lệ;
  không đủ dữ liệu thì fallback FormulaSnapshot.
- Không có Formula snapshot: dùng lot consumption cost snapshot dương, sau đó
  BaseCostSnapshot của dòng đơn. Giá vốn 0 vẫn có thể là thiếu dữ liệu nguồn.

Không lấy lại công thức/phụ phí 5.000/10.000/20.000 từ repo Backend cũ.

## Đối soát

So sánh cùng company, ngày đầu/cuối kỳ, customer scope, attribution, tiền tệ VND
và trạng thái dữ liệu. Header khi lọc sale/group khớp doanh thu/giá vốn của P&L **by-sales** trong cùng
kỳ. P&L tổng/Customer/Trend dùng toàn bộ nguồn company, còn by-sales dùng customer visibility và attribution, gồm UNASSIGNED khi không lọc sale/group.
Không buộc tổng hai tập khác nhau phải bằng nhau.
P&L tổng còn có sales deductions, điện, vận chuyển và chi phí khác; lợi nhuận sau
các khoản này không đồng nghĩa với `Revenue - CostOfSales` ở báo cáo sale.

`ReportMetricRules` dùng chung margin (làm tròn 2 chữ số), tăng trưởng và hệ số quy
đổi cho KPI. Header trả tăng trưởng null khi kỳ trước bằng 0; dashboard P&L legacy
giữ contract số 0/100 cho trường hợp này. Thay đổi contract legacy cần quyết định riêng.

Company của mọi endpoint report lấy từ current user; CompanyId query chỉ được bằng
company đó. Header có capability `reports.sales-profit.view`; xem [contract Sales](../../Features/Reports/Sales/README.md).

Sales Header/Body/Drawer và by-sales gọi resolver với includeUnassignedCompanyCustomers=true:
khi không lọc sale/group, customer đã qua visibility nhưng thiếu attribution được
cộng vào group UNASSIGNED. Người toàn company giữ doanh số lịch sử của customer
chưa phân bổ/inactive; sale vẫn chỉ thấy customer active trong CRM scope.
Product Type dùng cùng visibility nhưng vẫn yêu cầu attribution hợp lệ.
KH_VIETAUS hiện tại tiếp tục bị loại.
