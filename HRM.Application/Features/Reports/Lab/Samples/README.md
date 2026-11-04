# Báo cáo mẫu Lab

`GET /api/v1/reports/lab/samples` — tổng quan và xu hướng tháng.
`GET /api/v1/reports/lab/samples/items` — drawer, phân trang trên server.

Ví dụ: `?From=2026-10-01&To=2026-10-12`; drawer thêm `Metric=Won&PageNumber=1&PageSize=20`.
From/To bắt buộc, overview lọc theo `SampleRequest.CreatedDate`, gồm trọn ngày To bằng khoảng
`[From 00:00, To+1 00:00)`. Trend có ngày bắt đầu riêng; không bị From của overview giới hạn.
`timezone=Asia/Ho_Chi_Minh` (UTC+7). Các cột nguồn là `timestamp without time zone`, được đọc như
giờ địa phương nghiệp vụ. Boundary và GROUP BY tháng dùng cùng local wall-clock, không đổi midnight
sang UTC trước khi so sánh với cột local. Không thay timestamp ghi bởi các workflow hoặc backfill ngày cũ.
CompanyId chỉ kiểm tra/thu hẹp về công ty current user. CustomerId, SaleEmployeeId (`ManagerBy`),
RequestType và LabReceivedByEmployeeId là filter tùy chọn. LabReceivedBy là người ghi nhận nhận mẫu,
không phải một nguồn phân công Lab; hệ thống chưa có nguồn phân công riêng được xác minh.

## Scope và quyền

Authorize; current user phải có company và employee hợp lệ. Dùng `BuildReadScopeAsync` và
`ApplySampleRequestVisibility` như báo cáo Sample Request hiện hành, giữ company/customer ownership/team.
Filter không mở quyền. Loại yêu cầu inactive, status `Cancelled`, khách inactive và khách `KH_VIETAUS`.
Chỉ query field thường: identity, status, ngày và số trial; không query giá, NVL hoặc nội dung kỹ thuật.
Không thêm capability hoặc thay role mapping. Không sửa workflow hoặc API Sales/Purchasing.

## Nguồn và công thức đã thống nhất

- **Development Lead Time**: ngày Lab approve trừ ngày lần đầu gán mã màu.
  Start = MIN `AuditLogs.ChangedAt` của Product liên kết, schema `SampleRequests`, table `Products`,
  company chính xác; `OldValues.ColourCode` trống/null và `NewValues.ColourCode` có giá trị.
  Một Product dùng lại có thể có mốc gán mã màu trước ngày tạo yêu cầu; đây là mốc phát triển Product.
  End = MIN `FormulaVersion.CreatedAt`, `Status=Approved` và `ChangeReason=Status changed to Approved`.
  Snapshot này được `UpdateFormulaStatusCommandHandler` tạo ngay khi Lab duyệt. Chỉ xét Formula cùng company
  đã chọn trên yêu cầu hoặc liên kết qua trial active. Không ghép bằng customer/product.
  Không dùng CheckDate (có thể ghi lại khi đổi NVL/gửi mẫu), FinishedDate, UpdatedDate hoặc Product.CreatedDate.
  Thiếu snapshot hoặc liên kết Formula trực tiếp thì thiếu dữ liệu, kể cả Formula cùng Product đã Approved.
  Cả hai mốc phải trước To+1. Ngày lịch, có phần lẻ; bình quân chỉ tính cặp end >= start.
  Trả valid/missing/invalid riêng; thiếu một mốc thuộc missing; có đủ nhưng end < start thuộc invalid.
  `leadTimeEligibleCount = valid + missing + invalid`: hiện toàn bộ yêu cầu hợp lệ trong cohort đều được
  xét Lead Time nên bằng requestCount; không lấy requestCount để gán mặc định. `leadTimeUnavailableReason`
  null khi có cặp hợp lệ (kể cả thời gian thực sự bằng 0), `no_requests` khi cohort rỗng, `no_valid_dates`
  khi có yêu cầu nhưng không có cặp ngày hợp lệ. Không trả nhãn UI hoặc số đã format.
