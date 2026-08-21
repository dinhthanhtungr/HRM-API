# Sample Requests

## Sample trial report

Dialog **Phản hồi khách hàng/Ghi chú phản hồi** dùng composer `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback`. Endpoint chỉ dành cho Sale, bắt buộc `idempotencyKey`, tự resolve customer từ Trial và trong một transaction vừa cập nhật phản hồi Trial vừa tạo CRM interaction/reference `SampleTrial/trialId` cùng follow-up task tùy chọn. Contract đầy đủ nằm trong `SampleRequestSampleTrials.README.md`.

Khi Lab gửi mẫu, message trong Notification Hub trả thêm `sampleReceiptAction` gắn chính xác với Trial vừa tạo. Sale/Leader xác nhận đã nhận mẫu bằng `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/confirm-receipt`. Nếu không gửi `sampleReceivedDate`, backend dùng thời điểm hiện tại; kết quả được lưu vào Trial và action chuyển từ `Pending` sang `Confirmed`.

Ngày Sale nhận mẫu được lưu vào `Trial.RequestReceivedDate` đã có sẵn. Action chuyển Trial từ `SampleSent` sang `WaitingCustomerFeedback` và dùng `UpdatedBy/UpdatedDate` để audit; không bổ sung cột database mới.

`GET /api/v1/plm/sample-requests/sample-trials` trả danh sách phân trang để FE dựng bảng theo dõi Lab giống báo cáo Excel. Query dùng Sample Request visible làm nguồn và left join trial: hồ sơ chưa có trial vẫn xuất hiện một dòng với `hasTrial = false`; hồ sơ có nhiều trial trả mỗi trial một dòng. Endpoint hỗ trợ keyword, khoảng ngày, Sample Request, customer, trial status, customer reply status và sorting. Dữ liệu luôn đi qua company/customer visibility; `additiveRate` và `labNote` trả `null` nếu current user không có quyền xem thông tin kỹ thuật PLM.

Các query keyword Sample Request hỗ trợ mã TP của chính yêu cầu, tên/mã màu Product và mã VU Formula liên quan.
Riêng danh sách trial còn tìm theo VU gắn trên Trial.

Contract chi tiết, mapping dữ liệu và script tạo bảng nằm trong `SampleRequestSampleTrials.README.md`.

Trial được tạo bằng `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials` và cập nhật bằng `PATCH /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}`. PATCH dùng `clearFields` whitelist để xóa field nullable; backend không dùng upsert và không tự đoán create/update.

## GetSampleRequestSummary Attachments

`GetSampleRequestSummary` returns sample request attachments as lightweight metadata under each summary item.
It also returns product category metadata (`categoryId`, `categoryExternalId`, `categoryType`, `categoryName`) so FE can show the product type on the summary list without calling form-options again.

The attachment query links data through:

```text
SampleRequest.AttachmentCollectionId -> AttachmentModel
```

The response includes `attachments` with `url`, `downloadUrl`, and `isImage`. It does not include file bytes, base64, or physical storage paths.

Frontend image preview should use:

```html
<img src="/api/v1/attachments/{attachmentId}" alt="" />
```

This keeps the summary API fast and lets the browser load/cache images separately.

## Detail And Write APIs

Sample request detail is loaded by id:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}
```

Create a sample request:

```http
POST /api/v1/plm/sample-requests
```

After a sample request is created, backend automatically creates an InternalMail message for the sample request thread and publishes a notification to Lab recipients, except for internal sample requests. FE may send `initialLabMessage` so Sale can review/edit the message content before submit. If `initialLabMessage` is blank, backend falls back to a generated summary from the key Sale-entered sample request and product fields. The message uses `SampleRequestNotificationType.GeneralMessage`, title `Yêu cầu phối mẫu mới`, the default sample request message topic, and does not expose extra sensitive payload beyond the normal message payload.

Internal sample requests do not need an InternalMail thread or notification. Backend suppresses SampleRequest message sending when `SampleRequest.RequestType` is an internal value (`private`, `Nội bộ`, `Noi bo`, `Internal`, including common space/underscore/hyphen variants) or when the customer is the internal customer `KH_VIETAUS`. The suppress rule is enforced in `SendSampleRequestMessageCommandHandler`, so create, direct patch notification, data-change/formula-change message flows, sample-sent/completed/cancelled lifecycle messages and other SampleRequest message calls no-op before creating a conversation or publishing a notification. FE should hide the recipient/message confirmation step for these cases; if FE still submits message fields, BE returns success with empty ids and no side effect.

Patch a sample request:

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}
```

