# Hàng đợi công việc mẫu theo vai trò

## Nguồn dữ liệu và ranh giới

### Chẩn đoán thời gian endpoint daily-work

Ở cả Development và môi trường triển khai, `reports/daily` trả header `Server-Timing` sau khi xử lý thành công cho người đã đăng nhập, gồm thời gian
count, chọn trang/khách, tải payload, kiểm tra quyền của trang, dữ liệu giá và workflow.
Đồng hồ bắt đầu trước khi truy vấn được enumerate, nên bao gồm cả EF dịch/biên dịch query;
không gọi `ToQueryString` trước để làm nóng và bỏ sót chi phí request đầu.
`daily_total` đo tới khi handler hoàn tất, chưa bao gồm serialize/gửi HTTP response.
Chỉ có tên bước cố định và số mili giây; không có SQL, keyword, ID, actor, ghi chú hay giá.
Các phase chi tiết chạy khi có audience hoặc workflowDetail; request khác vẫn có `daily_total`.
Trong Network → chọn request → Timing, xem `samples_count`, `samples_page-selection`,
`samples_page-facts`, `samples_payload`, `samples_page-formulas`, `samples_page-permissions`,
`samples_standard-prices`, `samples_workflow-prices`, `samples_workflow-state` và các bước `report_*`.
`report_sample-query` bao gồm các bước `samples_*`; `daily_total` bao gồm tất cả, không cộng các tổng lồng nhau.
Log `Sample daily query timings` cũng chỉ chứa thời gian.
Các tag SQL cố định `SampleDaily.Count`, `SampleDaily.CustomerPriority`, `SampleDaily.Payload`
giúp đối chiếu PostgreSQL với từng bước; không chứa giá trị request.

`SampleWorkDecisionQuery` và `SampleWorkDecisionRules` quyết định bước nghiệp vụ duy nhất từ hồ sơ, VU, lần gửi mới nhất, giá chuẩn đúng nguồn và các sự kiện đã lưu. Điều kiện nhóm được áp trước count/phân trang; `SampleWorkAssignment` quyết định người xem có được thao tác hay chỉ thấy bước đang chờ. Không suy việc cần làm từ thông báo, màu thẻ hoặc nhãn trạng thái riêng lẻ.

Mọi hàng đợi giữ authenticated user, employee/company và customer visibility hiện hành; hồ sơ inactive/khách nội bộ bị loại. Xem được hồ sơ không đồng nghĩa được sửa. SaleUser thao tác phản hồi/quyết định trên hồ sơ mình phụ trách. SaleAdmin có capability riêng `plm.sample-request.workflow.sale.managed` được làm thay Sale trong `BuildScopeAsync().EmployeeIdsInScope`, đồng thời phải qua write visibility của chính hồ sơ và actor active cùng company. Quyền xem toàn công ty/đọc báo cáo không mở rộng phạm vi làm thay. Bộ lọc Team Lab chỉ chọn mặc định/phân loại, không mở rộng company/customer scope. Người có quyền mở queue được chọn Tất cả trong phạm vi nhìn thấy.

Ngoại lệ cục bộ cho quản lý mẫu: LabAdmin active cùng company được đọc toàn bộ hồ sơ mẫu để đặt lịch/rà tiến độ, kể cả không là Sale phụ trách. Scope planning chỉ áp dụng trong module mẫu (queue LabAdmin, workflow/chi tiết/lịch sử của hồ sơ và command lịch), không cấp quyền đọc CRM toàn cục hoặc quyền sửa kỹ thuật. Command lịch vẫn cần capability riêng; đọc chi tiết không nâng `CanEdit`/write scope. Khách nội bộ bị loại theo cả ID và mã; company boundary không đổi. Developer mô phỏng vẫn giao với visibility thật của Developer sau scope planning nhân viên đích.

Các bucket công việc dùng [WORK_SCOPE.md](WORK_SCOPE.md): hồ sơ từ **01/11/2026** hoặc hồ sơ cũ được Admin chọn theo dõi tiếp; việc chưa xong tự tồn sang tháng sau. `all` phục vụ tra cứu nên không áp cutoff vận hành. Chọn theo dõi tiếp không chuyển hồ sơ legacy sang luồng chip/kg. `MyActionRequired` của Sale giao owner cá nhân với write visibility trước count/phân trang, áp dụng như nhau cho SaleUser và SaleAdmin. Hàng đợi tra cứu vẫn dùng read visibility chung; hồ sơ ngoài phạm vi làm thay không có nút ghi.

## Phân trang trọn nhóm khách hàng

`MyActionRequired` chỉ lấy `ManagerBy == current EmployeeId`, kể cả SaleAdmin được làm thay cấp dưới; vẫn giao với allowed owners và write visibility. `ActionRequired` lấy toàn bộ việc Sale đang chờ xử lý trong read scope team hiện hành, không ép owner về người đăng nhập. Quyền làm thay trên từng hồ sơ giữ nguyên và không được dùng để mở rộng bộ lọc cá nhân.

