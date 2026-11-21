# Công việc mẫu theo role và báo cáo Admin

## Phạm vi và dữ liệu dùng chung

Các API dưới đây chỉ đọc, giữ company/customer visibility của `sample-trials`, loại yêu cầu inactive và khách nội bộ `KH_VIETAUS`. Không đổi trạng thái, không thêm bảng/cột và không tạo thông báo. Giá/kỹ thuật trong danh sách chi tiết vẫn được che theo capability hiện hành. Các mốc và hành động tính từ Sample Request, Trial, Formula, WorkflowEvent và nguồn giá chuẩn; không phải dữ liệu người dùng phải nhập lại.

## Công việc theo vai trò

Contract hiện hành về nút, role và mô phỏng: [ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md).

`GET /api/v1/plm/sample-requests/reports/daily?audience=sale&bucket=actionRequired&date=2026-11-01&pageSize=15`

Hai query parameter mới là tùy chọn. Không gửi `audience` giữ nguyên các view legacy. Khi có audience, `view` không quyết định nhóm nữa; `bucket` được lọc tại BE trước đếm/phân trang. Tên enum không phân biệt hoa/thường.

- `audience=sale`: SaleUser hoặc Admin, dùng company/customer read scope chung; `myActionRequired` thêm `ManagerBy` là employee hiện tại. Quyền thao tác vẫn kiểm owner riêng.
- `audience=lab`: LabUser, LabAdmin hoặc Admin được đọc; nút kỹ thuật cần capability Lab. `audience=labAdmin` là hàng đợi quản lý lịch/rà tiến độ riêng cho LabAdmin hoặc Admin. LabAdmin mặc định không có capability kỹ thuật.
- Các bucket công việc dùng [phạm vi vận hành từ 01/11/2026](WORK_SCOPE.md), gồm hồ sơ mới và hồ sơ cũ được Admin chọn theo dõi tiếp. Delay không tự đưa hồ sơ cũ vào phạm vi.
- `all`: tra cứu toàn bộ hồ sơ được phép xem, gồm hồ sơ cũ chưa chọn theo dõi, hoàn thành/hủy. Không áp mốc vận hành; vẫn giữ company/customer scope.
- `actionRequired` của Lab: hồ sơ chưa kết thúc, còn việc Lab hoặc được bật delay thủ công; không giới hạn tháng tạo. Việc tồn tự chuyển sang tháng/năm sau. Delay không bị xóa khi đổi nhóm.
- `actionRequired` của Sale: bước được phân công Sale theo classifier chung, gồm báo giá/phản hồi giá, phản hồi lần gửi và quyết định hoàn thành/yêu cầu mẫu tiếp. Gửi chip không chặn giá; mẫu kg bắt khách đồng ý giá đúng VU.
- `waitingLab`: hồ sơ mở còn công việc Lab, không giới hạn tháng để Sale theo dõi toàn bộ mẫu của mình.
- `waitingPrice`: đã xác nhận công thức, còn phần giá chưa có phiên bản chuẩn đúng nguồn.
- `waitingSale`: Trial hiện tại đã gửi/chờ phản hồi/báo giá.
- `rework`: Trial hiện tại Failed hoặc ReworkRequested, hồ sơ chưa kết thúc.
- `feedbackReceived`: Trial hiện tại có ngày phản hồi với kết quả Approved/Failed/Cancelled/ReworkRequested. Đây là bộ lọc quan sát, có thể trùng Rework/Completed/Cancelled.
- `completed`/`cancelled`: trạng thái hồ sơ, không đếm từng Trial.

Response giữ `PagedResult<SampleRequestSampleTrialReportDto>`:

```json
{"items":[{"sampleRequestId":"…","sampleRequestExternalId":"TP_31072","trialNo":2,"trialCount":2,"workStatus":"AwaitingCustomerFeedback","nextActionCode":"FollowUpCustomer","responsibleRole":"Sale","responsibleEmployeeId":"…","isActionForCurrentUser":true,"isDelayed":false}],"totalCount":1,"pageNumber":1,"pageSize":15}
```

`nextActionCode`/`responsibleRole` mô tả bước nghiệp vụ; FE dùng `primaryActionCode`, `isActionForCurrentUser`, `supplementaryActionCodes` để dựng nút được phép cho góc nhìn hiện tại, không suy từ màu hoặc tên nhóm. Receipt chuẩn là mốc tùy chọn, không phải Sale nhận mẫu. Hồ sơ không có Trial trả trial fields null, không tạo Trial giả. Danh sách chính vẫn một dòng/hồ sơ với Trial mới nhất; lịch sử giữ các Trial trước qua API lịch sử hiện hành.