- **Order Win Rate** theo nghiệp vụ công ty = số yêu cầu đã được khách duyệt để lên PO / số yêu cầu hợp lệ
  tạo trong kỳ. `conversionBasis=customer_approval_for_po`: đây là proxy nghiệp vụ đã được chốt với user,
  không khẳng định MerchandiseOrder thực tế đã tồn tại.
  Nguồn là audit SampleRequests của chính yêu cầu, trạng thái trước khác Completed, trạng thái sau Completed,
  ngày audit >= CreatedDate và < To+1. Nhiều trial/lần duyệt vẫn đếm một yêu cầu.
  Mẫu mới/đang xử lý nằm trong mẫu số. Audit hoàn tất sau To không tính win.
  Completed hiện tại nhưng không có audit hoàn tất hợp lệ: conversionUnknownCount tăng; tỷ lệ toàn nhóm null
  với `missing_completed_audit`. Nếu có audit hợp lệ sau To thì biết kết quả xảy ra ngoài kỳ, không coi là thiếu.
  Không fallback từ CustomerReplyDate vì user được nhập ngày phản hồi; không đoán PO từ sản phẩm/khách hàng.
- Dashboard dùng requestCount cho số yêu cầu, winNumerator cho khách duyệt lên PO,
  winPercentage cho tỷ lệ và developmentLeadTimeDays cho thời gian phát triển.
  winDenominator là số yêu cầu của chính cohort; conversionUnknownCount và hai reason phục vụ
  giải thích KPI null. Nhóm leadTimeValid/Missing/Invalid/EligibleCount phục vụ cảnh báo chất lượng ngày.
  Response tổng quan/tháng đã bỏ trialCount, processingCount, completedCount, sentCount,
  otherStatusCount, waitingFeedbackCount, acceptedCount và rejectedCount vì dashboard không dùng.
  winNumerator dựa audit hoàn tất trước cutoff, không suy diễn từ status hiện tại hoặc trial Approved.

Tỷ lệ và ngày bình quân làm tròn 2 chữ số, midpoint away from zero. Không có yêu cầu: count=0,
percentage=null (`no_requests`). Có yêu cầu nhưng không win và không thiếu lịch sử: percentage=0.
Không có cặp ngày hợp lệ: DevelopmentLeadTimeDays=null.

## Biểu đồ và drawer

overview tính từ toàn bộ cohort CreatedDate nằm trong From/To, không cộng 6 tháng để dựng overview.
months tính độc lập cho 6 tháng lịch kết thúc tại tháng To: từ ngày 1 của tháng To-5 đến hết To.
Ví dụ From=01/10/2026, To=12/10/2026: overview chỉ 01–12/10; months tháng 05–09 đầy đủ,
tháng 10 tính 01–12/10. Mọi business filter, company/ownership scope, công thức và cutoff To+1 giống nhau.
Mỗi tháng nhóm theo tháng CreatedDate; thời điểm chốt kết quả duyệt/approve vẫn là To chung của báo cáo,
không đổi sang cuối từng tháng. periodTo là ngày cuối được xét cho **ngày tạo yêu cầu** của tháng đó.

Response dùng overviewPeriod cho phạm vi overview, trendPeriod cho phạm vi trend; không trả from/to trùng ở cấp ngoài,
monthCount luôn 6. Months luôn đủ 6 phần tử tăng dần, kể cả cohort rỗng. To trước 01/06 năm 0001 bị từ chối
vì không biểu diễn được đầy đủ cửa sổ 6 tháng. periodFrom là ngày 1, periodTo là cuối tháng hoặc To nếu To
sớm hơn; isPartialMonth=true khi periodTo trước ngày cuối tháng, kể cả To là ngày 1.

dataStatus=available khi requestCount>0; no_requests khi query thành công và requestCount=0.
dataUnavailableReason=null trong cả hai trường hợp. unavailable chỉ dành cho nguồn thực sự không khả dụng,
không dùng khi rỗng hoặc chỉ thiếu ngày; implementation hiện fail request theo error contract nếu DB query lỗi,
không giả tạo tháng không có dữ liệu. Metrics thiếu ngày vẫn có dataStatus=available.
Tháng rỗng trả mọi count=0, winPercentage=null, winUnavailableReason=no_requests,
developmentLeadTimeDays=null, leadTimeUnavailableReason=no_requests. Có yêu cầu nhưng không win và
không thiếu lịch sử: winPercentage=0. FE không thay null bằng 0 trên chart tỷ lệ/lead time.