`GET /api/v1/plm/sample-requests/reports/daily?groupByCustomer=true` phân trang theo khách hàng
thay vì từng TP. Sau mọi điều kiện company, quyền xem, team, cutoff vận hành, bucket và tìm kiếm,
BE chọn một trang CustomerId rồi trả toàn bộ TP **còn khớp các điều kiện đó** của các khách đã chọn.
Không mở rộng thành tất cả TP của khách khi các TP khác không khớp bộ lọc hoặc quyền xem.
CustomerId được trả trong DTO để FE không gộp nhầm khách trùng tên/mã.
Tìm mẫu trên daily-work hỗ trợ mã khách hàng (`KH_155`) bên cạnh tên khách, mã TP và mã màu;
dấu `_` được tìm theo ký tự thật, không phải wildcard. Áp dụng tại DB trước đếm/chọn trang,
trong cùng company/customer scope, cho cả Lab, LabAdmin và Sale.

`sortBy=WorkPriority` (mặc định trên daily-work) ưu tiên việc người xem thực hiện được,
trong đó Lab: hoàn thành VU để lên đơn → gửi mẫu/chip → việc Lab còn lại;
Sale: hoàn thành hồ sơ lên đơn → phản hồi chip/mẫu khách hàng → phản hồi giá và việc Sale còn lại.
Ghim cá nhân luôn đứng trước các mức ưu tiên. Cả SQL và đường facts chọn nhóm khách áp dụng
thứ tự này trước phân trang, giữ nguyên capability và scope. Sau các việc thực hiện được,
rồi việc chờ người khác, đã hoàn thành và đã hủy. Trong mỗi loại, việc Lab quá hạn lên trước,
sau đó đến hạn sớm nhất; chưa có hạn xếp cuối. Phân quyền và trách nhiệm dùng cùng capability,
audience và phạm vi Sale của read model; sắp xếp không cấp thêm quyền thao tác.
Giá trị bỏ trống/không nhận diện được quay về WorkPriority; không phân biệt hoa thường.
`sortBy=Newest` luôn phân trang theo TP (bỏ qua `groupByCustomer=true`), ghim cá nhân trước,
rồi ngày tạo và ID giảm dần. SQL sắp xếp trước Skip/Take; không dùng ngày cập nhật hoặc ưu tiên
workflow thay ngày tạo. `bucket=all` dùng đường đọc nhẹ, không tính rank workflow cho toàn bộ tập.
Các bucket/team/quyền xem vẫn giữ nguyên. FE giữ lựa chọn gộp trước đó để khôi phục khi rời Newest.
Riêng Lab/Newest/ActionRequired: đọc một stream facts hẹp đã scope, SQL xếp ghim/ngày tạo/ID,
lọc quyết định Lab chuẩn trước đếm và chọn trang, chỉ giữ facts của đúng trang trong bộ nhớ.
Không chạy Count với CASE workflow rồi đọc lại PageFacts. Tag `SampleDaily.NewestFactsOnce`;
`samples_page-selection` bao gồm lượt facts dùng chung, không thêm polling, cache hoặc schema.
Khi gộp khách, lấy **công việc ưu tiên nhất của từng khách** làm vị trí cả nhóm rồi sắp TP trong nhóm.
`sortBy=CustomerCode` xếp mã khách tăng dần (chưa có mã xếp cuối), CustomerId phân biệt mã trùng;
TP trong từng khách vẫn theo ưu tiên công việc. Thứ tự luôn được tính trước khi chọn trang khách.
Riêng nhóm khách theo WorkPriority, BE đọc một lượt metadata đã lọc (`CustomerId`, mã khách,
rank, quá hạn, hạn dự kiến), stream và giữ đúng một tuple tốt nhất/khách trong bộ nhớ;
không tải toàn bộ DTO, ghi chú hay giá và không gọi truy vấn riêng cho từng khách.
Luồng metadata giữ thứ tự mã/ID của PostgreSQL để bảo toàn collation, null và tie-break.
Sau khi chọn CustomerId thuộc trang, truy vấn payload vẫn dùng lại toàn bộ scope/bộ lọc ban đầu.
Bộ nhớ bước chọn trang tỷ lệ số khách, không tỷ lệ số TP. CustomerCode vẫn chọn ID trang trực tiếp bằng SQL.
Route sample-trials và các báo cáo khác không nhận tùy chọn nội bộ này, giữ thứ tự cũ.

