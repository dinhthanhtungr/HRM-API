# Luồng legacy, chip và mẫu kg

Việc xuất hiện theo vai trò và contract nút FE: [ROLE_WORK_QUEUES.md](ROLE_WORK_QUEUES.md).
Lịch quản lý độc lập: [WORK_SCHEDULE.md](WORK_SCHEDULE.md).

## Phạm vi và nguồn sự thật

BE chọn luồng bằng **SampleRequest.CreatedDate**, theo timestamp nghiệp vụ hiện có:

- Trước `01/11/2026 00:00`: `Legacy`.
- Từ mốc đó: `ChipAndSample`.

Đây là rule luồng, độc lập với rule đưa vào danh sách công việc tại `WORK_SCOPE.md`. Admin chọn Theo dõi tiếp không đổi mode. FE không tự so sánh ngày để chọn API. `flow.workflowMode`, `flow.stage`, `flow.allowedActions` do BE trả qua workflow và các dòng report. Capability không thay thế company/customer visibility hoặc ownership.

Không tạo/chạy migration, không thêm bảng/cột. `SampleTrial.DeliveryKind` là property **NotMapped**, tính từ `DeliveredSampleQuantityKg` của chính lần gửi: null = Unknown, 0 = ColorChip, >0 = TrialSample. Không suy loại lịch sử từ `SampleRequest.SampleQuantity` hiện tại. `FormulaStatus.Rework` lưu qua cột Status string hiện có. Formula.UpdatedDate được đánh dấu concurrency token trên cột hiện có, không thay schema.

## Legacy

Tham chiếu lịch sử Git: `450239c`, UpdateFormulaStatusCommandHandler. API status hiện có vẫn là một điểm vào; BE gọi `LegacySampleDeliveryService` khi gửi hồ sơ trước mốc. Yêu cầu Formula Approved, cùng Product/company và hồ sơ active trong write scope. Không bắt kg, phản hồi giá hoặc báo cáo Trial; không tạo Trial giả. Cập nhật Formula/SampleRequest sang SampleSent, ngày/người gửi, audit, snapshot và outbox trong transaction. Dữ liệu Trial cũ nếu có được giữ nguyên.

Sale phụ trách có thể xác nhận Complete qua workflow/actions khi Formula đã SampleSent. Chọn Formula qua PATCH không còn được dùng như đường tắt tạo Trial duyệt giả; dùng action Complete. Quyền xem, company, employee active và token được kiểm tra lại ở server.

## Chip: không có kg yêu cầu

1. Lab chuẩn bị/duyệt VU. Gửi chip không cần khách đồng ý giá, FE không có ô kg; BE lưu lượng thực gửi bằng 0.
2. Gửi chip -> Trial WaitingCustomerFeedback, Formula SampleSent và khóa sửa thành phần.
3. Sale chưa có phản hồi -> vẫn khóa. Không dùng WAITING làm bằng chứng mở sửa.
4. Sale ghi phản hồi thực tế Đạt/Không đạt -> Trial lưu kết quả, Formula Rework, SampleRequest InProgress. Chip Đạt **không tự chọn Formula, không tự hoàn thành request**.
5. Không đạt -> Lab chỉnh/lưu cùng VU rồi gửi chip vòng mới, giữ snapshot/lịch sử trước. Việc chính vẫn là `ReworkFormula` cho đến khi gửi; `flow.allowedActions` đồng thời có `Send` cho Lab nếu VU đang Rework/Approved, không yêu cầu kg và lần chip mới nhất là Failed. Không suy khả năng gửi chỉ từ mã việc chính. Gửi lại khóa tiếp và chuyển việc sang Sale phản hồi lần mới.
6. Đạt -> Lab chỉnh theo mẫu khách chọn rồi FinalizeChip: Formula Completed, request chưa Completed.
7. Sale chọn Complete (không bắt báo cáo/ghi chú thêm), hoặc RequestSample với kg >0 để đi tiếp cùng request.

ReopenChip dành cho capability Lab, chỉ ở nhánh chip đã chốt, hồ sơ còn mở và VU chưa có bằng chứng đã gửi kg. Sale không được cấp quyền mở sửa qua action này. Sự kiện mở lại không xóa sự kiện chốt trước.

## Mẫu kg

- Sale yêu cầu kg dương; việc này không tự tạo lần đã gửi. Sau đã gửi chip, thay đổi khối lượng qua workflow action RequestSample thay vì PATCH field để bỏ qua giai đoạn.
- Lab chốt VU sau chip không đồng nghĩa request hoàn thành. VU Completed sau chip được dùng nguyên nội dung để gửi mẫu kg; không cần mở sửa chỉ để chạy mẫu.
- Chỉ cho gửi khi có khách đồng ý giá của **đúng VU**, chưa bị thu hồi/phản hồi giá mới thay thế. Theo quyết định user, giữ xác nhận giá khi chỉnh tỷ lệ trong cùng VU; không tự gán sang VU khác.
- Gửi kg phải có lượng thực gửi >0, ghi một Trial riêng và chuyển Formula SampleSent. VU đã gửi kg không được sửa thành phần; kiểm tra cả lịch sử, không chỉ Status hiện tại.
- Mẫu kg Đạt -> hoàn thành theo luồng phản hồi hiện có. Không đạt -> lưu kết quả, không mở sửa VU cũ; FE thể hiện NewRequestRequired. Chỉnh công thức phải dùng VU mới và hồ sơ mới. Chưa tự gán kết quả Không đạt thành Hủy hoặc Hoàn thành khi user chưa chốt trạng thái kết thúc đó.
- Không mở lại SampleRequest Completed/Cancelled chỉ vì nhập kg. Không tự thêm lineage/clone hồ sơ khi chưa có thao tác người dùng.