Đa số field trên màn hình dùng PATCH trực tiếp rồi báo Lab. Riêng ba tiêu chuẩn `product.food_safety`,
`product.rohs_standard`, `product.reach_standard` do Sale/Leader đề xuất bắt buộc đi qua luồng Lab
duyệt; PATCH trực tiếp (kể cả `clearFields`) bị từ chối. Người thuộc
`ApplicationRoleSets.PLM.ProductTechnicalEditors` vẫn có thể sửa trực tiếp:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests
```

Lab duyệt hoặc từ chối một phần/toàn bộ đề xuất ngay trong Notification Hub:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests/{messageId}/decision
```

Request tạo đề xuất chỉ nhận mã field ổn định và giá trị mới. Backend tự đọc giá trị cũ từ
`Product`, không tin giá trị cũ do FE gửi:

```json
{
  "message": "Nhờ Lab kiểm tra các thông tin kỹ thuật",
  "isUrgent": false,
  "changes": [
    { "fieldCode": "product.usage_rate", "newValue": 2.5 },
    { "fieldCode": "product.food_safety", "newValue": true }
  ]
}
```

Các mã field được phép nằm tập trung tại `SampleRequestDataChangeFieldCatalog`; FE không được gửi
tên property C# tùy ý. Nhóm hiện tại gồm thông tin kỹ thuật của `Product` như mã/tên màu, phụ gia,
tỷ lệ sử dụng, tiêu chuẩn an toàn, điều kiện sử dụng, khối lượng và ghi chú sản phẩm.

Request quyết định:

```json
{
  "decision": "Approve",
  "fieldCodes": ["product.usage_rate"],
  "reason": null
}
```

`fieldCodes` rỗng nghĩa là xử lý toàn bộ field còn `Pending`. `Reject` bắt buộc có `reason`.
Lab không gửi lại giá trị mới; backend lấy đúng proposal trong message gốc. Khi approve, backend
kiểm tra giá trị hiện tại vẫn bằng giá trị cũ lúc Sale gửi rồi tái sử dụng `PatchSampleRequestCommand`
để áp dụng. Nếu dữ liệu đã đổi, quyết định bị chặn và FE phải reload. Reject chỉ cập nhật trạng thái
proposal và gửi phản hồi, không patch dữ liệu nghiệp vụ.

Mỗi quyết định tạo một reply trong cùng `InternalConversation` và notification cho người Sale đã gửi.
Message gốc lưu trạng thái từng field (`Pending`, `Approved`, `Rejected`) trong `PayloadJson`; không có
bảng/cột hay migration mới. `GET /api/v1/plm/sample-requests/{sampleRequestId}/messages` trả thêm
`action`, còn Notification Hub dùng `payloadJson` sẵn có từ API InternalMail để render action card.

Notification của luồng này publish qua `INotificationService.PublishAsync` với các topic cụ thể:

```text
plm.sample_request.data_change.requested -> Lab và participant liên quan
plm.sample_request.data_change.approved  -> Sale đã gửi yêu cầu
plm.sample_request.data_change.rejected  -> Sale đã gửi yêu cầu
```

Cả ba thuộc category `SampleRequest`. Notification payload chỉ giữ metadata định vị thread/message/sample;
proposal đầy đủ không được đưa vào SignalR hoặc Web Push. SignalR và Web Push vẫn chỉ làm tín hiệu tải lại
dữ liệu qua API có phân quyền.

Sale chỉ đề xuất trên hồ sơ do mình tạo/phụ trách; Leader và super user có scope rộng hơn. Lab hoặc
super user mới được quyết định. PATCH trực tiếp field kỹ thuật kiểm tra role trong
`ApplicationRoleSets.PLM.ProductTechnicalEditors`; mọi query/write đều khóa theo current `CompanyId`.