Daily-work có audience/sort và không lấy lịch sử lần thử tách bước đọc payload của trang:
chốt chính xác cặp SampleRequestId/TrialId từ query đã scope/lọc/sắp xếp, đọc facts của đúng
những cặp này rồi dùng cùng `SampleWorkDecisionRules.Resolve` trong bộ nhớ. Payload lấy từ
base query đã có scope và đúng các khóa trang; không lặp cây CASE quyết định cho từng trường DTO.
Thông tin công thức của trang được đọc theo batch, vẫn kiểm tra company/product/active và giữ
nguyên cách chọn VU legacy/lần gửi/xác nhận. Thứ tự khóa trang được khôi phục sau đọc; không có
truy vấn cho từng TP. Báo cáo cũ và chế độ lịch sử giữ nguyên đường đọc cũ.
`samples_page-facts`/`samples_page-formulas` trong Server-Timing tách hai bước này. Test hiệu năng
đo lần enumerate đầu, trước ToQueryString/EXPLAIN, với bảng công thức/giá có dữ liệu nền;
số đo chỉ thuộc bước payload, không thay cho phép đo toàn endpoint HTTP.

Riêng daily-work `bucket=All&groupByCustomer=true&sortBy=WorkPriority` đọc facts hẹp một lần
để vừa chọn khách vừa giữ dữ kiện của đúng trang; không chạy lại PageKeys/PageFacts sau chọn khách.
Quyết định và ưu tiên dùng cùng rule trong bộ nhớ, quyền Sale vẫn được kiểm tra qua writable scope.
Luồng stream theo mã/ID khách của PostgreSQL; chỉ giữ các nhóm thuộc tiền tố trang và nhóm đang đọc,
không cache toàn bộ DTO/ghi chú/giá hoặc giữ dữ kiện giữa các request. Khi khách đến sau có ưu tiên
cao hơn, thay cả nhóm, không tách TP của một khách. Những bucket, thứ tự khác và lịch sử giữ đường
đọc trước đó. `samples_page-selection` lúc này gồm lượt facts dùng chung; `samples_page-facts`
gần như không còn chi phí truy vấn trong fast path này. Benchmark hai bước ghi ở
`artifacts/daily-payload-performance/facts-once.txt`, không phải thời gian toàn endpoint.

Khi bật tùy chọn này, `totalCount`, `pageSize`, `totalPages` và `hasNextPage` có đơn vị **khách hàng**;
`items` là các TP nên có thể nhiều hơn pageSize. FE dùng metadata phân trang để tải khách tiếp theo,
không suy trang tiếp từ số TP trong items. Tắt/bỏ groupByCustomer giữ phân trang và tổng số TP cũ.
Không sửa PagedResult dùng chung, quyền thao tác hoặc dữ liệu DB; không cần migration.

## Vai trò và kết quả thao tác

| Người thao tác | Khi có việc/nút chính | Thao tác và kết quả |
|---|---|---|
| LabUser | Bước chuẩn bị/duyệt/làm lại VU, gửi chip/kg hoặc chốt VU chip; có capability Lab | Đặt tên/mã khi thiếu dữ liệu sản phẩm trước khi tạo VU; tạo/sửa/xác nhận VU theo trạng thái được phép. Gửi chip/kg tạo bằng chứng lần gửi đúng loại; chốt chip hoàn thành VU, chưa hoàn thành hồ sơ. |
| SaleUser phụ trách | Đã có giá chuẩn cần báo khách, cần ghi nhận phản hồi giá, lần gửi đang chờ phản hồi, hoặc khách duyệt chip/VU đã chốt | Ghi ngày báo giá thực tế, sau đó ghi khách đồng ý/không đồng ý/chưa phản hồi kèm dữ liệu form. Phản hồi chip/kg qua form phản hồi. Sau chip đã chốt, chọn hoàn thành hồ sơ hoặc yêu cầu kg trên cùng hồ sơ. |
| SaleAdmin trong phạm vi quản lý | Cùng bước nghiệp vụ của Sale, sau kiểm tra capability và write scope theo Sale phụ trách | Làm thay các thao tác Sale được phép, ghi actor thực tế là SaleAdmin; không đổi owner. Không cấp thêm quyền duyệt/đọc giá riêng, chọn/sửa VU thủ công hoặc thao tác Lab. |
| LabAdmin | Hồ sơ mở thiếu lịch tương ứng hoặc cần rà dữ liệu không đủ bằng chứng | Đặt/chỉnh lịch qua `ScheduleLabWork`/`EditLabSchedule`; `ReviewSample` mở xem để rà soát, không tự sửa dữ liệu suy đoán. Mặc định không nhận việc kỹ thuật LabUser. Đặt lịch không đổi trạng thái hồ sơ/VU/Trial và không gửi thông báo. |
| Người duyệt giá theo quyền hiện hành | Chưa có giá chuẩn được duyệt đúng nguồn VU | Duyệt giá trong luồng giá hiện có; hàng đợi thể hiện chờ duyệt giá, không trao quyền duyệt cho Lab/Sale qua việc xem thẻ. |

