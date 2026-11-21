# Delivery OTD

## Phạm vi IMS

Tài khoản IMS cần quyền `reports.delivery-otd.view` và ngữ cảnh nhân viên/công ty.
Đầu báo cáo, biểu đồ, xu hướng theo tháng, chi tiết và tổng lượng giao trong kỳ
cùng giới hạn theo khách active bên ngoài trong công ty hiện tại. Dòng giao phải
khớp khách trên đơn. Dữ liệu dùng ngày, lượng và trạng thái giao, không trả giá,
doanh thu, công thức, lô, ghi chú hoặc lý do tạm dừng. Token explicit cần role
claim tương ứng và refresh sau khi cấp quyền.

## Xu hướng theo PO giống card header

Mỗi điểm `charts.monthly[]` bổ sung `poSummary` giống hệt cấu trúc và công thức
`header.poSummary`. FE vẽ đường **Tỷ lệ giao đúng hạn theo PO** bằng
`monthly[i].poSummary.poOtdPercent`; tooltip dùng onTimePos/evaluatedPos,
totalPos, latePos, pendingPos. Không dùng summary.otdPercent hoặc
summary.quantityOtdPercent cho đường PO. Summary cũ giữ nghĩa theo dòng/khối lượng
để tương thích; poSummary ở các chart khác là null, chỉ monthly cung cấp giá trị.

Tập dòng từng tháng lấy theo DueDate, fallback RequestedDate khi thiếu ngày hứa
(bao gồm cả dòng thiếu ngày hứa chưa giao); deduplicate OrderId trước tính PO.
Reader chung với header lấy toàn bộ dòng của PO để resolve MAX ngày hứa MFG,
MAX ngày phiếu đến AsOf và lịch sử tạm dừng. Các ngoại lệ thiếu ngày hứa/tạm dừng
được tính đạt giữ nguyên như card. Một PO có dòng thuộc nhiều tháng có thể xuất
hiện ở từng tháng; không cộng số PO các tháng để đối soát tổng kỳ.
Chọn trọn một tháng, cùng filter và AsOf thì poSummary của điểm tháng đó khớp
header.poSummary. Chọn quý/năm thì header đánh giá PO duy nhất toàn kỳ, không
phải trung bình tỷ lệ tháng. Tháng rỗng trả poSummary count 0, poOtdPercent null.

## Khoảng thời gian xu hướng monthly

`GET /api/v1/reports/delivery-otd/charts`: riêng `monthly` lấy tối thiểu 6 tháng
liên tục kết thúc tại tháng To, lấy trọn tháng. Chọn tháng 10/2026 trả 05–10/2026;
chọn quý III/2026 trả 04–09/2026; chọn năm 01/01–31/12/2026 trả đủ 12 tháng
01–12/2026. Khoảng tùy chọn dài hơn 6 tháng giữ đủ các tháng From–To.
FE phải gửi trọn năm khi chọn năm vì API nhận From/To, không nhận loại preset.
AsOf và DateBasis cùng filter khách/sản phẩm/manager, company và mốc tạo ĐHG
01/03/2026 vẫn giữ nguyên. Tháng rỗng trả count/quantity 0, tỷ lệ null (không
hiểu null là OTD 0%). Tháng sau AsOf chưa có mẫu số đến hạn nên tỷ lệ có thể null.
Header, details và các nhóm chart khác vẫn dùng kỳ gốc; không đối soát tổng
monthly mở rộng với header kỳ ngắn. FE không cắt chuỗi monthly về kỳ gốc.

## Tổng lượng giao trong kỳ

`GET /api/v1/reports/delivery-otd/header` bổ sung field cấp root
`deliveredQuantityInPeriod` (decimal). FE dùng field này cho “Tổng khối lượng đã
giao trong kỳ”, không dùng summary.onTimeQuantity hoặc details.deliveredQuantity.
Ví dụ response rút gọn: `{ "deliveredQuantityInPeriod": 285458.99 }`.