Hồ sơ thiếu bằng chứng nghiệp vụ có thể trả `NeedsReview`/`ReviewSample` thuộc LabAdmin để rà; không tự giả loại lần gửi, không biến ngày dự kiến thành ngày thực tế và không mở lại hồ sơ Completed/Cancelled.

## Báo cáo mẫu tổng hợp theo kỳ

### Lượt công việc — giao diện hiện hành

`admin-summary.activities` bổ sung totals/branches/employees/resultsAsOf. Counts theo quotation/chip/sample gồm completed/responses/accepted/rejected/waiting/cancelled/unknown. Tổng các kết quả bằng completed; responses có thể lớn hơn completed. Các metric TP cũ bên dưới giữ tương thích, không dùng thay số lượt.

- Báo giá: mỗi phiên bản giá VND Approved có giá dương và ApprovedAt trong kỳ, cùng company/product/VU, có xác nhận VU trước mốc duyệt hoặc FormulaId được chọn trên TP. Giữ phiên bản Approved đã inactive để không mất lịch sử. Không suy lượt từ ngày dự kiến/UpdatedDate.
- Chip/mẫu: mỗi Trial active có SentDate trong kỳ, lượng thực gửi 0/>0. Không suy loại từ khối lượng yêu cầu hiện tại; đếm cả các lần gửi trước đã phải làm lại. Thiếu lượng/giá trị không hợp lệ vào kind=other: vẫn tính tổng, tách link kiểm tra ngoài ba cột, không tự gán chip/mẫu.
- Báo giá quy cho người xác nhận đúng VU gần nhất trước lúc duyệt, giao mẫu quy cho SentByEmployeeId. Chỉ dùng employee cùng company; thiếu actor tách riêng, không gán cho mọi người tạo VU. Nhóm theo membership hiện tại; người thuộc hai nhóm gom vào multiple, không nhân số lượt. Roster hiện hành vẫn hiện người 0 lượt.
- Kỳ lọc ngày hoàn thành `[fromDate, toDate+1)`; kết quả mới nhất đến resultsAsOf, kể cả phản hồi sau kỳ. Mẫu đếm CustomerFeedbackRecorded theo đúng Trial; bản ghi cũ chỉ có CustomerReplyDate giữ tối thiểu 1 lượt, không dựng thêm lịch sử. Sửa riêng ngày/note không tính thành phản hồi nghiệp vụ mới.
- Giá ưu tiên ProductPricingVersionId; thu hồi theo RelatedEventId. Thiếu version chỉ ghép đúng TP/VU trong khoảng từ ApprovedAt đến lần duyệt kế tiếp cùng VU. Không cộng tin chia sẻ nhóm Chung/notification. Kết quả giá lấy RecordedAt/Id mới nhất; thu hồi/không trả lời về waiting. Trial dùng kết quả hiện tại. Hủy TP không đổi các lượt đã đạt/không đạt; lượt còn chờ tách cancelled. Kết quả thiếu dữ liệu tách unknown.

`GET reports/activity-details`: fromDate/toDate, kind, outcome, branchId, employeeId, team, pageNumber/pageSize. outcome=responses chọn công việc có phản hồi, mỗi dòng vẫn là một công việc có cột số responses, không nhân TP theo số phản hồi. employeeId=Guid.Empty chọn thiếu actor; team hỗ trợ QAQC.RD/QAQC.MAU/multiple/unassigned. Cùng builder với summary; lọc/đếm/phân trang trong PostgreSQL trước khi tải payload trang. DTO chỉ có TP/VU, loại/lần gửi, người, thời gian và counts/kết quả; không có giá tiền, cost, thành phần VU hay note riêng. Endpoint giữ Authorize + SampleRequestReportAccess + company/customer visibility; không mở thêm quyền nghiệp vụ.

FE dùng bốn thẻ tổng và bảng công ty hoặc nhóm/người, mỗi ô có completed/kết quả/responses. Click số mở danh sách lượt và link TP đến lịch sử hiện có. Bộ lọc drilldown và nhóm hiển thị lưu URL; chỉ thu gọn nhóm là local. Hồ sơ nhận/đang làm/treo/hủy ở mục riêng theo đơn vị TP. API cũ thiếu activities hiện cảnh báo, không giả dữ liệu 0.

### Metric TP giữ tương thích