Nhánh Báo giá không yêu cầu kg: Lab xác nhận VU → giá chuẩn → Sale ghi báo giá → phản hồi đồng ý giá → Lab hoàn thành VU → Sale hoàn thành hồ sơ. Ngày báo giá thực tế chỉ là bằng chứng đã báo khách, không thay phản hồi đồng ý giá và không tự hoàn thành hồ sơ.

Các cờ ghi của list/detail/flow/lịch sử sửa phản hồi dùng cùng quyết định write scope. Metadata `CanActForSaleOwner` là nội bộ, không serialize; false không được gán lại thành true khi wrapper daily tính lại phần hiển thị. `CanConfirmCustomerPrice`, `CanUpdateCustomerFeedback`, `CanCorrectCustomerFeedback`, nút phản hồi công khai và primary action không được suy từ role SaleAdmin đơn thuần. Rollout cần đồng bộ bốn capability Sale theo script triển khai riêng ngoài Migrations, rồi login/refresh để nhận claim mới; không tự sửa DB đang chạy.

Nhận mẫu chuẩn là thao tác bổ sung độc lập, không chiếm vị trí nút chính và không bắt buộc trước mọi bước. Delay không tự mở lại hồ sơ đã hoàn thành/hủy; không tự thay người chịu trách nhiệm của bước đang chờ. Không lấy lịch dự kiến thay cho ngày thực tế.

## Legacy, chip và mẫu kg

Contract chuyển bước/mutation chuẩn: [CHIP_SAMPLE_FLOW.md](../workflow/CHIP_SAMPLE_FLOW.md).

- **Trước cutoff:** Lab gửi mẫu theo legacy không bắt báo cáo Trial/kg/phản hồi giá mới; Sale hoàn thành bằng thao tác tường minh. Chọn VU khi lên đơn không tạo phản hồi giả hay thông báo phản hồi.
- **Chip, không yêu cầu kg:** gửi không chặn bởi giá; sau gửi khóa VU. Chờ Sale ghi phản hồi thực tế; Đạt/Không đạt đưa VU sang làm lại cho phép Lab chỉnh cùng VU. Không đạt thì gửi chip vòng mới. Đạt thì Sale chọn chạy mẫu kg / Hoàn thành để lên đơn / Tiếp tục theo dõi / Hủy yêu cầu phối mẫu. Chỉ chọn Hoàn thành để lên đơn mới giao Lab chốt VU, sau đó Sale hoàn thành hồ sơ. Theo dõi giữ việc tại Sale; hủy bắt buộc lý do. Lựa chọn được lưu theo VU và lần chip, không tự hoàn thành VU khi khách duyệt.
- **Kg > 0:** chỉ gửi khi khách đồng ý giá đúng VU. VU đã chốt chip được gửi kg nguyên nội dung, không cần mở sửa. Giữ xác nhận giá cũ khi chỉnh cùng VU theo quyết định nghiệp vụ. VU từng gửi kg không mở sửa thành phần; không đạt cần VU/hồ sơ mới. Không tạo BOM thử nghiệm trong luồng chính.
- Thiếu bằng chứng loại lần gửi trả `NeedsReview`, không tự suy chip từ kg yêu cầu hiện tại.

## Contract FE

`GET /api/v1/plm/sample-requests/reports/daily?audience=lab|sale|labAdmin&bucket=actionRequired` trả `PagedResult<SampleRequestSampleTrialReportDto>`.

```json
{"items":[{"sampleRequestExternalId":"TP_31082","nextActionCode":"ApproveFormula","responsibleRole":"Lab","primaryActionCode":"ScheduleLabWork","needsLabSchedule":true,"canEditLabSchedule":true,"isActionForCurrentUser":true,"supplementaryActionCodes":["EditLabSchedule"]}],"totalCount":1,"pageNumber":1,"pageSize":15}
```

Ví dụ là góc nhìn LabAdmin: bước kỹ thuật vẫn thuộc Lab nhưng nút cá nhân là đặt lịch. `nextActionCode`/`responsibleRole` mô tả bước nghiệp vụ; `primaryActionCode` là nút được phép cho góc nhìn hiện tại, null nghĩa không có nút chính. `supplementaryActionCodes=[]` nghĩa không có thao tác bổ sung. `needsLabSchedule` là giá trị tính: hồ sơ mở thiếu ngày báo giá dự kiến với nhánh Báo giá không kg, hoặc thiếu ngày giao mẫu dự kiến với nhánh còn lại. `canEditLabSchedule` là capability trên hồ sơ mở, không có nghĩa người đó được sửa công thức.