Tổng DeliveryOrderDetail.Quantity > 0 theo ngày tạo phiếu trong [From,
min(To, AsOf)] gồm trọn ngày. Lấy phiếu/dòng active, không hàng kèm, không
Canceled/Cancelled, cùng company; cộng cả Pending/InProgress. ĐHG/dòng phải active,
không Cancelled, OrderType Merchandise, lượng đặt > 0, tạo từ 01/03/2026.
Giữ CustomerId/ProductId/ManagerId; không lọc hạn yêu cầu/hứa theo kỳ, không phụ
thuộc DateBasis. Vì vậy vẫn tính hàng giao trong kỳ cho đơn có hạn ngoài kỳ,
kể cả không có dòng OTD trong kỳ. Không nối lot, không chặn lượng giao vượt ở
lượng đặt; không có dữ liệu hoặc From > AsOf trả 0. Đơn vị giữ theo dữ liệu nguồn,
không đổi đơn vị hoặc làm tròn thêm. Đây là lượng ghi nhận trên phiếu, chưa phải
bằng chứng khách thực nhận và không phải tổng của detail drawer OTD.

Chỉ đánh giá ĐHG có `MerchandiseOrder.CreateDate >= 01/03/2026` (bao gồm ngày
01/03), áp dụng chung header, charts, details và KPI PO. Đây là mốc ngày tạo đơn,
không phải ngày tạo phiếu giao hoặc hạn giao. From/To vẫn lọc hạn giao theo
DateBasis; đơn tạo trước mốc bị loại kể cả giao hàng/hạn giao nằm trong kỳ chọn.

API chỉ đọc cho dashboard giao đủ đúng hạn. Không tạo migration, bảng, lịch giao,
POD hoặc thay đổi lifecycle điều vận. Mỗi phần tử đánh giá là một dòng ĐHG, không
phải từng phiếu, từng lot hoặc từng lịch giao. Chỉ lấy OrderType Merchandise,
header/detail active, không Cancelled, ExpectedQuantity > 0. KPI theo dòng: đơn tạm dừng vẫn
tính và detail trả isDeliveryPaused để FE nhận diện; không tự dời hạn.

## API và quyền

- GET `/api/v1/reports/delivery-otd/header`: metadata và KPI tổng.
- GET `/api/v1/reports/delivery-otd/charts`: monthly, customers, products, statuses, aging.
- GET `/api/v1/reports/delivery-otd/details`: danh sách phân trang.

Authorize dùng DeliveryOrderAccessRules.ReadRoles giống API đọc điều vận;
handler kiểm tra lại CanRead, authenticated và CompanyId khác rỗng. Phạm vi
company của current user, không nhận company từ client; theo quyền điều vận hiện
hành, không áp dụng attribution của báo cáo doanh số. Không thêm capability,
không đọc giá/cost/margin, không trả thông tin liên hệ hay EF entity.

Filter chung: From/To bắt buộc, lấy trọn ngày, tối đa 1096 ngày; AsOf tùy chọn,
mặc định ngày hiện tại từ IDateTimeProvider và không được ở tương lai;
DateBasis `0=Requested`, `1=Promised`; CustomerId/ProductId/ManagerId tùy chọn,
không nhận Guid.Empty. Details thêm Page >= 1, PageSize 1..200 (mặc định 50),
Status tùy chọn. FE gửi cùng filter cho cả ba endpoint; Status chỉ lọc drawer,
không làm thay đổi header/charts. Filter không hợp lệ trả 400; controller từ
chối role không có quyền qua Authorize. Handler cũng fail closed khi gọi trực tiếp.

## Nguồn và giới hạn