Số đếm, đơn vị hiển thị và danh sách khi bấm vào số dùng chung
`SampleRequestAdminReportQuery.Build` → `SampleRequestWorkScope`: hồ sơ từ 01/11/2026
hoặc hồ sơ cũ được Admin chọn theo dõi tiếp. Việc chưa xong mang sang kỳ/tháng/năm sau.
Chỉ số **Tổng số mẫu nhận trong kỳ** (`Received`) đếm TP có `CreatedDate` trong kỳ
`[FromDate 00:00, ToDate + 1 ngày 00:00)`, không phụ thuộc `LabReceivedDate` hay trạng thái
hiện tại; mỗi TP tính một lần, không đếm từng Trial. Hồ sơ cũ được theo dõi tiếp nhưng tạo
ngoài kỳ không cộng vào số nhận mới. Hoàn thành/hủy vẫn theo mốc nghiệp vụ, việc mở theo
cuối kỳ. Tổng số, từng chi nhánh và drilldown dùng cùng predicate/phạm vi được phép xem.
Daily có query parameter tùy chọn `useOperationalScope=true` để ô tìm mẫu ở báo cáo
dùng cùng phạm vi, kể cả `view=all`; mặc định false giữ tra cứu legacy/detail như cũ.
FE giữ bộ lọc kỳ, ba lựa chọn nhanh và bảng sáu cột; năm cột số chia đều, căn giữa.
Theo dõi mẫu của Lab chuyển sang báo cáo ngay trên cùng route bằng `reportView=summary`;
bấm số mở danh sách gọn trong drawer. Route báo cáo riêng vẫn hoạt động.
Bỏ mô tả dài; chỉ hiện cảnh báo lịch sử khi thực sự có hồ sơ kết thúc thiếu ngày.

Mọi nhân viên đã đăng nhập, có company/employee hợp lệ, được mở báo cáo tổng hợp và chi tiết.
Controller giữ `[Authorize]`; handler dùng `SampleRequestReportAccess`, sau đó vẫn áp dụng
company/customer read scope hiện hành. Người chỉ thấy một phần hồ sơ sẽ nhận số đếm và
chi tiết của phần đó, không phải toàn công ty. User thiếu context bị từ chối; scope rỗng
trả báo cáo rỗng. Tổng hợp chỉ có số lượng, chi tiết vẫn mask giá/kỹ thuật như sample-trials.
Không mở quyền sửa, nhận chuẩn, delay hoặc chọn theo dõi hồ sơ cũ theo quyền xem báo cáo.
Menu là **PLM → Báo cáo mẫu tổng hợp**; hai màn tác nghiệp đổi tên thành
**Theo dõi mẫu của Lab/Sale**. Giữ route/API có chữ admin để tương thích link/client cũ;
tên class/DTO nội bộ không mang ý nghĩa giới hạn quyền. Không cần seed claim để xem.

`GET /api/v1/plm/sample-requests/reports/admin-summary?fromDate=2026-11-01&toDate=2026-11-30`

FE đổi ngày/tuần/tháng thành khoảng ngày inclusive. Khoảng bắt buộc hợp lệ, tối đa 367 ngày, không nhận DateOnly.MaxValue. API không tự suy ra tháng từ ngày hiện tại.

```json
{"fromDate":"2026-11-01","toDate":"2026-11-30","groupSource":"SampleRequestBranch","hasCompleteHistoricalStatus":false,"rows":[{"branchId":"…","branchCode":"…","branchName":"…","receivedCount":8,"completedCount":5,"openCount":12,"cancelledCount":1,"unknownTerminalDateCount":2}],"totals":{"receivedCount":8,"completedCount":5,"openCount":12,"cancelledCount":1,"unknownTerminalDateCount":2}}
```