`needsLabSchedule` chỉ bật khi bước còn thuộc Lab hoặc LabAdmin; bước đang chờ Sale/duyệt giá không vào nhóm Cần lập lịch chỉ vì thiếu ngày. Tuy nhiên ở chế độ LabAdmin, hồ sơ còn mở và có `canEditLabSchedule` vẫn nhận nút `ScheduleLabWork` để cập nhật ngày đã có trong các nhóm theo dõi khác; `ReviewSample` giữ ưu tiên cho hồ sơ cần rà soát. `flow.allowedActions` vẫn là điều kiện chuyển bước từ BE; FE không tự mở quyền bằng role hoặc thời gian tạo. Quyền và concurrency được kiểm tra lại khi lưu. Các cờ là read model tính lại từ dữ liệu hiện tại, không phải trạng thái đã lưu riêng. Ngày dự kiến canonical nằm trên SampleRequest; xem [WORK_SCHEDULE.md](WORK_SCHEDULE.md) cho PATCH, clearFields và audit.

Hạn trên thẻ và nhóm Quá hạn dùng cùng thứ tự nguồn: Báo giá không kg dùng ngày báo giá dự kiến; nhánh còn lại dùng ngày giao mẫu dự kiến, rồi ngày Sale yêu cầu, cuối cùng ngày Lab nhận mẫu cộng số ngày giao mẫu hợp lệ. Lập lại lịch không sửa ngày Sale yêu cầu nhưng phải đổi hạn hiển thị và nhóm Quá hạn tương ứng. Chi tiết `dueDate`/`dueDateSource` và giới hạn fallback nằm trong README mục dữ liệu card Trial.

## Developer xem thử và thao tác theo nhân viên

Ngoài preview chỉ đọc, `GET /api/v1/dev/sample-work-preview/acting-identity?employeeId=…`
trả roles/permission của tài khoản thực để FE mở đúng màn làm việc. Chỉ Developer được cấp ngữ cảnh;
BE kiểm tra lại role Developer trong DB, actor/employee đích active cùng company, tài khoản đích
không khóa, có role SaleUser/SaleAdmin/LabUser/LabAdmin; được có thêm Developer trên chính tài khoản đích.
Quyền đích được giữ nguyên, không cộng từ actor. Nhiều tài khoản cùng employee
bị từ chối để tránh chọn nhầm quyền. Không cấp token/refresh token hoặc thêm quyền vào tài khoản.

Header `X-Sample-Work-Employee` trên API PLM/CRM/attachment/recipient được middleware xử lý sau
authentication và trước authorization: thay principal bằng user đích cùng roles/permission thật,
không hợp nhất quyền Developer. API gốc tiếp tục enforce scope, ownership, capability và field visibility.
Header giả mạo từ Sale/Lab hoặc dùng với API auth/dev bị 403. Trước request ghi phải lưu AuditLog
`dev.SampleWorkActingRequests`, ChangedBy là Developer thực, NewValues chứa target user/employee,
method/path/trace; không lưu payload/secret. Dấu vết là `DeveloperSampleWorkWriteAttempt`, kể cả thao
tác sau đó bị API từ chối; audit dữ liệu và workflow event gốc dùng employee đích. Tin/thao tác nghiệp
vụ phát sinh thật theo cùng API, không có nhánh bỏ kiểm tra. Không thêm migration.

`GET /api/v1/dev/sample-work-preview/options` liệt kê nhân viên active cùng company và persona LabUser/SaleUser/LabAdmin. `GET /api/v1/dev/sample-work-preview?employeeId=…&persona=LabUser` dùng cùng query/phân loại hàng đợi. Chỉ Developer đã đăng nhập với employee active đúng company được gọi; nhân viên đích cũng phải active cùng company.

Response có `readOnly=true`. Kết quả là giao của visibility nhân viên đích và visibility Developer đang xem, không mạo danh token hay mở rộng quyền thật. Session mô phỏng riêng trong scope request; pipeline chỉ cho hai query Daily/Trials, từ chối command và query khác. FE không hiện nút sửa/lưu/gửi từ simulator. Không phát thông báo, không ghi audit hoặc sửa dữ liệu khi xem thử.

## Triển khai và permission

Baseline `ManageSampleLabWorkflow` dành LabUser và các role hỗ trợ hiện hành Admin/Developer/President; **LabAdmin không tự được cấp việc kỹ thuật**. `ManageSampleWorkSchedule` mặc định LabAdmin/Admin/Developer. User có thêm role hoặc được cấp capability riêng vẫn được xử lý theo resolver quyền hiện hành; queue quản lý LabAdmin không biến thành queue kỹ thuật.

Không thêm bảng/cột, không cần `Add-Migration`/`Update-Database`. Với permission explicit, áp script claim hẹp có chủ đích rồi login/refresh JWT; token cũ không tự đổi. [seed-sample-work-schedule-permission.sql](../../deployment/seed-sample-work-schedule-permission.sql) thêm claim lịch. [revoke-labadmin-default-workflow-permission.sql](../../deployment/revoke-labadmin-default-workflow-permission.sql) tùy chọn gỡ đúng claim kỹ thuật cũ của role LabAdmin; phải rà trước nếu môi trường cố ý dùng claim đó. Không xóa claim LabUser/Admin hoặc membership người dùng. Các script không tự chạy lúc startup và chưa được agent chạy trên DB.