RequestedDate = MerchandiseOrderDetail.DeliveryRequestDate.
PromisedDate = MAX MfgProductionOrder.ExpectedDate của từng dòng ĐHG qua MfgOrderPO.
Chỉ lấy link active, dòng/header ĐHG active, MFG active cùng company và không
Canceled/Cancelled theo rule điều vận. Không lấy MerchandiseOrderDetail.ExpectedDeliveryDate,
không dùng RequiredDate/ManufacturingDate và không fallback sang ngày yêu cầu khi tính KPI. Với Promised thiếu ngày,
chỉ dùng ngày yêu cầu để đưa dòng chẩn đoán vào kỳ; DueDate vẫn null. Dòng đã giao đủ đến AsOf được quy ước OnTime,
tính vào tử/mẫu OTD và đủ 100% lượng đúng hạn; dòng chưa đủ vẫn MissingDueDate,
không tính trong mẫu số. Không suy ra ngày hứa hoặc ghi ngày giả vào dữ liệu. Requested chọn kỳ theo ngày yêu cầu; Promised chọn theo ngày
hứa (ngoại lệ chẩn đoán trên). Không giới hạn ngày tạo ĐHG.

Lượng giao lấy DeliveryOrderDetail.Quantity > 0, active, không IsAttach, liên kết
đúng dòng ĐHG; header active, cùng company, Status khác Canceled.
Cộng cả Pending/InProgress/Completed, chỉ loại phiếu hủy theo status; không dùng RealQuantity hoặc trạng thái
Delivered của ĐHG làm bằng chứng giao đủ. Ngày của mỗi lần giao là
DeliveryOrder.CreatedDate, không phải UpdatedDate, ngày nhận POD hoặc ngày chuyển
Completed. Phiếu thiếu CreatedDate không được tính. Không nối lot nên không nhân
lượng khi một dòng có nhiều lot. Tên khách/sản phẩm là snapshot trên ĐHG/dòng.

Metadata header ghi EvaluationUnit=OrderLine,
DeliveryDateSource=DeliveryOrderCreatedDate,
IsCustomerReceiptConfirmed=false, IsHistoricalSnapshot=false. FE cần hiển thị
nguồn này: chỉ số chưa chứng minh khách nhận hàng đúng hạn. Lượng giao trước kỳ
vẫn được cộng nếu ngày tạo phiếu <= AsOf. Lịch sử dựa trên dữ liệu hiện tại: phiếu
được cập nhật hôm nay có ngày tạo cũ vẫn xuất hiện khi xem AsOf cũ; không thể tái dựng
trạng thái, ngày hứa, lượng hoặc tạm dừng tại thời điểm quá khứ.

Không dùng DeliverySchedules vì tài liệu hiện hành chưa xác nhận bảng/luồng ghi
runtime. Chưa đánh giá nhiều đợt, chưa suy diễn phân bổ FIFO. Cần triển khai schema
và luồng ghi/xác nhận nhận hàng riêng trước khi nâng thành OTD từng đợt thực nhận.

## Công thức và trạng thái

Q = ExpectedQuantity; D = DueDate; T = AsOf. C là ngày tạo phiếu đầu tiên mà lượng
phiếu giao không bị hủy cộng dồn theo ngày đạt Q. Các lần giao sau T bị loại.

- DeliveredQuantity: tổng lượng hợp lệ đến T, có thể vượt Q.
- OnTimeQuantity: min(Q, tổng lượng đến min(D,T)); thiếu D nhưng đã giao đủ đến T thì bằng Q.
- RemainingQuantity: max(0, Q - DeliveredQuantity).
- DueLines: các dòng có D <= T, cộng dòng thiếu D nhưng đã giao đủ đến T.
  Ngày đến hạn tương lai và dòng thiếu D chưa giao đủ không ở mẫu số.
- OtdPercent: OnTimeLines / DueLines * 100.
- QuantityOtdPercent: tổng OnTimeQuantity của dòng đến hạn / tổng Q đến hạn * 100.
- OverdueRemainingQuantity: tổng RemainingQuantity với D < T và còn thiếu.
- LateDays: max(0, C - D), null khi thiếu C hoặc D.
- OverdueDays: max(0, T - D) khi còn thiếu; còn lại 0.
- AverageLateDays: trung bình LateDays của dòng giao đủ trễ, không gồm dòng đang thiếu.