Overview tính bình quân từ cặp ngày gốc hợp lệ trong SQL; tỷ lệ từ numerator/denominator gốc.
Không lấy trung bình đơn giản winPercentage hoặc lead time đã làm tròn của các tháng. Drawer giữ filter
From/To của chính request, không tự chuyển sang trend; muốn xem cohort tháng truyền khoảng ngày phù hợp.

Metric drawer: All, Processing, Completed, Sent, WaitingFeedback, Accepted, Rejected, Won,
LeadTimeValid, LeadTimeMissing, LeadTimeInvalid, ConversionUnknown, OtherStatus (enum hoặc số tương ứng).
TotalCount đếm tập bản ghi theo Metric; các filter trạng thái/trial vẫn dùng được trong drawer dù
không còn trả count ở dashboard. Với Won/LeadTime/ConversionUnknown, count khớp KPI tương ứng
khi cùng filter và thời điểm đọc. PageNumber >= 1,
PageSize 1..100; thứ tự CreatedAt giảm dần, SampleRequestId để ổn định trang.
`approvedForPo`: true có audit trong cutoff, false chưa có audit trong kỳ nhưng không thiếu dữ liệu đã biết,
null lịch sử không đủ. `relatedOrderCodes=null` vì chưa có liên kết mẫu→PO trực tiếp được xác minh.
Tên khách/sản phẩm/người là master data hiện tại. Các ngày nguồn được trả để đối soát.

Response rút gọn:

```json
{"timezone":"Asia/Ho_Chi_Minh",
 "overviewPeriod":{"from":"2026-10-01","to":"2026-10-12"},
 "trendPeriod":{"from":"2026-05-01","to":"2026-10-12","monthCount":6},
 "conversionBasis":"customer_approval_for_po",
 "overview":{"requestCount":10,"leadTimeValidCount":6,
 "leadTimeMissingCount":3,"leadTimeInvalidCount":1,"developmentLeadTimeDays":2.25,
 "leadTimeEligibleCount":10,"leadTimeUnavailableReason":null,
 "winNumerator":4,"winDenominator":10,"winPercentage":40,"conversionUnknownCount":0},
 "months":[{"year":2026,"month":10,"periodFrom":"2026-10-01","periodTo":"2026-10-12",
 "isPartialMonth":true,"dataStatus":"available","dataUnavailableReason":null,
 "metrics":{"requestCount":10,"winPercentage":40}}]}
```

Thực tế months trả đủ 6 tháng và mỗi metrics có cùng toàn bộ field với overview; ví dụ trên lược bớt tháng/field.
Drawer trả items/totalCount/pageNumber/pageSize/totalPages/hasPreviousPage/hasNextPage.

## Dữ liệu lịch sử và hiệu năng

Kiểm tra DB read-only ngày 12/10/2026: 936 audit có cặp giá trị mã màu trống→có giá trị, 417 audit
có snapshot Status=Completed, 629 FormulaVersion ghi Status changed to Approved. Các count này là nguồn
toàn DB, chưa áp scope/cutoff/report validation; không phải count KPI. Có 12.523 yêu cầu Completed hiện tại,
nên lịch sử completion không đầy đủ. Không backfill, migration hoặc suy đoán ngày còn thiếu.
Audit CompanyId=null được loại để fail closed. Audit cũ chỉ có ChangedValues nhưng không có
snapshot OldValues/NewValues không được tự đoán. Trial inactive không dùng làm bằng chứng Formula.

Read-only AsNoTracking + projection. Hai aggregate query riêng cho overview và trend trên cùng query nguồn
đã scope, không materialize từng yêu cầu để tổng hợp; drawer COUNT và LIMIT/OFFSET
trong DB, không N+1. Audit lookup tương quan dùng RecordId/company/schema/table; độ nhanh thực tế phụ thuộc
index và cardinality. Không thêm index/schema. Test PostgreSQL read-only đối soát 1.468 yêu cầu từ
01/05–12/10/2026: monthly query khoảng 0,87 giây trong lần đo đầu. Đây là thời gian query test,
không phải SLA hoặc benchmark HTTP endpoint; scope thực tế/cache/network có thể khác.