Kiểm chứng gồm classifier/bucket SQL trước phân trang, role/owner/company, lịch và audit trên PostgreSQL cô lập, Developer guard/read-only. Kết quả UAT trực tiếp và giới hạn phải ghi riêng trong tài liệu UAT; build/test không thay cho thao tác FE dưới từng tài khoản thật.

## Bằng chứng kiểm tra ngày 03/11/2026

- BE: **87/87** kiểm thử kết hợp đạt, gồm 17 tình huống truy vấn PostgreSQL cô lập thực tế và 15 kiểm thử bảo mật preview. Nhóm lịch đã chạy riêng trước đó **11/11** đạt; không cộng hai số như các bộ độc lập không trùng.
- FE: **65/65** kiểm thử mới nhất đạt; kiểm tra TypeScript cuối đã đạt.
- Bộ BE rộng sau parity **500/500** đạt; targeted parity 55 ca và PostgreSQL inactive-formula 1 ca đạt. Build API sau parity 0 lỗi. Những kết quả này có trước bản sửa scope planning LabAdmin cuối; kiểm thử lại planning đang chờ, chưa suy diễn là đã đạt.
- Production build FE 88 route đã thành công ở lần trước; không thay bằng chứng đó cho UAT phiên mới.
- Swagger API đang chạy tại cổng **7263** đã được đọc kiểm tra nhưng chưa có endpoint preview mới. Vì vậy chưa thể khẳng định đã UAT trực tiếp FE đủ ba persona trên bản BE mới. Cần người dùng tự Stop/F5 API rồi kiểm tra lại; agent giữ nguyên API/debugger đang chạy.
- Không chạy script permission/revoke trên DB, không migration, không restart API. Kiểm thử PostgreSQL dùng cơ sở dữ liệu cô lập, không sửa dữ liệu nghiệp vụ thực.

Các mục đang chờ phải được cập nhật bằng kết quả thực trước khi kết luận UAT hoàn tất; không dùng kết quả cũ thay cho build hoặc phiên chạy mới.

## Kiểm tra tích hợp ngày 04/11/2026

Sau khi người dùng chạy lại BE/FE và chỉ định database test, đã hoàn tất [UAT trực tiếp ngày 04/11](../history/UAT_WORK_QUEUES_20261104.md). Tài liệu đó ghi rõ phần đã quan sát, thay đổi trên hồ sơ UAT và giới hạn kiểm chứng.

- Sau khi ghép scope planning và sửa đồng bộ hạn lịch: **508/508** kiểm thử SampleRequests/Formulas đạt, không skip. Kết quả TRX tại `artifacts/sample-work-uat-final-results/sample-work-uat-final.trx` (artifact local, không commit). Bộ này gồm kiểm tra LabAdmin không sở hữu khách vẫn xem/quản lý lịch đúng company, giữ che giá và từ chối hồ sơ foreign/inactive/internal.
- Build `HRM.Api` với output riêng `artifacts/verify-work-api-final` thành công, **0 lỗi**. Còn 12 cảnh báo NuGet ImageSharp đã có từ trước.
- FE đã kiểm tra lại sau sửa UAT: **80/80** test ở 11 file đạt, tiếp theo `npx.cmd tsc --noEmit` exit 0. Caption progress dùng cờ hành động BE thay vì khóa dịch. Không có cảnh báo mới do thay đổi này.
- Runtime BE/FE đã chạy và có endpoint preview; database `dev_enviroment_v1` được xác minh bằng transaction chỉ đọc. Người dùng tự nạp lại BE để kiểm tra bản sửa hạn lịch.
- UAT trực tiếp đã kiểm tra ba persona, owner/team/filter, chế độ chỉ đọc, Anh/Việt, lịch/audit, chuyển việc sau chốt chip và các form legacy/chip/kg. Các thao tác lưu trên hồ sơ UAT dùng phiên Developer hiện hành; ma trận quyền role được kiểm tra bằng automated tests, không mô tả là đã đăng nhập UI riêng từng role. Xem tài liệu UAT cho phạm vi chi tiết và bằng chứng.

Lệnh kiểm thử tích hợp:

```powershell
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~Features.PLM.SampleRequests|FullyQualifiedName~Features.PLM.Formulas" -m:1 -p:UseSharedCompilation=false -p:OutDir=../artifacts/verify-work-final/ --logger "trx;LogFileName=sample-work-final.trx" --results-directory artifacts/sample-work-final-results -v:quiet
```

## Nhóm đã có phản hồi và nhãn loại phản hồi