## Báo giá và phản hồi giá

Yêu cầu Báo giá không có kg tiếp tục nhánh công thức -> duyệt giá -> Sale ghi ngày báo giá -> phản hồi giá. Ghi RealPriceQuoteDate không tự xong việc Sale. Phản hồi gồm PriceAccepted, PriceRejected, PriceNoResponse; ngày, ghi chú/căn cứ được lưu thành WorkflowEvents. Accepted tái sử dụng kiểm tra giá/căn cứ hiện hành. Rejected/NoResponse yêu cầu ngày báo giá đã lưu và ghi chú/căn cứ giá, không tự gửi một đề nghị duyệt giá mới hoặc hủy hồ sơ. Chưa tự tạo task CRM từ phản hồi giá; công việc vẫn ở hàng đợi Sale theo giai đoạn.

Sau khách đồng ý giá của yêu cầu Báo giá, Sale có action Complete. Với thẻ màu/mẫu kg, đồng ý giá không tự hoàn thành request. Khối lượng >0 được ưu tiên nhận diện giai đoạn chạy mẫu thay vì chỉ nhìn nhãn InfoType.


Khi VU của hồ sơ đã có lần gửi kg active trong cùng company (kể cả trên hồ sơ khác), nhánh chip chuyển `NeedsReview` để quản lý kiểm tra dữ liệu; không quảng cáo gửi lại/chốt/mở lại chip hoặc phản hồi chip mà mutation sẽ chặn. Không tự tạo hồ sơ mới hoặc đổi lịch sử. Lần kg hiện tại vẫn giữ việc Sale phản hồi và nhánh Không đạt tạo yêu cầu mới. Báo giá không kg đã có bằng chứng Lab xác nhận vẫn đi tiếp giá; nếu chưa xác nhận mà VU đang SampleSent/Completed thì chuyển kiểm tra thay vì hiện nút xác nhận không hợp lệ.

Sau khi đã có lần gửi, gửi lại chip và tiếp tục từ chip sang kg phải dùng đúng VU của lần đó. Không dùng trạng thái Approved của một VU khác để vượt bước; trước lần gửi đầu tiên vẫn được chọn công thức hợp lệ theo quy tắc hiện có.

## Báo giá không kg: Lab chốt VU rồi Sale hoàn thành hồ sơ

Hồ sơ hiện hành loại Báo giá, không yêu cầu kg: sau khi có xác nhận công thức Lab, ngày đã báo giá và khách đồng ý giá đúng VU, công thức Approved chuyển việc sang Lab với `nextActionCode=FinalizeQuotation`, `flow.stage=FinalizeQuotationFormula` và `flow.allowedActions=["FinalizeQuotation"]` theo capability Lab. Chưa đồng ý giá không được hiện hoặc gọi bước này. VU thiếu/không hợp lệ hoặc trạng thái chưa thể chốt chuyển kiểm tra; không tự bỏ qua xác nhận giá.

Lab gọi workflow/actions với `action=FinalizeQuotation`, token hồ sơ và token VU. BE kiểm tra lại dữ liệu/quyền/company, chốt VU Completed, lưu snapshot, cập nhật token hồ sơ và ghi sự kiện `QuotationFormulaFinalized`/outbox trong một transaction. Không chọn VU sản xuất và không hoàn thành SampleRequest ở bước Lab. Không có báo cáo bổ sung bắt buộc; công thức phải được lưu trước khi gọi action.

Sau đó Sale phụ trách nhận `CompleteRequest`/action `Complete` để chuyển hồ sơ Completed và chọn VU, rồi dùng lên đơn hàng. Hồ sơ Báo giá đã có VU Completed và khách đồng ý đúng VU vẫn được Sale hoàn thành, không tạo sự kiện Lab giả để backfill. Tạo đơn trực tiếp cũng kiểm tra liên kết sự kiện chốt báo giá: VU Completed còn thuộc hồ sơ hiện hành đang mở chưa được dùng lên đơn; Sale hoàn thành hồ sơ mới gỡ chặn này. Luồng legacy và chip/kg giữ các điều kiện riêng.


## API và tokens