Tỷ lệ và trung bình làm tròn 2 chữ số bằng Math.Round mặc định; mẫu số 0 trả null,
trung bình không có dòng trễ trả null; 0 là giá trị tính được. Danh sách rỗng là
không có dữ liệu, không tự tạo điểm đồ thị giá trị 0.

Status numeric: 0 MissingDueDate; 1 NotDue; 2 OnTime; 3 Late;
4 OverdueIncomplete. Thiếu D nhưng có C là OnTime theo quy ước,
OnTimeQuantity = Q; thiếu D và chưa có C là MissingDueDate. MissingDueDateLines
chỉ đếm nhóm chưa đủ này (khớp bộ lọc details). Có C <= D là OnTime; có C > D là Late; chưa đủ và D < T
là OverdueIncomplete; chưa đủ và D >= T là NotDue. Ngày đến hạn đúng T chưa quá
hạn nhưng vẫn nằm trong DueLines; KPI ngày hiện tại còn thay đổi đến hết ngày.
NotDueLines trong summary đếm D > T, không đếm dòng đến hạn đúng hôm nay.

Charts mỗi điểm trả Key/Label và Summary cùng công thức header. Monthly nhóm theo
tháng DueDate; dòng thiếu DueDate đã giao đủ nhóm theo tháng RequestedDate; customers/products nhóm theo ID; statuses nhóm theo mã enum;
aging chỉ gồm OverdueIncomplete với các bucket 01-03, 04-07, 08-14, 15+ ngày.
Dòng MissingDueDate chưa giao đủ không nằm trong monthly; có thể nằm trong nhóm khách/sản phẩm.
Không tính trung bình các tỷ lệ nhóm để ra OTD tổng: cộng tử/mẫu rồi chia.

Details sắp DueDate rồi DetailId, phân trang sau tính; totalCount theo Status
nếu có. Header/charts chưa phải snapshot chung giữa các request, dữ liệu có thể
thay đổi khi phiếu/ĐHG được sửa. Kỳ giới hạn nhưng query vẫn tính toàn bộ dòng
trong kỳ trước phân trang; cần đo tải runtime trước khi dùng cho công ty lớn.

## Ví dụ FE

`GET /api/v1/reports/delivery-otd/header?from=2026-10-01&to=2026-10-31&asOf=2026-10-14&dateBasis=0`

```json
{
  "from": "2026-10-01T00:00:00",
  "to": "2026-10-31T00:00:00",
  "asOf": "2026-10-14T00:00:00",
  "dateBasis": 0,
  "evaluationUnit": "OrderLine",
  "deliveryDateSource": "DeliveryOrderCreatedDate",
  "isCustomerReceiptConfirmed": false,
  "isHistoricalSnapshot": false,
  "summary": {
    "totalLines": 3, "dueLines": 2, "onTimeLines": 1, "lateLines": 0,
    "overdueIncompleteLines": 1, "notDueLines": 1, "missingDueDateLines": 0,
    "dueQuantity": 200, "onTimeQuantity": 160,
    "overdueRemainingQuantity": 40, "otdPercent": 50,
    "quantityOtdPercent": 80, "averageLateDays": null
  }
}
```

Bấm phần quá hạn gọi details cùng filter và `status=4&page=1&pageSize=50`.
Mỗi detail có ID dòng/ĐHG để điều hướng, các ngày yêu cầu/hứa/hạn/hoàn tất và
lượng giao/đúng hạn/còn thiếu để đối chiếu. Các ID đích vẫn phải kiểm tra quyền
trong API chi tiết; không xem quyền đọc report là quyền mutation.

## KPI giao đúng hạn theo PO trong header

Header thêm `poSummary` bên cạnh `summary` theo dòng. Thẻ tỷ lệ theo PO dùng
`poSummary.poOtdPercent`, tử số `onTimePos`, mẫu số `evaluatedPos`.
`summary.otdPercent` vẫn là tỷ lệ theo dòng, không dùng cho thẻ theo PO.