`workBucket=FeedbackReceived` giữ điều kiện đã có ngày phản hồi của Trial hiện tại, đồng thời nhận hồ sơ có sự kiện khách phản hồi giá thực tế `CustomerPriceAccepted`, `CustomerPriceRejected` hoặc `CustomerPriceNoResponse`. Truy vấn kiểm tra sự kiện đúng company/hồ sơ ở SQL trước count và phân trang; ngày báo giá thực tế, giá chuẩn đã duyệt hoặc sự kiện thu hồi đơn lẻ không được coi là phản hồi. Các điều kiện visibility, mốc theo dõi, team và audience không đổi. Hồ sơ chưa có Trial vẫn vào nhóm này khi đã có phản hồi giá.

Mỗi dòng report có `feedbackSummaries`, dùng cho nhãn gọn trên thẻ/ngăn chi tiết, ví dụ:

```json
{"feedbackSummaries":[
  {"kind":"Price","outcome":"Accepted","occurredAt":"2026-11-04T10:54:00","note":"Khách đồng ý theo phương án đã báo."},
  {"kind":"ColorChip","outcome":"Rejected","occurredAt":"2026-11-03T15:00:00","note":"Khách yêu cầu chỉnh tông màu."}
]}
```

Mảng rỗng nghĩa là chưa có phản hồi được ghi nhận. Mỗi loại trả tối đa một kết quả mới nhất; giá, chip và mẫu kg có thể cùng xuất hiện. `kind` là `Price`, `ColorChip`, `TrialSample` hoặc `UnknownDelivery` khi lần gửi lịch sử chưa có lượng thực gửi. Loại lần gửi lấy từ lượng thực lưu trên Trial, không suy từ kg Sale đang yêu cầu. `outcome` là `Accepted`, `Rejected`, `NoResponse`, `Cancelled`, `Revoked` hoặc `Recorded` (phản hồi lịch sử không đủ trạng thái chuẩn). `occurredAt` dùng ngày phản hồi Trial; với giá dùng ngày nghiệp vụ của sự kiện, thiếu thì dùng ngày ghi nhận.

Nhãn là lịch sử phản hồi, không xác nhận giá hiện còn hiệu lực hoặc đúng VU hiện tại. Nếu đã phản hồi giá rồi sau đó thu hồi, nhóm vẫn coi là từng nhận phản hồi nhưng nhãn giá trả `Revoked` theo sự kiện thu hồi mới nhất, tránh hiện đồng ý giá đang có hiệu lực. Thu hồi không có phản hồi giá trước đó không sinh nhãn. Với giá, `note` chỉ lấy phản hồi dùng chung liên kết đúng sự kiện đang chọn, kể cả khi nhãn là thu hồi; không lấy Note giá riêng hoặc ghi chú của phản hồi cũ để lấp chỗ trống. Có phản hồi dùng chung thì `isNoteHidden=false`; chưa có thì `note=null`, `isNoteHidden=true` cho mọi người, còn loại/kết quả/ngày vẫn hiện. Chip/kg giữ ghi chú kỹ thuật của Trial theo record scope và `isNoteHidden=false`. Cờ được serialize rõ ràng cho FE. Dữ liệu giá riêng ở DTO/vùng giá tiếp tục dùng capability `pricing.approved-selling-price.view` ngoài company/visibility, không suy quyền từ nhãn Lab/Sale. Dữ liệu nhãn được gắn từ batch Trial/sự kiện của trang đã tải, không truy vấn riêng từng thẻ.