- Gửi/duyệt VU: giữ API Formula status hiện có. Một route gửi cho cả legacy và hiện hành; mode do BE resolve. Legacy/chip không gửi kg; kg bắt buộc lượng dương. Gửi mẫu hiện hành không kèm chỉnh FormulaUpdate: lưu chỉnh sửa trước rồi gửi, tránh sửa ngầm VU đã chốt.
- `GET /api/v1/plm/sample-requests/{id}/workflow`: `item.flow` chứa mode, stage, FormulaId, requestedKg, token request/formula và danh sách action cho user hiện tại.
- `POST /api/v1/plm/sample-requests/{id}/workflow/actions`: body `action`, `expectedUpdatedDate`, `expectedFormulaUpdatedDate`; tùy action thêm `requestedKg`, `occurredAt`, `note`, `priceReference`, `quotationLineId`. Mode/status/company/actor không nhận từ FE. Route id là nguồn chuẩn.
- Action: FinalizeChip, ReopenChip, Complete, RequestSample, PriceAccepted, PriceRejected, PriceNoResponse. Send nằm trên status API hiện có, không gọi qua action endpoint.
- Trả OperationResult, HTTP 200 thành công/400 thất bại cho workflow actions. Token không khớp cần tải lại. Request-level transition dùng atomic lock theo UpdatedDate trong transaction; Formula có token riêng và version snapshot. Không xóa hay viết lại sự kiện cũ.

Các query ghi dữ liệu phải gọi `AsTracking()` sau khi ghép phạm vi khách hàng. Với Sale không có quyền xem toàn bộ, `AsNoTracking()` ở query khách hàng lồng có thể làm cả query hồ sơ thành không theo dõi; chỉ ghi được sự kiện mà không lưu thay đổi hồ sơ hoặc token. Các bước workflow, phản hồi và parent request của Trial/Formula giữ cùng phạm vi quyền nhưng khôi phục tracking trước khi sửa. Test persistence chạy handler thật với biểu thức visibility Sale bị giới hạn, đọc lại DB và kiểm tra token cũ không tạo trùng sự kiện/outbox.

## Quyền và side effects

- `plm.sample-request.workflow.lab`: mapping SampleRequestLabWorkflowEditors (LabUser/Admin/Developer/President). LabAdmin mặc định quản lý lịch qua capability riêng, không tự nhận việc kỹ thuật; xem rollout tại ROLE_WORK_QUEUES.md.
- `plm.sample-request.workflow.sale`: mapping FormulaSelectors; thao tác Sale mới còn bắt buộc **ManagerBy == EmployeeId**.
- Permission claims dùng seed baseline hiện có và script idempotent `seed-sample-flow-permissions.sql`; đây chỉ là cấp claim trên bảng sẵn có, không phải migration/schema. Chưa tự chạy; cần refresh JWT/login sau seed.
- Các thao tác mới lưu WorkflowEvents và outbox chung. Worker publish qua luồng thông báo hiện có; không thêm topic/numeric value, không đổi vận chuyển SignalR/Push hoặc recipient/mute rules. Payload không chứa thành phần công thức, cost hoặc giá tiền.
- BOM thử nghiệm không được gọi từ luồng này.

## Công việc, báo cáo và lên đơn

Các hàng đợi Lab/Sale lọc điều kiện trước phân trang. Flow read model được gắn theo batch trang, không gọi truy vấn riêng cho từng dòng. Hồ sơ thiếu thông tin lần gửi hiện có được đánh dấu NeedsReview, không tự giả là chip hay mở khóa.

Nguồn ngày hoàn thành báo cáo tổng hợp ưu tiên sự kiện Sale Complete; không lấy ngày chip Đạt làm ngày kết thúc sớm. Khách duyệt mẫu kg tiếp tục dùng ngày phản hồi. Lookup lên SaleOrder yêu cầu hồ sơ hiện hành Completed; legacy giữ điều kiện SampleSent/Completed theo contract cũ. CreateSaleOrder cũng chặn Formula có lần gửi thuộc hồ sơ hiện hành còn mở.

FE dùng cùng editor Formula và một form action ngắn: chip không kg, Complete chỉ xác nhận, RequestSample chỉ nhập kg, giá có ngày/căn cứ/ghi chú. Snapshot/card phân biệt Chip và Mẫu kg theo từng lần gửi; không ghi đè lịch sử.

## Kiểm thử và triển khai

Kết quả thao tác FE, validation, lỗi đã sửa và giới hạn kiểm tra ngày 03/11/2026: [UAT_FLOW_20261103.md](UAT_FLOW_20261103.md).

BE: SampleFlowRulesTests (cutoff, không suy loại theo kg hiện tại, chip mở Rework, no-response giữ khóa, chốt chip/Sale quyết định, gate giá cùng VU, kg không đạt, company/owner/capability, SQL PostgreSQL dịch trước phân trang, property NotMapped). Chạy cùng các test workflow/CRM/SaleOrder hiện hành. FE có test payload tokens, Complete không gửi báo cáo, RequestSample không giả ngày gửi và hồi quy dialog/progress.

Triển khai BE trước FE; cập nhật permission claims và refresh phiên. Không chạy migration/database update. Không tự sửa hồ sơ lịch sử, restart API đang được người dùng/debugger chạy hoặc ghi dữ liệu test vào DB thật. Build/test không thay thế UAT trên binary đã triển khai.