Read change history for one sample request:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}/history
```

`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` trực tiếp ghi audit cho các field thay đổi của `SampleRequests`
và `Products` với reason `SampleRequestDirectPatch`. Luồng duyệt đề xuất thay đổi dữ liệu vẫn dùng reason
`SampleRequestDataChangeApproval`. Handler chỉ ghi audit theo một nhánh (`IsDataChangeApproval` hoặc PATCH trực tiếp),
và helper audit tự bỏ qua khi không có field thật sự thay đổi, nên một lần lưu không tạo audit trùng cho cùng source.

After FE directly patches `sample_request.*` or whitelisted `product.*` fields that do not need Lab approval, FE can notify Lab with:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/direct-patch-notifications
```

This endpoint does not update business data. It only creates an InternalMail message in the active SampleRequest conversation and publishes a notification after the PATCH has already succeeded. The request must include `idempotencyKey`, `message`, optional `recipientEmployeeIds`, and `changes[]`. `changes[].fieldCode` only accepts direct-notify field codes from the whitelist; it explicitly excludes `product.food_safety`, `product.rohs_standard`, and `product.reach_standard` because those fields use the Lab approval flow.

```json
{
  "idempotencyKey": "019fb000-0000-7000-9000-000000000001",
  "message": "Sale da dieu chinh thong tin yeu cau phoi mau.",
  "recipientEmployeeIds": [],
  "changes": [
    {
      "fieldCode": "sample_request.sample_quantity",
      "label": "So luong mau thu",
      "oldValue": 1,
      "newValue": 2
    }
  ]
}
```

Direct notification accepts these `sample_request.*` fields:

```text
sample_request.expected_quantity
sample_request.expected_price
sample_request.sample_quantity
sample_request.number_delivery_sample_date
sample_request.package
sample_request.bag_weight
sample_request.customer_product_code
sample_request.request_delivery_date
sample_request.expected_delivery_date
sample_request.real_delivery_date
sample_request.request_test_sample_date
sample_request.response_delivery_date
sample_request.expected_price_quote_date
sample_request.real_price_quote_date
sample_request.info_type
sample_request.other_comment
sample_request.sale_comment
sample_request.additional_comment
sample_request.request_type
```

Direct notification also accepts these `product.*` fields so Sale can save direct changes and notify Lab instead of creating a pending approval card:

```text
product.colour_code
product.name
product.colour_name
product.additive
product.usage_rate
product.delta_e
product.requirement
product.expiry_type
product.storage_condition
product.lab_comment
product.procedure
product.recycle_rate
product.taical_rate
product.application
product.product_usage
product.polymer_matched_in
product.code
product.end_user
product.max_temp
product.weather_resistance
product.light_condition
product.visual_test
product.return_sample
product.is_recycle
product.weight
product.unit
product.other_comment
```

The message content type is `SampleRequestDirectPatchNotification`. Notification topic is `SampleRequestDirectPatchNotified` (`plm.sample_request.direct_patch.notified`) with category `SampleRequest`. Payload includes `sampleRequestId`, `externalId`, `changedByEmployeeId`, `idempotencyKey`, `changes[]`, `conversationId`, and `messageId`. Retrying the same `idempotencyKey` in the same SampleRequest conversation returns the existing message and does not publish a duplicate notification.

The history endpoint treats the sample request and its product detail as one business record. It loads audit logs from `Audit.AuditLogs` where `SchemaName = SampleRequests` and:

```text
TableName = SampleRequests, RecordId = SampleRequest.SampleRequestId
TableName = Products,       RecordId = SampleRequest.ProductId
```

Rows with the same `CorrelationId` are grouped into one timeline item so a single save operation can show both `SampleRequests` fields and `Products` fields together. Each changed field keeps `details[].source` so the frontend can still show whether the field came from `SampleRequests` or `Products`. Detail, history, and messages are scoped with `ICustomerVisibilityService.ApplySampleRequestVisibility`; users cannot read a sample request by id unless the request belongs to a customer in their current visibility scope.

History is also filtered by PLM field visibility. Users outside `ApplicationRoleSets.PLM.ProductTechnicalEditors` do not receive restricted `Products` technical fields such as `Requirement`, `LabComment`, `Procedure`, technical rates, standards, tests, and internal product comments. Audit rows remain stored fully in `Audit.AuditLogs`; only the API response is filtered, and a timeline card with no visible details is omitted.

`POST` creates a new attachment collection automatically. File upload still uses the attachment endpoints.

`PATCH` updates only fields included in the request body. It can update core sample request fields and a small set of product-detail fields used by the detail screen.

Production orders are still returned under `productionOrders` by linking:

