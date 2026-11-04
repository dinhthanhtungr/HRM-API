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

Số đếm, đơn vị hiển thị và danh sách khi bấm vào số dùng chung
`SampleRequestAdminReportQuery.Build` → `SampleRequestWorkScope`: hồ sơ từ 01/11/2026
hoặc hồ sơ cũ được Admin chọn theo dõi tiếp. Việc chưa xong mang sang kỳ/tháng/năm sau.
Khoảng ngày báo cáo vẫn lọc đúng mốc nhận/hoàn thành/hủy hoặc việc mở cuối kỳ; không
thay bằng tháng tạo hồ sơ. Tổng số và drilldown không dùng hai phạm vi khác nhau.
Daily có query parameter tùy chọn `useOperationalScope=true` để ô tìm mẫu ở báo cáo
dùng cùng phạm vi, kể cả `view=all`; mặc định false giữ tra cứu legacy/detail như cũ.
FE giữ bộ lọc kỳ, ba lựa chọn nhanh và bảng năm cột; bốn cột số chia đều, căn giữa.
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
- Nhóm theo `SampleRequest.BranchId → Company.Name/Code` đang lưu. Không suy HCM/Long Giang từ chuỗi tên khách, không giả có mapping khu vực chưa được cấu hình. BranchId rỗng vẫn là một nhóm riêng.
- `receivedCount`: `SampleRequest.LabReceivedDate` trong kỳ. Chưa nhận hoặc chưa nhập ngày không được tính; không thay bằng ngày tạo hay Sale nhận mẫu.
- `completedCount`: hồ sơ hiện Completed có mốc Approved của Trial trong kỳ (ngày CustomerReplyDate mới nhất).
- `cancelledCount`: hồ sơ hiện Cancelled có sự kiện SampleRequestCancelled trong kỳ; dự phòng ngày phản hồi của Trial Cancelled. Không dùng UpdatedDate đoán ngày hủy.
- `openCount`: hồ sơ đã tạo trước cuối kỳ, hiện còn mở hoặc có mốc kết thúc sau cuối kỳ. Mẫu cũ chưa kết thúc được tính dù không thuộc tháng tạo hiện tại; chỉ số này không đồng nghĩa cờ delay thủ công.
- `unknownTerminalDateCount`: hồ sơ hiện kết thúc nhưng không có mốc kết thúc đáng tin cậy và đã tạo trước cuối kỳ. Không gán chúng vào một kỳ tùy ý hoặc tự tính là mở.
- Các cột là mốc thời gian khác nhau nên không có công thức nhận = hoàn thành + treo + hủy.

`hasCompleteHistoricalStatus=false` là giới hạn cố ý: dữ liệu legacy có thể thiếu mốc hoặc từng đổi trạng thái/mở lại, hiện không có snapshot toàn bộ trạng thái tại mọi ngày. Báo cáo dùng mốc có dữ liệu và trạng thái hiện tại, không tuyên bố là sổ cái lịch sử hoàn chỉnh. Chọn kỳ tương lai cũng chỉ dùng dữ liệu hiện có, không dự báo.

`GET /api/v1/plm/sample-requests/reports/admin-details?fromDate=…&toDate=…&metric=open&branchId=…&pageNumber=1&pageSize=15`

Metric Received/Completed/Open/Cancelled dùng đúng phép lọc của ô tổng hợp, sau đó dùng projection/masking/pagination báo cáo mẫu chung. Bỏ branchId để xem tổng; truyền Guid.Empty để xem nhóm chưa gán. Response PagedResult như daily, có `isDelayed`, `canUpdateLabProgress`, `sampleRequestUpdatedDate` để PATCH hiện hành chống xung đột. Chi tiết hiển thị tiến trình hiện tại, không tái dựng Trial tại thời điểm lịch sử. Ô tìm mẫu tại báo cáo dùng daily `view=all&useOperationalScope=true&keyword=…`. Muốn đưa hồ sơ cũ chưa được chọn vào vận hành, mở danh sách Yêu cầu phối mẫu và chọn Theo dõi tiếp theo quyền Admin. Delay không thay thế lựa chọn này, không mở lại hồ sơ Completed/Cancelled.

Legacy `reports/period` giữ nguyên thống kê kết quả Trial, không đổi nghĩa các field cũ.