- Mỗi số đếm là số hồ sơ SampleRequest, không nhân lên theo Trial.
- Hồ sơ có `SampleQuantity > 0` luôn thuộc nhánh chạy mẫu kg, kể cả `InfoType` là Báo giá/Quotation: **Lab hoàn thành chỉ lấy `SentDate` của lần gửi active có `DeliveredSampleQuantityKg > 0`**. Giá chuẩn đã duyệt, lần gửi chip và sự kiện hoàn thành không qua Trial không thay thế bằng chứng gửi kg. Chưa gửi kg vẫn ở Mẫu đang thực hiện nếu đã có VU và hồ sơ còn mở. Các mốc giá/hoàn thành không qua Trial bên dưới chỉ áp dụng cho hồ sơ không yêu cầu kg; quy tắc reset khi làm lại vẫn giữ nguyên.
- Nhóm theo `SampleRequest.BranchId → Company.Name/Code` đang lưu. Không suy HCM/Long Giang từ chuỗi tên khách, không giả có mapping khu vực chưa được cấu hình. BranchId rỗng vẫn là một nhóm riêng.
- `receivedCount`: `SampleRequest.LabReceivedDate` trong kỳ. Chưa nhận hoặc chưa nhập ngày không được tính; không thay bằng ngày tạo hay Sale nhận mẫu.
- `completedCount` / metric `completed`: **Lab hoàn thành**, độc lập trạng thái đóng TP. Lấy mốc mới nhất giữa giá VND được duyệt (`ApprovedAt`, giá dương, đúng công thức đã xác nhận hiện hành của hồ sơ Báo giá), gửi chip/mẫu (`SentDate`) và sự kiện hoàn thành không qua Trial. Chỉ tính mốc trong kỳ, loại hồ sơ hủy. Không đoán ngày duyệt từ ngày tạo/cập nhật giá. Yêu cầu làm lại/giá thấp hơn/chạy mẫu/gửi chip/chốt công thức hoặc Trial mới dạng Draft/phản hồi Failed sau mốc hoàn thành đưa hồ sơ về đang làm đến khi có mốc hoàn thành mới.
- `priceAcceptedCount`, `chipAcceptedCount`, `sampleAcceptedCount` và metric tương ứng `priceAccepted`, `chipAccepted`, `sampleAccepted`: ba kết quả khách chấp nhận tách riêng bên dưới hai phần Lab. Giá dùng kết quả workflow mới nhất (thu hồi/từ chối/chưa phản hồi thay thế đồng ý trước đó); chip/mẫu dùng Trial active mới nhất theo từng loại (0 kg / lớn hơn 0 kg), trạng thái Approved và CustomerReplyDate trong kỳ. Một TP có thể nằm trong nhiều nhóm; không cộng ba số thành tổng TP duy nhất. Tổng và drilldown dùng cùng phép lọc.
- `cancelledCount`: hồ sơ hiện Cancelled có sự kiện SampleRequestCancelled trong kỳ; dự phòng ngày phản hồi của Trial Cancelled. Không dùng UpdatedDate đoán ngày hủy.
- `openCount`: hồ sơ đã tạo trước cuối kỳ, hiện còn mở hoặc có mốc kết thúc sau cuối kỳ. Mẫu cũ chưa kết thúc được tính dù không thuộc tháng tạo hiện tại; chỉ số này không đồng nghĩa cờ delay thủ công.
- `unknownTerminalDateCount`: hồ sơ hiện kết thúc nhưng không có mốc kết thúc đáng tin cậy và đã tạo trước cuối kỳ. Không gán chúng vào một kỳ tùy ý hoặc tự tính là mở.
- `inProgressCount` / metric `inProgress`: hồ sơ tạo trước cuối kỳ, hiện chưa Completed/Cancelled, có công thức active thuộc cùng company/product và chưa hoàn thành công việc Lab hoặc đang được yêu cầu làm lại. Cùng định nghĩa áp dụng cho bảng chi nhánh và cây người tạo VU.
- `pendingCount` / metric `pending`: hồ sơ tạo trước cuối kỳ, hiện `New` hoặc `InProgress` nhưng chưa có công thức active. Đây là cột Mẫu đang treo mới; `openCount` và metric `open` giữ nguyên cho client cũ. Hai chỉ số mới dựa trên trạng thái/công thức hiện tại, không tái dựng lịch sử công thức tại cuối kỳ.
- Các cột là mốc thời gian khác nhau nên không có công thức nhận = hoàn thành + treo + hủy.

`hasCompleteHistoricalStatus=false` là giới hạn cố ý: dữ liệu legacy có thể thiếu mốc hoặc từng đổi trạng thái/mở lại, hiện không có snapshot toàn bộ trạng thái tại mọi ngày. Báo cáo dùng mốc có dữ liệu và trạng thái hiện tại, không tuyên bố là sổ cái lịch sử hoàn chỉnh. Chọn kỳ tương lai cũng chỉ dùng dữ liệu hiện có, không dự báo.

`GET /api/v1/plm/sample-requests/reports/admin-details?fromDate=…&toDate=…&metric=open&branchId=…&pageNumber=1&pageSize=15`

Metric Received/Completed/Open/Cancelled dùng đúng phép lọc của ô tổng hợp, sau đó dùng projection/masking/pagination báo cáo mẫu chung. Bỏ branchId để xem tổng; truyền Guid.Empty để xem nhóm chưa gán. Response PagedResult như daily, có `isDelayed`, `canUpdateLabProgress`, `sampleRequestUpdatedDate` để PATCH hiện hành chống xung đột. Chi tiết hiển thị tiến trình hiện tại, không tái dựng Trial tại thời điểm lịch sử. Ô tìm mẫu tại báo cáo dùng daily `view=all&useOperationalScope=true&keyword=…`. Muốn đưa hồ sơ cũ chưa được chọn vào vận hành, mở danh sách Yêu cầu phối mẫu và chọn Theo dõi tiếp theo quyền Admin. Delay không thay thế lựa chọn này, không mở lại hồ sơ Completed/Cancelled.

Legacy `reports/period` giữ nguyên thống kê kết quả Trial, không đổi nghĩa các field cũ.