```text
SampleRequest.ProductId -> MfgProductionOrder.ProductId
```

`MfgProductionOrders` does not own attachments in the current database schema, so do not add or query `manufacturing.MfgProductionOrders.attachment_collection_id`.

Do not key production order lookups by product code, colour code, or external-id snapshots. Use `ProductId` when both sides have it. Snapshot strings are display/fallback data only.

Each production order also returns the currently selected manufacturing formula header by linking:

```text
MfgProductionOrder.MfgProductionOrderId -> ProductionSelectVersion.MfgProductionOrderId
ProductionSelectVersion.ManufacturingFormulaId -> ManufacturingFormula
```

The selected manufacturing formula rule is:

```text
ProductionSelectVersion.ValidFrom IS NOT NULL
ProductionSelectVersion.ValidTo IS NULL
```

`productionOrders[].formulaExternalId` is populated from the selected manufacturing formula when one exists. If no selected manufacturing formula exists, it falls back to `MfgProductionOrder.FormulaExternalIdSnapshot`.

`productionOrders[].selectedManufacturingFormula` is a lightweight header only. It includes the manufacturing formula id, external id, name, total price, and active material count. It does not include manufacturing formula materials, so summary payloads stay small.

Khi `PATCH /api/v1/plm/formulas/{formulaId}/status` chuyển Formula sang `SampleSent`, backend đồng thời chuyển các
Sample Request active đang trỏ tới `FormulaId` đó sang `SampleRequestStatus.SampleSent`.

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
hoặc chọn Formula khi Sample Request đang `SampleSent`; FE phải gọi action nghiệp vụ tương ứng.

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

Sale từ chối hoặc Lab hủy yêu cầu:

```text
SampleRequest.FormulaId giữ nguyên
SampleRequest.Status = Completed
Formula đề xuất.Status = Rejected khi Sale từ chối, hoặc Cancelled khi yêu cầu bị hủy
```