Nhánh Báo giá không kg đã có khách đồng ý đúng VU: VU Approved giao việc `FinalizeQuotation` cho Lab. Lab bấm Hoàn thành công thức chỉ chốt VU Completed; tiếp theo Sale phụ trách bấm `CompleteRequest` để hoàn thành hồ sơ và lên đơn. VU đã Completed từ trước không cần tạo lịch sử Lab giả. Contract/token/thông báo và guard tạo đơn: [Luồng báo giá](../workflow/CHIP_SAMPLE_FLOW.md#báo-giá-không-kg-lab-chốt-vu-rồi-sale-hoàn-thành-hồ-sơ).

### Che nội dung giá tại các nguồn đọc

Quyền đọc giá lấy từ `IPricingVisibilityService.GetAccess().CanViewApprovedSellingPrice`. LabUser/LabAdmin thông thường không có capability này; nhiều role hoặc permission claim cấp quyền hợp lệ vẫn được resolver xử lý, không áp ưu tiên cấm theo tên role. Người không có quyền nhận `CustomerPriceReference`, `CustomerPriceConfirmationNote`, `CustomerPriceQuotationLineId` bằng null; DTO `customerPriceConfirmation` cũng che `note`, `priceReference`, `quotationLineId` và trả `isContentHidden=true`, đồng thời dòng report trả `isCustomerPriceContentHidden=true`. Cờ trạng thái khách đã đồng ý, người/ngày ghi nhận vẫn theo phạm vi hồ sơ.

`GET .../workflow` dùng report đã che và không trả lookup căn cứ báo giá cho người không xem giá. `GET .../workflow/events` che Note của sự kiện giá/thu hồi, có `isNoteHidden=true`; sự kiện phản hồi chip/kg không bị che theo quyền giá. History AuditLog cũ chỉ bỏ các detail tài chính xác định bằng reason sự kiện giá và field Note/Reference/RevocationReason hoặc tên field căn cứ giá rõ ràng; không che Note kỹ thuật thông thường, trạng thái và ngày. Export dùng DTO report cùng pipeline, không có đường trả ghi chú giá thô riêng.

## Ghi chú phản hồi dùng chung và thông tin giá riêng

Client mới gửi `customerFeedbackNote` (tối đa 2000 ký tự) cho nội dung khách phản hồi được Lab đọc, tách khỏi `note` và `priceReference`/`reference` riêng của giá. Không tự sao chép hoặc phân tích số tiền trong ghi chú lịch sử để biến thành nội dung dùng chung. `note` API cũ vẫn giữ riêng theo quyền đọc giá. Rejected/NoResponse chấp nhận ghi chú dùng chung mới hoặc Note riêng từ client cũ để tương thích; client mới yêu cầu ghi chú dùng chung. Accepted cho phép bỏ trống ghi chú dùng chung.

Mỗi phản hồi giá chính giữ sự kiện/Note/PriceReference riêng. Nếu có nội dung dùng chung rõ ràng, BE ghi thêm `CustomerPriceFeedbackShared`, `RelatedEventId` trỏ đúng sự kiện giá chính, Note chỉ lấy từ `customerFeedbackNote`, cùng transaction với cập nhật hồ sơ và outbox. Sự kiện dùng chung không phải bằng chứng đồng ý giá và không thay trạng thái công thức. Thông tin giá phát vào nhóm Giá theo quyền hiện có; nội dung dùng chung phát vào Chung để Lab đọc. Không thêm cột/bảng và không thay nội dung lịch sử.

`feedbackSummaries[].note` ưu tiên nội dung dùng chung mới nhất liên kết đúng sự kiện giá chính mới nhất; khi có liên kết này, Lab đọc được và `isNoteHidden=false`. Nếu không có nội dung dùng chung liên kết, `feedbackSummaries[].note=null` và `isNoteHidden=true` cho mọi người, kể cả người có quyền xem giá. Không đưa Note giá riêng/hỗn hợp lịch sử lên thẻ hoặc ngăn ghi chú dùng chung; nội dung giá riêng chỉ qua vùng/DTO giá có kiểm tra capability. Không dùng ghi chú dùng chung cũ của phản hồi trước để lấp một phản hồi giá mới hơn. Nguồn dùng chung được xác định từ sự kiện liên kết, không đoán từ nội dung hoặc gửi cờ nguồn ra FE. DTO private `customerPriceConfirmation` và primary workflow-event vẫn theo quyền giá như trên; sự kiện `CustomerPriceFeedbackShared` và AuditLog `CustomerFeedbackNote` là nội dung dùng chung.

### Bổ sung hoặc sửa nội dung dùng chung sau khi đã phản hồi giá

Sale phụ trách có `canRecordCustomerFeedbackNote=true` và `latestPriceEventId` trên dòng daily-work/workflow, kể cả hồ sơ Completed. Chỉ Sale owner có capability workflow Sale và employee active mới có nút này; Cancelled/inactive không được sửa. Lab không có quyền ghi nội dung thay Sale.

`POST /api/v1/plm/sample-requests/{id}/customer-feedback-note` nhận:

```json
{"expectedRequestUpdatedDate":"2026-11-04T10:54:00","expectedPriceEventId":"<latest-price-event-guid>","customerFeedbackNote":"Khách đồng ý phương án đã gửi, Lab có thể hoàn tất."}
```

Token hồ sơ lấy `flow.requestUpdatedDate` hoặc `sampleRequestUpdatedDate`; nullable nếu dữ liệu gốc chưa có token. Event id bắt buộc là phản hồi giá mới nhất đúng company/hồ sơ. BE kiểm tra cả hai một lần nữa khi cập nhật. Nội dung không rỗng, tối đa 2000 ký tự. Lưu chỉ cập nhật token/người sửa hồ sơ, thêm sự kiện dùng chung liên kết và AuditLog trước/sau, xếp outbox Chung; không đổi Completed/giá đã đồng ý/Formula/IsSelect và không mở lại hồ sơ. Gửi lại cùng nội dung với token mới trả no-op, không tạo tin trùng; token/event cũ phải tải lại. Đây không phải API sửa phản hồi Trial và không thay rule chỉ sửa Trial mới nhất.