Các bộ lọc và DateBasis chọn dòng như hiện hành; các PO có ít nhất một dòng
trong kết quả được chọn, mỗi MerchandiseOrderId chỉ tính một lần. Sau khi chọn PO,
KPI xét toàn bộ dòng hợp lệ của PO, kể cả dòng nằm ngoài kỳ hoặc ProductId đã lọc.
Ngày hứa PO là MAX ExpectedDate của MFG active qua link active, cùng company,
không hủy; luôn dùng nguồn ngày hứa MFG, kể cả khi DateBasis chọn kỳ theo yêu cầu.
Ngày giao PO là MAX DeliveryOrder.CreatedDate của phiếu hợp lệ đến AsOf trên
các dòng PO, không dùng ngày phiếu cũ hoặc ngày đầu tiên lượng cộng dồn đạt đủ.
Bỏ phiếu hủy, inactive, attachment, lượng <= 0 và phiếu sau ngày chốt theo rule đọc dòng.

- PO đang hoặc từng tạm dừng luôn đạt, kể cả chưa giao, giao trễ hoặc ngày hứa
  sau AsOf; tính cả tử và mẫu. Nhận diện bằng IsDeliveryPaused=true hoặc còn
  DeliveryPausedFrom/DeliveryPausedTo/DeliveryPausedBy, hoặc DeliveryPauseReason/
  DeliveryPauseType khác null và không chỉ whitespace. Cờ hiện tại đã false vẫn
  đạt nếu còn dấu vết. Ưu tiên ngoại lệ này trước ngày hứa/ngày giao. Không giới
  hạn thời điểm tạm dừng theo AsOf vì đây là rule từng có dữ liệu tạm dừng.
  Không có bảng lịch sử riêng: nếu dấu vết bị xóa hết thì không thể nhận diện.
- PO thiếu ngày hứa luôn đạt, kể cả chưa có phiếu giao; tính cả tử và mẫu.
- Có ngày hứa <= AsOf: phiếu mới nhất <= ngày hứa là đạt, > ngày hứa là trễ.
- Có ngày hứa <= AsOf nhưng chưa có phiếu giao: chưa đạt (`pendingPos`).
- Ngày hứa > AsOf và không có dấu vết tạm dừng: chưa đánh giá, không tính trong mẫu số.
- Rule PO không yêu cầu giao đủ lượng; đây là KPI theo ngày phiếu mới nhất do
  nghiệp vụ quy định, khác KPI giao đủ theo dòng.

`totalPos` gồm tất cả PO được chọn; `evaluatedPos = onTimePos + latePos + pendingPos`.
`poOtdPercent = onTimePos / evaluatedPos * 100`, làm tròn 2 chữ số; mẫu số 0 trả null.
Ví dụ: `"poSummary": { "totalPos": 6, "evaluatedPos": 5, "onTimePos": 3,
"latePos": 1, "pendingPos": 1, "poOtdPercent": 60 }`.
Charts/details tiếp tục theo dòng; nhóm OnTime của PO không phải bộ lọc status của details.

## Tổ chức implementation

Feature nằm trong `Features/Reports/DeliveryOtd`, không gộp mọi trách nhiệm vào một file:
- `DeliveryOtdReader.cs`: truy vấn dữ liệu theo company, nguồn MFG, phiếu giao và dấu tạm dừng.
- `DeliveryOtdPoCalculator.cs`: rule KPI PO, thứ tự ưu tiên ngoại lệ và tử/mẫu.
- `DeliveryOtdCalculator.cs`: KPI, trạng thái và biểu đồ theo dòng.
- `DeliveryOtdContracts.cs`: DTO/filter/enum; các handler trong `Queries` nối flow đọc và tính.
- README này mô tả contract và rule; test tại `HRM.Application.Tests/Features/Reports/DeliveryOtdTests.cs`.

Khi nâng cấp rule PO, sửa calculator và test tương ứng; khi đổi nguồn dữ liệu,
sửa reader và test scope. Ngoại lệ tạm dừng PO không thay đổi KPI/details theo dòng.
FE tooltip tỷ lệ PO cần thêm: “PO đang hoặc từng có dữ liệu tạm dừng mặc định đạt”.
