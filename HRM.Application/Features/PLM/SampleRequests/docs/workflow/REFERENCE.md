# Quy tắc và vòng đời yêu cầu mẫu

## Lưu ý từ Lab gửi Sale

Daily work/report trả thêm `latestLabNote` (cùng DTO lưu ý) và `canWriteLabNote` để
đọc/viết nhanh tại thẻ. Summary được project từ sự kiện mới nhất trong cùng truy vấn
phân trang; không query từng dòng. Note chỉ thuộc company/hồ sơ đã qua read visibility.
Quyền ghi cần Lab capability + employee active + hồ sơ mở + read visibility; POST
vẫn kiểm tra lại độc lập với cờ trên danh sách. Không thay đổi schema hoặc notification.

`GET /{id}/lab-notes?pageNumber=1` trả tối đa 20 lưu ý mỗi trang (mới nhất trước),
`items[{id,note,recordedAt,recordedBy}]`, `hasMore`, `canWrite`, `expectedUpdatedDate`.
Đọc theo company + active + customer visibility của hồ sơ, bao gồm Sale có quyền xem;
không trả giá, ghi chú kỹ thuật hay ghi chú nhóm Giá. Hồ sơ đóng vẫn đọc lịch sử.

`POST /{id}/lab-notes` nhận `{noteId,expectedUpdatedDate,note}`. Nội dung plain text từ
1 đến 2.000 ký tự; đây là nội dung chia sẻ cho Sale/nhóm Chung, không phải bí mật kỹ thuật.
Chỉ capability `plm.sample-request.workflow.lab` (ManageSampleLabWorkflow), employee
active cùng company và read visibility được ghi trên hồ sơ còn mở; LabAdmin chỉ có quyền
lập lịch không tự được ghi. Scope/quyền/token được kiểm tra lại trên máy chủ.
Mapping hiện hành gồm LabUser, Developer, President và Admin; không yêu cầu người phụ
trách công thức hay ownership của thao tác kỹ thuật. GET, POST và `canWriteLabNote` dùng
cùng phạm vi hồ sơ được xem; không mở rộng quyền sửa công thức, ghi phản hồi thay Sale
hoặc truy cập khác company. Permission explicit bị từ chối vẫn được tôn trọng.
Mỗi lần cập nhật append `SampleRequestWorkflowEvent.EventType=LabNoteRecorded`, giữ nguyên
bản cũ, tác giả và thời gian; không sửa status/VU/Trial. Request token được cập nhật nguyên tử
với sự kiện và outbox; `noteId` ổn định cho retry cùng actor/nội dung, không tạo bản trùng.

Worker hiện có gửi tin hệ thống nhóm Chung qua topic `plm.sample_request.lab_note.recorded`
(enum 62). Người nhận theo resolver của hồ sơ/nhóm Chung hiện hành, gồm Sale phụ trách;
không nhận danh sách người nhận từ FE. Legacy cũng được nhận lưu ý được ghi chủ động.
Thông báo có thể rút gọn khi đạt giới hạn 2.000 ký tự; API lưu ý giữ đầy đủ nội dung.
Không thêm bảng/cột/migration; không phát lại tin hoặc lấy ghi chú kỹ thuật cũ làm lưu ý chung.