Mỗi hành động `gửi mẫu`, `chọn công thức hoàn thành`, `yêu cầu cập nhật công thức`, `chấp nhận`, `từ chối` và `hủy yêu cầu` phải tạo message trong cùng Sample Request conversation và publish notification cho các participant liên quan. Không tạo conversation/group mới cho các sự kiện này.

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
```

Tất cả topic này thuộc category `SampleRequest`. Payload notification chỉ chứa metadata định vị thread/message/formula/sample; nội dung chi tiết và action card được đọc lại từ API message có phân quyền.

Các giá formula trong summary (`selectedFormula.totalPrice`, `productionOrders[].selectedManufacturingFormula.totalPrice`,
`productionOrders[].standardManufacturingFormula.totalPrice` và `previousTotalPrice`) chỉ trả khi current user thuộc
`ApplicationRoleSets.PLM.FormulaPriceViewers`; ngoài nhóm này backend trả `null`.

Manufacturing formula materials are lazy-loaded from:

```http
GET /api/v1/plm/manufacturing-formulas/{manufacturingFormulaId}/materials
```

`productionOrders[].standardManufacturingFormula` is loaded through:

```text
MfgProductionOrder.ProductId -> ProductStandardFormula.ProductId
```

The current standard rule is:

```text
ProductStandardFormula.ValidTo IS NULL
```

It returns the current standard formula header, the date it became valid, and the closest previous standard formula when one exists.

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
công thức của Trial pending mới nhất. Shortcut này chỉ dành cho `ApplicationRoleSets.PLM.FormulaSelectors`, vẫn kiểm tra
company/customer scope và `expectedUpdatedDate`. Formula phải thuộc đúng product, đang active, có status `SampleSent`
và phải khớp Trial `SampleSent`/`WaitingCustomerFeedback` mới nhất. Backend mặc định
`CustomerReplyStatus = APPROVED`, cập nhật Trial `Approved`, Formula `Completed`, chọn Formula cho product và chuyển
Sample Request sang `Completed` trong cùng `SaveChanges`. FE nên mở dialog xác nhận rõ side effect trước khi gửi PATCH.

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

## Query Organization

`GetSampleRequestSummaryQueryHandler` should stay focused on query orchestration.

Keep query-only projection models in:

```text
Queries/GetSampleRequestSummary/Models/GetSampleRequestSummaryModels.cs
```

Keep query-specific data loading in:

```text
Queries/GetSampleRequestSummary/Services/SampleRequestSummaryRelatedDataLoader.cs
```

This loader is static and receives `IPLMReadDbContext` from the handler. It is not registered in DI because it is a query-local helper without state.

Keep reusable helpers out of the handler:

```text
SampleRequestAdditiveHelper.ResolveGroupCode(...)
AttachmentFileHelper.IsImageFile(...)
AttachmentFileHelper.BuildUrl(...)
```

## Data Change Field Contract

`POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests` supports stable field codes from `SampleRequestDataChangeFieldCatalog`. FE bắt buộc dùng flow này khi Sale/Leader đổi `product.food_safety`, `product.rohs_standard` hoặc `product.reach_standard`; các field khác chỉ dùng khi nghiệp vụ yêu cầu approval. Với thay đổi chỉ cần báo Lab, dùng `PATCH` rồi `direct-patch-notifications`.

Sample request fields currently supported:

```text
sample_request.expected_quantity
sample_request.expected_price
sample_request.sample_quantity
sample_request.package
sample_request.bag_weight
sample_request.customer_product_code
sample_request.request_delivery_date
sample_request.expected_delivery_date
sample_request.request_test_sample_date
sample_request.expected_price_quote_date
sample_request.info_type
sample_request.other_comment
sample_request.sale_comment
sample_request.additional_comment
sample_request.request_type
```

Date fields must be sent as valid date strings. Number/date fields require a concrete value in this approval flow; string fields may be sent as `null` to clear them to blank through the existing patch semantics.

## Summary Visibility

`GetSampleRequestSummary` applies `CustomerVisibilityService.ApplySampleRequestVisibility` before search, filters, sorting, and paging. Results are scoped to the current user's company and customer visibility; `companyId` can only narrow results inside that current company.

For `LabUser`, Sample Request visibility includes the internal customer `KH_VIETAUS` (`CustomerVisibilityConstants.RestrictedCustomerId`) so `/api/v1/plm/sample-requests/summary` and other Sample Request read APIs can show internal sample requests to Lab. This exception applies to Sample Request visibility only; sale order and production order dashboard sources keep their own internal-customer exclusion rules.

## Message Recipients

All Sample Request thread message types resolve default recipients through `SampleRequestRecipientResolver`. Static role/group rules live in `SampleRequestRecipientRules`. The only locked required recipients are active employees in roles `President` and `LabAdmin`. Default leader recipients are active leaders (`MemberInGroup.IsAdmin = true`) in the current company and are resolved by stable product category `ExternalId`:

- `PC` (Compound) -> `QAQC.RD` and `QAQC.MAU`.
- `PMA` (Masterbatch), `PPG` (Phụ gia) -> `QAQC.RD`.
- `PHM` (Hạt màu), `PBM` (Bột màu), `PDM` (Dry mix) -> `QAQC.MAU`.
- `PKH`, `PTP`, `PMS`, `PPB`, or unknown/unmapped category -> fallback `QAQC.RD` and `QAQC.MAU`.

Regular `LabUser` employees are not required recipients unless FE/user adds them or they already belong to the existing conversation. For an existing conversation, preview returns every active participant and the sample request manager, except the current sender, as locked selected recipients because real send always keeps them in the thread participant set. This keeps `selectedRecipients` aligned with the people who will actually receive the message.

Internal sample requests (`RequestType` internal or customer `KH_VIETAUS`) skip this recipient resolution during real send. Preview with an existing `contextId` also returns empty `requiredRecipients`, `suggestedRecipients`, `selectedRecipients` and `canAddRecipients = false`.

Both preview and real send use the same resolver:

```text
SampleRequestMessageRecipientResolver -> SampleRequestRecipientResolver
SendSampleRequestMessageCommandHandler -> SampleRequestRecipientResolver
```

FE can preview default recipients before submit through the generic recipient preview API:

```http
POST /api/v1/message-recipients/preview
```

Create form payload example:

```json
{
  "contextType": "SampleRequest",
  "actionType": "Create",
  "draftManagerBy": "00000000-0000-0000-0000-000000000000",
  "draftCategoryId": "00000000-0000-0000-0000-000000000000"
}
```

Existing sample request message/data-change payload example:

```json
{
  "contextType": "SampleRequest",
  "actionType": "DataChangeRequest",
  "contextId": "00000000-0000-0000-0000-000000000000",
  "selectedRecipientEmployeeIds": []
}
```

Direct patch notification preview uses the same resolver:

```json
{
  "contextType": "SampleRequest",
  "actionType": "SampleRequestDirectPatch",
  "contextId": "00000000-0000-0000-0000-000000000000",
  "selectedRecipientEmployeeIds": []
}
```

The response contains `requiredRecipients`, `suggestedRecipients`, and `selectedRecipients`. `President`/`LabAdmin` users are locked required recipients. For an existing thread, active participants and the sample request manager are also locked, except the current sender. Category-matched QAQC leaders are returned as default, unlocked recipients and are selected by default on the first preview. FE may remove or add these optional recipients, then submit the selected optional ids back through:

```text
CreateSampleRequestCommand.InitialLabRecipientEmployeeIds
CreateSampleRequestDataChangeRequestCommand.RecipientEmployeeIds
CreateSampleRequestDirectPatchNotificationCommand.RecipientEmployeeIds
SendSampleRequestMessageCommand.ExtraRecipientEmployeeIds
```

BE still validates all submitted recipient employee ids against the current company and active employees, and only adds locked `President`/`LabAdmin` recipients when FE does not send them. Existing active conversation participants remain participants so the history is not hidden from them.

For the initial preview, FE omits `selectedRecipientEmployeeIds` so category-matched leaders are selected by default. After the user edits the recipient list, FE sends the current optional ids; sending an empty array explicitly removes every optional default recipient.

## Conversation Subject Sync

Thread InternalMail của Sample Request dùng `InternalConversation.Subject` làm tên cuộc trao đổi hiện tại.

Subject được build theo format:

```text
{SampleRequest.ExternalId}
{SampleRequest.ExternalId} - {Product.ColourCode}
```

Conversation lịch sử có thể vẫn lưu prefix `Trao đổi yêu cầu phối mẫu`. API danh sách conversation không
backfill hoặc sửa dữ liệu đó; response bổ sung `displayTitle` và loại prefix legacy tại tầng presentation.
FE dùng `displayTitle` ở cột inbox, còn màn hình chi tiết vẫn có thể dùng `subject` đầy đủ.

Khi Lab lưu `ColourCode` hoặc khi PATCH SampleRequest làm đổi product/colour code, BE gọi `SampleRequestConversationSubjectService.SyncSubjectAsync` để cập nhật đúng một conversation đang active của sample request đó. Luồng này chỉ cập nhật metadata thread hiện tại, không sửa body message cũ, notification cũ hoặc payload lịch sử.

Nếu `ColourCode` thực sự đổi sang một giá trị mới, BE đồng thời cập nhật mã snapshot và subject conversation của
các báo giá `Draft` cùng company đang dùng `ProductId` đó. Báo giá đã gửi hoặc không còn là `Draft` vẫn giữ nguyên
snapshot; message, notification và payload lịch sử không bị viết lại.

Các điểm đang sync:

```text
SendSampleRequestMessageCommandHandler: tạo conversation mới với subject có colour code nếu đã có.
PatchSampleRequestCommandHandler: Lab lưu product/colour code bằng PATCH SampleRequest, hoặc approve data-change có defer save.
```

## Lab Start Processing Notification

Khi Lab lưu thông tin làm Sample Request chuyển trạng thái từ `New` sang `InProgress`, BE tự gửi một message công khai vào thread Sample Request.

Khi **tạo mới** Sample Request, backend cũng dùng cùng rule: nếu Product đã có đủ `Name` và `ColourCode` sau khi resolve/tự sinh mã, Sample Request được tạo thẳng ở `InProgress`; nếu thiếu một trong hai thì vẫn là `New`. Rule này áp dụng cho cả Product có sẵn và Product được tạo cùng request.

## Quy tắc chuyển trạng thái

`Rules/SampleRequestStatusTransitionRules.cs` là nơi duy nhất gán trạng thái Sample Request cho các sự kiện lifecycle: tạo mới, đủ định danh Product, gửi mẫu, khách chấp nhận/không đạt/hủy và yêu cầu/ra quyết định cập nhật Formula. Handler vẫn chịu trách nhiệm phân quyền, tải dữ liệu, audit, `UpdatedBy/UpdatedDate`, lưu transaction và message/notification; rule không có side effect.

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