Tin workflow mới nêu kết quả, ghi chú và mã công thức có liên kết; bước tiếp theo chỉ hiện
khi rule workflow hiện tại xác định rõ. Tin cập nhật ghi chú tách nghĩa với phản hồi mới,
nhóm Chung không chứa ghi chú/tham chiếu nhóm Giá. Xem [nội dung tin](WORKFLOW.md#nội-dung-tin-workflow).

SaleAdmin và SaleUser là Leader (`MemberInGroups.IsAdmin = true`, membership active) chỉ xem
yêu cầu mẫu do chính mình hoặc thành viên nhóm mình quản lý phụ trách, kể cả khi có
CustomerViewAll. Giới hạn nằm trong `ApplySampleRequestVisibility`, dùng chung cho list,
detail, report và mutation; không thay scope của khách hàng, báo giá hay đơn hàng.
SaleAdmin không có nhóm Leader active chỉ còn phạm vi chính mình. SaleUser thường giữ scope
xem hiện có và chỉ thao tác hồ sơ mình phụ trách; không tự được làm thay khi nhận claim.

SaleAdmin và SaleUser Leader có quyền `plm.sample-request.workflow.sale.managed` được thực hiện thao tác
Sale thay nhân viên thuộc phạm vi nhóm quản lý hiện hành (`ViewerScope.EmployeeIdsInScope`),
kèm quyền workflow Sale cơ bản, nhóm Leader thật (với SaleUser) và write visibility của hồ sơ. Quyền xem toàn bộ không
tự cấp quyền làm thay ngoài phạm vi này. SaleUser giữ điều kiện chính mình phụ trách.
Lịch sử/thông báo ghi nhân viên thực sự thao tác, không đổi Sale phụ trách.
Khi triển khai môi trường đã dùng role claims, cấp bốn permission cho SaleAdmin và quyền
managed cho SaleUser bằng
[script quyền luồng mẫu](../../deployment/20261104_saleadmin_managed_workflow_permissions.sql)
hoặc màn quản lý quyền role; sau đó refresh/đăng nhập lại. Script chỉ thêm claim vào
bảng Identity hiện có, không tạo migration/bảng/cột và không tự chạy trong tác vụ này.
Không thay quyền xem giá, duyệt giá chuẩn hoặc sửa kỹ thuật của Lab.

Workflow command đọc cùng bằng chứng xác nhận Lab với báo cáo: sự kiện đúng
company/hồ sơ và VU active đúng sản phẩm, phiên bản Approved có dấu xác nhận,
hoặc thông điệp xác nhận cũ trong đúng conversation. Không suy diễn đã xác nhận
chỉ từ Formula.Status. Giá chuẩn phải là phiên bản active Approved, giá bán dương,
mới nhất theo version/thời điểm duyệt, đúng company/sản phẩm/VU và tiền tệ mặc định
của báo cáo (`GetSampleRequestSampleTrialsQuery.DefaultCurrency`), như luồng xác nhận
giá hiện tại. Thiếu bằng chứng không mặc định đã có giá hoặc đã xác nhận.

VU làm căn cứ thống nhất cho việc cần làm, giá được khách chấp nhận và DTO:
legacy ưu tiên VU đã chọn trên hồ sơ; luồng mới ưu tiên VU của lần gửi hiện tại;
sau đó mới đến VU đã được xác nhận và VU trên hồ sơ. Metadata VU trả cùng căn cứ,
giới hạn VU active đúng company/sản phẩm. Trạng thái chốt chip lấy sự kiện mới nhất
giữa chốt/mở lại của đúng VU và lần gửi. Legacy đã gửi nhưng thiếu VU trạng thái
SampleSent vào nhóm cần kiểm tra, không hiện nút hoàn thành giả.

Contract hàng đợi/nút thao tác theo LabUser, SaleUser, LabAdmin và Developer xem thử:
[ROLE_WORK_QUEUES.md](../work/ROLE_WORK_QUEUES.md).
Phiên thao tác thay cho phép tài khoản đích có thêm Developer cùng vai trò nghiệp vụ;
vẫn dùng đúng quyền tài khoản đích, kiểm tra active/company và audit hiện hành.
Lịch quản lý LabAdmin, token và audit: [WORK_SCHEDULE.md](../work/WORK_SCHEDULE.md).
LabAdmin được cập nhật lịch đã có trên hồ sơ còn mở,
kể cả đang chờ Sale/duyệt giá; nhóm Cần lập lịch vẫn chỉ lấy hồ sơ thiếu lịch của Lab.

Nhánh Báo giá không kg của hồ sơ từ 01/11/2026: khách đồng ý giá đúng VU →
Lab bấm **Hoàn thành công thức** (`FinalizeQuotation`) → Sale hoàn thành hồ sơ → lên đơn.
Lab chỉ chuyển VU Approved thành Completed, lưu snapshot/sự kiện/thông báo;
hồ sơ vẫn mở và chưa tự chọn VU. Sale chưa được Complete trước bước Lab.
API tạo đơn cũng chặn VU đã chốt báo giá trên hồ sơ hiện hành còn mở, dù không có Trial.
Chi tiết điều kiện, token và trường hợp VU Completed sẵn: [CHIP_SAMPLE_FLOW.md](CHIP_SAMPLE_FLOW.md).

Bucket MyActionRequired của audience Sale giữ cùng điều kiện việc Sale cần làm
như ActionRequired, thêm ManagerBy = employee đăng nhập trước phân trang.
ActionRequired và WaitingLab vẫn dùng phạm vi nhìn thấy của Yêu cầu phối mẫu.

Theo dõi mẫu Sale dùng cùng ApplySampleRequestVisibility/BuildReadScopeAsync với
Yêu cầu phối mẫu, không tự ép ManagerBy bằng employee đăng nhập. WorkAudience và
bucket chỉ chọn nhóm công việc; SaleEmployeeId chỉ lọc khi được gửi rõ ràng.
Quyền thao tác và IsActionForCurrentUser vẫn kiểm tra theo owner/capability riêng.
Các view SalePending/SaleToday legacy không có WorkAudience giữ hành vi cũ.

PATCH chọn VU trên hồ sơ legacy SampleSent (tạo trước 01/11/2026) lưu công thức được chọn
và chuyển SampleRequest cùng Formula được chọn sang Completed theo luồng cũ, kể cả
xác nhận lại cùng FormulaId. Giữ quyền FormulaSelectors/internal Lab, company/customer scope,
kiểm tra VU thuộc Product và concurrency token. Không tự ghi phản hồi khách hoặc duyệt Trial;
chỉ giữ thông báo hoàn thành cũ, không tạo sự kiện phản hồi khách của luồng báo cáo mới.
Hồ sơ mới vẫn không được dùng PATCH chọn VU để vượt luồng gửi/phản hồi/hoàn thành.
Nhánh FormulaUpdateRequested có yêu cầu duyệt riêng vẫn duyệt VU đề xuất như trước.

Quản trị job thông báo lỗi/chờ lâu (Developer): [JOB_ADMIN.md](../operations/JOB_ADMIN.md).

## Luồng hiện hành và legacy

Hồ sơ tạo trước 01/11/2026 không phát thông báo bổ sung của luồng theo dõi mẫu
(nhận mẫu chuẩn, xác nhận/thu hồi giá, phản hồi Trial và bước chip/kg).
Sự kiện vẫn lưu; dispatcher bỏ qua cả job cũ còn chờ thuộc các loại này.
Tin đã phát không bị sửa/xóa. Thông báo vận hành tạo yêu cầu, xác nhận công thức/
yêu cầu BGD báo giá, gửi mẫu và hủy vẫn giữ; các publisher cũ ngoài dispatcher không đổi.
Chọn công thức khi lên PO cho hồ sơ legacy không phát thêm tin phản hồi khách.
Hồ sơ từ 01/11/2026 giữ nguyên luồng thông báo hiện hành.

Phân team mặc định cho hàng đợi Lab và bộ lọc xem tất cả: [LAB_TEAMS.md](../work/LAB_TEAMS.md).

Kết quả test thao tác FE và validation: [UAT 03/11/2026](../history/UAT_FLOW_20261103.md).

[Contract legacy/chip/kg, API, quyền và triển khai](CHIP_SAMPLE_FLOW.md) là nguồn chuẩn cho giai đoạn và chuyển bước mới. BE định tuyến legacy theo ngày tạo hồ sơ trước 01/11/2026; FE dùng `item.flow`, không tự chọn service bằng ngày. Không tạo/chạy migration cho thay đổi này.

Chip không đạt giữ việc chính `ReworkFormula`, nhưng Lab được gửi lại cùng VU sau khi lưu điều chỉnh nếu BE trả `flow.allowedActions` có `Send`. Gửi lại tạo vòng chip mới, giữ lần không đạt cũ và khóa VU chờ Sale; không dùng mã việc chính thay cho danh sách thao tác được phép. Nhánh kg và hồ sơ đã đóng vẫn giữ chốt chặn riêng.

## Sửa phản hồi lần gửi mẫu mới nhất

[Quyền Sale, API chỉnh ngày/ghi chú và lịch sử AuditLog](FEEDBACK_CORRECTIONS.md).

## Quy trình giá và mẫu mới

[Quy trình Sale/Lab, dữ liệu, API và rollout](WORKFLOW.md) là contract của xác nhận giá cho
yêu cầu Thẻ màu dùng SampleRequest.Status + SampleRequestWorkflowEvent. AuditLog giữ lịch sử sửa dữ liệu,
không còn là nguồn nghiệp vụ xác nhận giá/công thức. Hồ sơ cũ giữ nguyên status, không phải xác nhận lại. Ngày Lab nhận chuẩn
giữ ở Sample Request; gửi mẫu mới chuyển Trial thẳng sang chờ phản hồi, không có bước Sale nhận.
Thẻ màu mới cho phép Lab tạo/chọn/xác nhận công thức trước khi khách đồng ý giá. Xác nhận giá
chỉ mở sau công thức, giá chuẩn đúng nguồn và ngày Sale báo giá. Theo luồng hiện hành,
khách đồng ý giá đúng VU là điều kiện gửi mẫu kg; gửi chip không bị chặn bởi giá.

## Vô hiệu hóa yêu cầu phối mẫu

`POST /api/v1/plm/sample-requests/{sampleRequestId}/deactivate` không nhận body.
Capability `plm.sample-request.deactivate` mặc định chỉ cấp cho `SaleUser` qua
`ApplicationPermissionRoleSets` và baseline seed `20260913_SeedRolePermissionClaims.sql`.
Không tự chạy seed/database update khi triển khai code. Môi trường dùng permission
explicit cần cập nhật role claim từ baseline seed rồi login/refresh token; token cũ
không tự có capability mới. Không thêm bảng/cột hoặc EF migration.

Backend bắt buộc authenticated, có company/employee context, employee active trong
company, Sample Request active cùng company và `ManagerBy == current EmployeeId`.
Chặn `SampleSent`, `Completed`, `SendDate`, `RealDeliveryDate`, hoặc bất kỳ Trial nào
(kể cả inactive) đã có `SentDate`/`RequestReceivedDate` hoặc trạng thái `SampleSent`,
`WaitingCustomerFeedback`, `Approved`, `Failed`, `ReworkRequested`. Draft/PriceQuote
chưa gửi vẫn được phép. Đây là soft delete, không xóa Product, Formula, Trial hoặc file.
Điều kiện được kiểm tra ngay trong UPDATE; gọi lại hoặc trạng thái/quyền đã đổi sẽ fail.

Thành công trả `{"success":true,"data":"<sampleRequestId>"}`, đặt `IsActive=false`,
`UpdatedBy/UpdatedDate` và ghi audit old/new `IsActive` với reason
`SampleRequestDeactivated` trong cùng transaction; không gửi notification.
Thất bại trả HTTP 400 với `{"success":false,"message":"..."}`, không tiết lộ record
ngoài company/ownership. `[Authorize]` vẫn yêu cầu đăng nhập.

Detail trả thêm `hero.canDeactivate` (boolean, mặc định false) từ cùng predicate và
capability backend. FE dùng cờ này để hiện nút xác nhận; cờ không thay thế kiểm tra
server tại thời điểm gọi API. Yêu cầu inactive bị loại khỏi các danh sách active.

Nhóm hội thoại phối mẫu tuân theo [InternalMail AREAS](../../../../InternalMail/AREAS.md):
Developer/President có capability `internal-mail.sample-request-groups.observe` được xem
Chung, Kỹ thuật, Giá và nhóm riêng trong cùng company; quyền quan sát không tự cấp gửi/mời.
Token explicit cần seed capability rồi login/refresh theo hướng dẫn triển khai trong tài liệu đó.

ACCUser có quyền xem Sample Request, detail/history/report, Formula options và thông tin kỹ thuật như President trong cùng công ty. Query dùng BuildReadScopeAsync; trường giá/NVL/kỹ thuật vẫn qua capability riêng. ProductTechnicalViewers bao gồm ACCUser, ProductTechnicalEditors giữ nguyên. Command, approval và private conversation area giữ gate hiện có.

Tài liệu cho Codex FE: [Summary, detail, POST và PATCH](../api/FE_HANDOFF.md).

## Sample Request Formula Lifecycle

Khi Lab gửi mẫu lần đầu hoặc gửi lại mẫu, `SampleRequest.FormulaId` chưa là Formula được khách chọn.
Backend tạo một `SampleRequestSampleTrial` trong cùng transaction với Formula/SampleRequest:

```text
Formula.Status = SampleSent
SampleRequest.Status = SampleSent
SampleRequest.FormulaId = null hoặc giữ Formula đã chốt trước đó (không ghi đè bằng Formula vừa gửi)
SampleRequestSampleTrial.Status = SampleSent
SampleRequestSampleTrial.TrialNo = max(TrialNo) + 1
```

Chỉ khi Sale ghi nhận Trial `Approved`, backend mới chọn Formula của Trial, chuyển Formula và Sample Request sang `Completed`.
Nếu Trial `Failed`, Sample Request quay về `InProgress` để Lab làm/clone Formula mới; nếu `Cancelled`, Sample Request chuyển `Cancelled`.
`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` không được phép gán trực tiếp `SampleSent`/`Completed`,
với hồ sơ mới, chọn Formula khi Sample Request đang `SampleSent` cũng bị chặn; FE phải gọi action nghiệp vụ tương ứng.
Legacy SampleSent được chọn VU để lên đơn nhưng việc chọn không duyệt mẫu hay hoàn thành hồ sơ.

Luồng chốt công thức của Sample Request được tách theo ý nghĩa nghiệp vụ:

```text
Chưa chốt công thức -> Lab gửi mẫu.
Đã chốt công thức -> Lab yêu cầu cập nhật công thức.
```

### Gửi mẫu lần đầu hoặc gửi mẫu lại

Khi Sample Request chưa `Completed`, Lab tạo hoặc clone Formula, chỉnh công thức ở trạng thái còn cho phép sửa, rồi bấm gửi mẫu.

Kết quả nghiệp vụ:

```text
Formula.Status = SampleSent
SampleRequest.Status = SampleSent
SampleRequest.FormulaId không bị gán bằng Formula vừa gửi
SampleRequestSampleTrial.TrialNo = max(TrialNo) + 1
SampleRequestSampleTrial.DeliveredSampleQuantityKg = khối lượng thực gửi
```

BE phải tạo một message công khai trong đúng `InternalConversation` của Sample Request, không tạo conversation mới. Message này thông báo cho Sale/participants rằng Lab đã gửi mẫu, kèm tối thiểu `SampleRequestId`, `SampleRequest.ExternalId`, `FormulaId`, `Formula.ExternalId`, `Product.ColourCode` và `Product.Name` nếu có.

### Sale chọn công thức khách hàng đồng ý

Khi khách hàng đã chọn một công thức, Sale xác nhận công thức được chọn.

Kết quả nghiệp vụ:

```text
SampleRequest.FormulaId = Formula khách hàng chọn
SampleRequest.Status = Completed
Formula.Status = Completed
```

Chỉ Sample Request/Formula đã `Completed` mới được dùng để lên đơn hàng. BE phải gửi message trong cùng Sample Request conversation để báo cho Lab biết Sale đã chốt công thức. Message này là một reply/event trong thread hiện có, không tạo nhóm trao đổi mới.

### Lab yêu cầu cập nhật công thức sau khi đã Completed

Sau khi Sample Request đã `Completed`, Lab không được sửa đè công thức đã chốt. Nếu cần cải tiến, Lab tạo hoặc clone một Formula mới. Lúc này nút trên FE không còn là "Gửi mẫu" mà là "Yêu cầu cập nhật công thức".

Kết quả khi Lab gửi yêu cầu:

```text
Formula mới.Status = PendingSaleConfirmation
SampleRequest.Status = FormulaUpdateRequested
SampleRequest.FormulaId = Formula khách hàng đang chọn hiện tại
```

Yêu cầu cập nhật công thức được lưu vào message payload/audit của Sample Request thread, tương tự luồng data-change request. Payload phải chứa công thức hiện tại và công thức đề xuất để Sale có thể xem/chấp nhận/từ chối ngay trong message hoặc mở Sample Request detail. Không cần tạo bảng request riêng nếu audit/message đã đủ cho nhu cầu truy vết.

Sale chấp nhận:

```text
SampleRequest.FormulaId = Formula đề xuất
SampleRequest.Status = Completed
Formula đề xuất.Status = Completed
```

Ngoài action `decision`, Sale/role `FormulaSelectors` có thể chốt ngay từ màn Sample Request bằng
`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` với `formulaId` đúng Formula đang được đề xuất khi
`status = FormulaUpdateRequested`. Đây là shortcut xác nhận: backend kiểm tra Formula cùng Product và khớp
Formula trong yêu cầu pending, chuyển Sample Request và Formula đề xuất sang `Completed`, chọn Formula đó cho
Product, đánh dấu message payload là `Approved` và gửi message công thức hoàn thành. PATCH không được dùng để
chốt một Formula khác với Formula của yêu cầu pending.

Sale từ chối hoặc Lab hủy yêu cầu:

```text
SampleRequest.FormulaId giữ nguyên
SampleRequest.Status = Completed
Formula đề xuất.Status = Rejected khi Sale từ chối, hoặc Cancelled khi yêu cầu bị hủy
```

Mỗi hành động `gửi mẫu`, `chọn công thức hoàn thành`, `yêu cầu cập nhật công thức`, `chấp nhận`, `từ chối` và `hủy yêu cầu` phải tạo message trong cùng Sample Request conversation và publish notification cho các participant liên quan. Không tạo conversation/group mới cho các sự kiện này.

Mọi lần `SampleRequest.Status` thực sự đổi trong lifecycle này đều ghi `AuditLog` cùng transaction với thao tác nghiệp vụ. Audit lưu `Status` cũ/mới, `ChangedBy`, `ChangedAt` và reason ổn định: `FormulaSampleSent`, `FormulaCustomerApproved`, `FormulaUpdateRequested`, `FormulaUpdateApproved`, `FormulaUpdateRejected`, `FormulaUpdateCancelled`, `SampleTrialCustomerFeedback` hoặc `CustomerCareSampleTrialInteraction`. PATCH vẫn dùng audit thay đổi field tổng quát hiện có và không ghi thêm status audit thứ hai cho cùng lần PATCH.

Các API formula-change dùng message payload, không tạo bảng/cột request riêng:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/formula-change-requests
POST /api/v1/plm/sample-requests/{sampleRequestId}/formula-change-requests/{messageId}/decision
```

Payload tạo yêu cầu:

```json
{
  "requestedFormulaId": "00000000-0000-0000-0000-000000000000",
  "message": "Lab đề xuất cập nhật sang công thức cải tiến.",
  "isUrgent": false,
  "recipientEmployeeIds": []
}
```

Payload quyết định:

```json
{
  "decision": "Approve",
  "reason": null
}
```

`decision` nhận `Approve`, `Reject` hoặc `Cancel`. `Reject` và `Cancel` bắt buộc có `reason`.
`GET /api/v1/plm/sample-requests/{sampleRequestId}/messages` trả `formulaChangeAction` cho message yêu cầu
đang chờ Sale quyết định.

Các topic notification riêng đã append vào `TopicNotifications` và map trong `NotificationTopicCatalog`:

```text
SampleRequestSampleSent                = 37 -> plm.sample_request.sample_sent
SampleRequestFormulaCompleted          = 38 -> plm.sample_request.formula.completed
SampleRequestFormulaUpdateRequested    = 39 -> plm.sample_request.formula_update.requested
SampleRequestFormulaUpdateApproved     = 40 -> plm.sample_request.formula_update.approved
SampleRequestFormulaUpdateRejected     = 41 -> plm.sample_request.formula_update.rejected
SampleRequestFormulaUpdateCancelled    = 42 -> plm.sample_request.formula_update.cancelled
SampleRequestDirectPatchNotified       = 43 -> plm.sample_request.direct_patch.notified
SampleRequestCancelled                 = 52 -> plm.sample_request.cancelled
```

Tất cả topic này thuộc category `SampleRequest`. Payload notification chỉ chứa metadata định vị thread/message/formula/sample; nội dung chi tiết và action card được đọc lại từ API message có phân quyền.

Khi PATCH hoặc phản hồi Sample Trial làm toàn bộ Sample Request chuyển từ trạng thái khác sang `Cancelled`, backend
tạo message hủy trong conversation hiện có và publish `SampleRequestCancelled` thuộc event group `lifecycle`. Không dùng
`SampleRequestFormulaUpdateCancelled` cho trường hợp này vì topic đó chỉ biểu diễn việc hủy yêu cầu cập nhật
Formula. Message/notification lịch sử không bị đổi hoặc xóa.

Các giá formula trong summary (`selectedFormula.totalPrice`, `formulaSelects[].price`, `formulaDevs[].price` và
`formulaStandard[].price`) chỉ trả khi current user thuộc
`ApplicationRoleSets.PLM.FormulaPriceViewers`; ngoài nhóm này backend trả `null`.

The selected customer formula header is returned under `selectedFormula` by linking:

```text
SampleRequest.FormulaId -> Formula
```

`selectedFormula` is `null` when `SampleRequest.FormulaId` is null or the formula is inactive. Summary does not return formula materials by default. It returns `materialCount` and `materialsUrl` so the frontend can lazy-load materials when a row is expanded.

Formula materials are loaded from:

```http
GET /api/v1/plm/formulas/{formulaId}/materials
```

Formula materials are ordered by `LineNo`, then `FormulaMaterialId`.

## Formula Status Endpoint Contract

FE gọi `PATCH /api/v1/plm/formulas/{formulaId}/status` với `sampleRequestId` để backend xử lý đúng Sample Request đang thao tác.

Payload gửi mẫu:

```json
{
  "status": "SampleSent",
  "sampleRequestId": "00000000-0000-0000-0000-000000000000",
  "deliveredSampleQuantityKg": 2.5,
  "expectedUpdatedDate": "2026-07-28T10:30:00"
}
```

Khi `status = SampleSent`, backend validate formula cùng product với Sample Request, yêu cầu khối lượng lớn hơn hoặc bằng 0,
chuyển `SampleRequest.Status = SampleSent`, ghi `SendBy/SendDate`, hoàn tất Trial `Draft` phù hợp hoặc tạo Trial mới có
`TrialNo = max + 1`. Trial lưu `DeliveredSampleQuantityKg`, snapshot `Formula.ExternalId` vào `BatchNo` và đặt
`CustomerReplyStatus = WAITING`. Backend không gán `SampleRequest.FormulaId` ở bước này. Sau khi lưu thành công, backend tạo
message InternalMail topic `SampleRequestSampleSent` với nội dung có giờ gửi, khối lượng mẫu và nhắc Sale ghi nhận.

Khi Sample Request đang `SampleSent`, PATCH Sample Request có thể gửi `formulaId` để xác nhận khách đã chấp nhận
công thức của Trial pending mới nhất. Shortcut này dành cho `ApplicationRoleSets.PLM.FormulaSelectors`; riêng
`LabUser` cũng được dùng khi hồ sơ đồng thời có customer `KH_VIETAUS` và `RequestType` nội bộ. Backend tự kiểm tra
hai điều kiện của ngoại lệ Lab từ dữ liệu đã lưu, không tin cờ do FE gửi, và vẫn kiểm tra company/customer scope cùng
`expectedUpdatedDate`. Formula phải thuộc đúng product, đang active, có status `SampleSent`
và phải khớp Trial `SampleSent`/`WaitingCustomerFeedback`/`PriceQuote` mới nhất. Backend mặc định
`CustomerReplyStatus = APPROVED`, cập nhật Trial `Approved`, Formula `Completed`, chọn Formula cho product và chuyển
Sample Request sang `Completed` trong cùng `SaveChanges`.

Với dữ liệu legacy không có bất kỳ Trial active nào, cùng shortcut này tự tạo Trial kế tiếp (`TrialNo = max + 1`) gắn
Formula được chọn, lưu snapshot customer/product/Formula và duyệt Trial ngay trong cùng transaction. Trial fallback được
ghi nhận tương đương luồng đã gửi–đã nhận–khách duyệt: `SentDate` lấy từ `SampleRequest.SendDate`, fallback sang
`RealDeliveryDate`, rồi mới là thời điểm chốt Formula; `SentByEmployeeId` chỉ kế thừa từ `SampleRequest.SendBy` nếu có;
`RequestReceivedDate`, `FinishedDate`, `CustomerReplyStatus = APPROVED`, `CustomerReplyDate` và người xác nhận là thời điểm/
Sale đang chốt. `DeliveredSampleQuantityKg` không được suy diễn vì không có dữ liệu giao thực tế. Fallback không áp dụng nếu
Request đã có Trial active ở trạng thái khác, nhằm không bỏ qua lifecycle đang tồn tại. FE nên mở dialog xác nhận rõ side effect
trước khi gửi PATCH.

Payload xác nhận công thức hoàn thành:

```json
{
  "status": "Completed",
  "sampleRequestId": "00000000-0000-0000-0000-000000000000",
  "expectedUpdatedDate": "2026-07-28T10:30:00"
}
```

Khi `status = Completed`, backend chuyển Sample Request sang `Completed`, set công thức được chọn là selected formula của
product, và tạo message InternalMail topic `SampleRequestFormulaCompleted` với nội dung công thức hoàn thành, sẵn sàng cho
báo giá.

Nếu không gửi `sampleRequestId`, backend chỉ fallback cập nhật các Sample Request active đã trỏ sẵn tới `FormulaId`.

## Quy tắc chuyển trạng thái

`Services/Requests/SampleRequestStatusTransitionRules.cs` là nơi duy nhất gán trạng thái Sample Request cho các sự kiện lifecycle: tạo mới, đủ định danh Product, gửi mẫu, khách chấp nhận/không đạt/hủy và yêu cầu/ra quyết định cập nhật Formula. Handler vẫn chịu trách nhiệm phân quyền, tải dữ liệu, audit, `UpdatedBy/UpdatedDate`, lưu transaction và message/notification; rule không có side effect.

Điều kiện gửi:

```text
Trạng thái trước đó là New.
Trạng thái sau khi lưu là InProgress.
Product có đủ Name và ColourCode.
```

Nội dung message gồm:

```text
Lab đã bắt đầu xử lý yêu cầu phối mẫu {SampleRequest.ExternalId}.
Mã màu: {Product.ColourCode}
Tên sản phẩm: {Product.Name}
```

Message này đi qua `SendSampleRequestMessageCommand`, publish topic `SampleRequestUpdated` (`plm.sample_request.updated`) và title `Lab đã bắt đầu xử lý yêu cầu phối mẫu`. BE chỉ gửi lúc chuyển trạng thái lần đầu để tránh spam khi Lab lưu lại nhiều lần.

Các điểm đang gửi:

```text
PatchSampleRequestCommandHandler: PATCH trực tiếp product name/colour code và không defer save.
```

Luồng approve data-change đang dùng `DeferSaveChanges = true` nên không tự gửi message start-processing trong `PatchSampleRequestCommandHandler`, để tránh side effect lồng khi xử lý decision.

## Yêu cầu báo giá từ Sample Request

Hai vị trí FE là báo cáo gửi mẫu và màn Formula dùng chung một nghiệp vụ:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests
Content-Type: application/json

{
  "formulaId": "guid-or-null",
  "message": "Ghi chú tùy chọn, tối đa 1000 ký tự",
  "isUrgent": false
}
```

`formulaId` là tùy chọn. Backend resolve nguồn theo thứ tự: Formula FE chỉ định, Formula của active trial có
`TrialNo` lớn nhất, rồi `SampleRequest.FormulaId`. Nếu cả ba không có Formula active đúng company/product,
yêu cầu vẫn được gửi ở cấp Product với `formulaSelectionSource = ProductOnly`; backend không tự chọn một Formula
bất kỳ khác của Product. Sample Request `New` hoặc `Cancelled` không được gửi yêu cầu báo giá.

Message yêu cầu báo giá do người dùng gửi chỉ ghi vào **nhóm Giá** của conversation SampleRequest, tái sử dụng hoặc tạo nhóm
theo writer/membership hiện có; không ghi bản sao chứa giá vào Chung. Notification trỏ đúng
conversation/message nhóm Giá. Người nhận chuông là President active cùng company, giao với
membership Giá và quyền giá bán hiện hành; loại sender và mute. Lab được yêu cầu báo giá
theo quyền hiện có nhưng không tự có quyền đọc nhóm Giá vì gửi yêu cầu.
Không tạo Quotation, đổi trạng thái hay duyệt giá; giữ rule private/internal.
Áp dụng lần gửi mới, không tự di chuyển hoặc gửi lại tin cũ. Thông báo kỹ thuật xác nhận công thức
có workflow event vẫn đi Chung theo luồng riêng, không chứa giá người dùng nhập.

Response dùng contract `SendInternalMessageResultDto`, gồm `conversationId`, `messageId`, `notificationId`.
Payload message/notification có `contentType = SampleRequestPriceQuoteRequested` và block
`priceQuoteRequest` chứa Sample Request, Product, Formula đã resolve và action
`SampleRequest.OpenPriceQuote`. Action parameters gồm `sampleRequestId`, `sampleRequestExternalId`, `productId`,
`productCode`, `formulaId`; FE tự ánh xạ action code sang route của client. Payload không chứa giá, cost hoặc margin; recipient đọc
dữ liệu giá hiện hành tại API pricing. Topic được tái sử dụng là `SampleRequestPriceQuoteRequested = 23`,
`topicCode = plm.sample_request.price_quote.requested`, category `sample_request`, event group `quotation`.

Tab Báo giá trong Notification Hub lấy conversation từ
`GET /api/v1/internal-mail/conversations?relatedType=SampleRequest&eventGroupCode=quotation`, không dùng notification
feed làm nguồn thread. Cách này giữ yêu cầu trong tab của cả sender dù sender không nhận self-notification.

