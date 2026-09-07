# Sample Requests

## Product category options

## Lookup cho Sale Order

`GET /api/v1/plm/sample-requests/lookup` giữ nguyên hành vi lookup thông thường. Khi dropdown được gọi từ màn tạo Sale Order, FE truyền `forSaleOrder=true`; backend luôn khóa dữ liệu về `CompanyId` của user hiện tại và chỉ trả Sample Request có `status` là `SampleSent` hoặc `Completed`. `customerId` là bắt buộc trong mode này: chưa chọn customer thì trả mảng rỗng. Với customer thường, lookup dùng tập customer gồm customer đang chọn **và** customer nội bộ `KH_VIETAUS`. Riêng khi customer đang chọn có `ExternalId = KH_VIETAUS`, backend trả Sample Request hợp lệ của **mọi customer trong cùng company**. Rule được BE tự nhận diện từ customer đã lưu, không tin một cờ FE gửi lên. Mỗi item có thêm `formulaId`/`formulaExternalId`: ưu tiên Formula đang chọn trên Sample Request, nếu chưa có thì lấy Formula active của Trial active mới nhất; FE dùng hai field này để điền Formula cho dòng Sale Order và disable item nếu `formulaId = null`. Các filter `isActive`, keyword và `status` vẫn cùng áp dụng; keyword vẫn phải khớp record như lookup thông thường. Nếu `status` là trạng thái khác hai giá trị trên thì kết quả là danh sách rỗng.

`GET /api/v1/plm/sample-requests/form-options` only returns the active canonical product categories of the current company. Legacy categories remain in the database for historical records and are intentionally omitted from this form lookup.

| Code | Display name |
| --- | --- |
| `CMP` | Compound |
| `CMB` | Color masterbatch |
| `AMB` | Additive masterbatch |
| `PIG` | Bột màu |
| `VRG` | Hạt nhựa nguyên sinh |
| `ADD` | Phụ gia |
| `GCO` | Gia công |

Each option contains `value` (the category ID), `code` (the stable canonical code), and `displayName`. FE must use `value` when creating or updating a product, and may use `code` for labels or rules. The API does not migrate or delete legacy category records.

Create and patch requests validate `categoryId` against the same canonical codes and the current company. A legacy category remains visible on an existing record when the request does not change `categoryId`; it cannot be selected for a new product, a category change, or a product reassignment.

`POST /api/v1/plm/sample-requests/products/migrate-categories?dryRun=true` is restricted to `PLM.ProductTechnicalInfo.Edit` and considers active Products across every company. Each Product is assigned only to the canonical category (`CMP`, `CMB`, `AMB`, `ADD`, or `PIG`) belonging to that same Product's company; the API fails without writing if any company with active Products does not have exactly one active category for each of those codes. `dryRun` defaults to `true`: no database data is changed and the response returns totals plus the first 100 candidate Products (`previewItems`, with `matchedBy`). Optional `targetCategoryCode=CMP|CMB|AMB|ADD|PIG` limits preview and `dryRun=false` execution to one target category, so each group can be reviewed independently. Only an explicit `dryRun=false` applies the rule to the Products currently matching at execution time; its response contains the same bounded preview. Rules are evaluated in this order: legacy category `Compound` or a `ColourCode` ending in `C` moves to `CMP`; a Product name normalized by removing accents, normalizing Unicode and collapsing spaces, then containing `BOT MAU`, moves to `PIG`; a `ColourCode` ending in `D` also moves to `PIG` unless the normalized Product name contains `BOT PHU GIA`, `PHU GIA`, or `HAT NHUA PHU GIA`; legacy category `Hạt màu` moves to `CMB`, as does a normalized Product name containing `HAT MAU` or `HAT NHUA MAU` when its `ColourCode` ends in a non-letter, `U`, or `A`. `CMB` excludes names containing `HAT PHU GIA`, `COMPOUND`, `BOT MAU`, or `BOT PHU GIA`. A normalized Product name containing `HAT PHU GIA` or `HAT NHUA PHU GIA` moves to `AMB`. A normalized Product name containing `BOT PHU GIA` moves to `ADD`; a `ColourCode` ending in `D` also moves to `ADD` when its normalized Product name contains `PHU GIA`. Otherwise, a `ColourCode` ending in a letter other than `C` moves to `AMB` unless its normalized Product name contains `MAU` or `BOT PHU GIA`.

## Sample trial report

Dialog **Phản hồi khách hàng/Ghi chú phản hồi** dùng composer `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback`. Endpoint chỉ dành cho Sale, bắt buộc `idempotencyKey`, tự resolve customer từ Trial và trong một transaction vừa cập nhật phản hồi Trial vừa tạo CRM interaction/reference `SampleTrial/trialId` cùng follow-up task tùy chọn. Contract đầy đủ nằm trong `SampleRequestSampleTrials.README.md`.

Khi Lab gửi mẫu, message trong Notification Hub trả thêm `sampleReceiptAction` gắn chính xác với Trial vừa tạo. Sale/Leader xác nhận đã nhận mẫu bằng `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/confirm-receipt`. Nếu không gửi `sampleReceivedDate`, backend dùng thời điểm hiện tại; kết quả được lưu vào Trial và action chuyển từ `Pending` sang `Confirmed`.

Ngày Sale nhận mẫu được lưu vào `Trial.RequestReceivedDate` đã có sẵn. Action chuyển Trial từ `SampleSent` sang `WaitingCustomerFeedback` và dùng `UpdatedBy/UpdatedDate` để audit; không bổ sung cột database mới.

`GET /api/v1/plm/sample-requests/sample-trials` trả danh sách phân trang theo từng Sample Request. Mỗi Sample Request chỉ trả một dòng/card và gắn Trial active mới nhất theo `TrialNo`; hồ sơ chưa có Trial vẫn xuất hiện với `hasTrial = false`. `trialCount` và `hasPreviousTrials` cho FE biết có thể mở lịch sử hay không. `reportType=All` (và không truyền `reportType`) trả mọi Sample Request active trong company/customer visibility, không loại theo trạng thái workflow. `CompletedSamples` lấy Sample Request `Completed` có Trial mới nhất `Approved`, lọc/sắp theo `CustomerReplyDate`; `WaitingCustomerFeedback` là hàng đợi theo dõi gồm Trial `SampleSent` chưa có `RequestReceivedDate` (**Chờ Sale nhận mẫu**, luôn xếp trước, sắp theo `SentDate`) và Trial `WaitingCustomerFeedback` đã nhận mẫu (**Chờ phản hồi khách hàng**, sắp theo `RequestReceivedDate`). FE dùng `status` để hiển thị đúng nhãn từng nhóm. Lịch sử được tải lười bằng `GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials`, trả các Trial active giảm dần theo `TrialNo`. Dữ liệu luôn loại khách nội bộ `KH_VIETAUS`; `additiveRate` và `labNote` trả `null` nếu current user không có quyền xem thông tin kỹ thuật PLM.

Mỗi dòng luôn trả `requestDeliveryDate` (ngày Sale yêu cầu có mẫu) và `expectedDeliveryDate` (ngày dự kiến có mẫu) từ Sample Request, kể cả khi `hasTrial = false`. Hai field này khác `requestReceivedDate`, là ngày Lab/Sale ghi nhận nhận mẫu của một Trial và chỉ có khi Trial tồn tại. FE tạo mới qua `POST /api/v1/plm/sample-requests` hoặc chỉnh qua `PATCH /api/v1/plm/sample-requests/{sampleRequestId}` bằng cùng hai field camelCase; PATCH có thể xóa từng ngày qua `clearFields` với mã `sample_request.request_delivery_date` hoặc `sample_request.expected_delivery_date`.

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

Với conversation InternalMail liên kết Sample Request, endpoint attachment của conversation cũng trả các tệp gốc này trong cùng danh sách với `source = "SampleRequest"`. Đây là metadata/read view, không sao chép attachment và không bao gồm tệp Formula.

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

`formulaLookups[]` là danh sách công thức phát triển active cùng Product để FE dựng
dropdown công thức khách hàng chọn. Mỗi phần tử trả `formulaId`, `externalId`, `name`,
`status` dạng tên enum và `totalPrice`; `totalPrice` là `null` khi người dùng không có
quyền xem giá. FE dùng `status` để chỉ cho chọn các trạng thái hợp lệ và có thể hợp nhất
danh sách này với endpoint `/api/v1/plm/formulas/lookup` khi tìm kiếm/phân trang.

Create a sample request:

```http
POST /api/v1/plm/sample-requests
```

After a sample request is created, backend automatically creates an InternalMail message for the sample request thread and publishes a notification to Lab recipients, except for internal sample requests. FE may send `initialLabMessage` so Sale can review/edit the message content before submit. If `initialLabMessage` is blank, backend falls back to a generated summary from the key Sale-entered sample request and product fields. The message uses `SampleRequestNotificationType.GeneralMessage`, title `Yêu cầu phối mẫu mới`, the default sample request message topic, and does not expose extra sensitive payload beyond the normal message payload.

Product data in the create, detail and patch contracts includes `grs` (boolean; omitted on create defaults to `false`) and `grsConsumerType` (nullable enum: `0 = PostConsumer`, `1 = PreConsumer`). `grsConsumerType` may be cleared only through PATCH `clearFields: ["product.grs_consumer_type"]`.

Internal sample requests do not need an InternalMail thread or notification. Backend suppresses SampleRequest message sending when `SampleRequest.RequestType` is an internal value (`private`, `Nội bộ`, `Noi bo`, `Internal`, including common space/underscore/hyphen variants) or when the customer is the internal customer `KH_VIETAUS`. The suppress rule is enforced in `SendSampleRequestMessageCommandHandler`, so create, direct patch notification, data-change/formula-change message flows, sample-sent/completed/cancelled lifecycle messages and other SampleRequest message calls no-op before creating a conversation or publishing a notification. FE should hide the recipient/message confirmation step for these cases; if FE still submits message fields, BE returns success with empty ids and no side effect.

Patch a sample request:

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}
```

Trong response detail, `technicalRequirement.otherComment` (ô **Yêu cầu bổ sung**) lấy từ
`SampleRequest.OtherComment`; đây cũng là nơi PATCH field `otherComment` lưu dữ liệu. Không dùng
`Product.OtherComment` cho field này.

Hiện mode mặc định là `DirectNotify`: toàn bộ Product field có trong PATCH contract, gồm
`product.food_safety`, `product.rohs_standard` và `product.reach_standard`, được lưu trực tiếp rồi báo
Lab. `data-change-requests` vẫn được giữ như legacy approval flow; khi mode đổi sang
`RequireLabApproval`, PATCH trực tiếp (kể cả `clearFields`) của ba field này sẽ bị từ chối với người
không thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors`:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests
```

Lab duyệt hoặc từ chối một phần/toàn bộ đề xuất ngay trong Notification Hub:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests/{messageId}/decision
```

Request tạo đề xuất chỉ nhận mã field ổn định và giá trị mới. Backend tự đọc giá trị cũ từ
Sample Request để hiển thị/audit proposal. Tạm thời, lúc Lab duyệt backend không chặn proposal chỉ vì
giá trị hiện tại đã khác `OldValue`; giá trị đề xuất sẽ ghi đè giá trị hiện tại. Các kiểm tra company,
quyền duyệt, participant của conversation, field đang pending và validation payload vẫn giữ nguyên.
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
Lab không gửi lại giá trị mới; backend lấy đúng proposal trong message gốc rồi tái sử dụng
`PatchSampleRequestCommand` để áp dụng. Trong thời gian proposal đang chờ, quyết định duyệt sẽ áp dụng
giá trị đề xuất lên dữ liệu hiện tại; Reject chỉ cập nhật trạng thái proposal và gửi phản hồi, không patch
dữ liệu nghiệp vụ.

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

Response gồm ba phần phục vụ hai chế độ xem lịch sử: `latestChange` là lần thay đổi
mới nhất; `fields[]` là lịch sử đã tổng hợp theo `source + fieldName` (giá trị ban đầu,
giá trị hiện tại, số lần thay đổi và lần mới nhất); `timeline[]` là từng lần lưu có chi
tiết cũ/mới. Với field `FormulaId`, API query Formula trong cùng company và trả giá trị
hiển thị `ExternalId - Name` ở `oldValue`/`newValue`, không trả GUID cho FE.

`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` trực tiếp ghi audit cho các field thay đổi của `SampleRequests`
và `Products` với reason `SampleRequestDirectPatch`. Luồng duyệt đề xuất thay đổi dữ liệu vẫn dùng reason
`SampleRequestDataChangeApproval`. Handler chỉ ghi audit theo một nhánh (`IsDataChangeApproval` hoặc PATCH trực tiếp),
và helper audit tự bỏ qua khi không có field thật sự thay đổi, nên một lần lưu không tạo audit trùng cho cùng source.
Các field thuộc `SampleRequests` như `package`, `bagWeight`, `expectedQuantity`, `expectedPrice`, các mốc ngày,
`additionalComment` và `saleComment` được persist trực tiếp cùng AuditLog; không chờ Lab xác nhận.
Các thay đổi `Products.GRS` và `Products.GRSConsumerType` cũng được lưu old/new trong cùng audit timeline.

Sau khi FE PATCH trực tiếp các field whitelist `sample_request.*` hoặc `product.*`, FE có thể báo Lab qua:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/direct-patch-notifications
```

Endpoint này không cập nhật dữ liệu nghiệp vụ. Nó chỉ tạo InternalMail trong conversation Sample Request hiện có và publish notification sau khi PATCH thành công. Request phải có `idempotencyKey`, `message`, `recipientEmployeeIds` tùy chọn và `changes[]`. `changes[].fieldCode` chỉ nhận field trong direct-notify whitelist. Hiện mode mặc định là `DirectNotify`, nên whitelist gồm cả `product.food_safety`, `product.rohs_standard` và `product.reach_standard`.
Khi PATCH đổi loại sản phẩm, FE dùng field code `product.category_id` trong `changes[]`.

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
product.grs
product.grs_consumer_type
product.weight
product.unit
product.other_comment
```

The message content type is `SampleRequestDirectPatchNotification`. Notification topic is `SampleRequestDirectPatchNotified` (`plm.sample_request.direct_patch.notified`) with category `SampleRequest`. Payload includes `sampleRequestId`, `externalId`, `changedByEmployeeId`, `idempotencyKey`, `changes[]`, `conversationId`, and `messageId`. Retrying the same `idempotencyKey` in the same SampleRequest conversation returns the existing message and does not publish a duplicate notification.

The history endpoint treats the sample request and its product detail as one business record. It loads audit logs from `Audit.AuditLogs` where `SchemaName = SampleRequests` and:

```text
TableName = SampleRequests, RecordId = SampleRequest.SampleRequestId
TableName = Products,       RecordId = SampleRequest.ProductId
TableName = Attachments,    RecordId = SampleRequest.SampleRequestId
```

Rows with the same `CorrelationId` are grouped into one timeline item so a single save operation can show both `SampleRequests` fields and `Products` fields together. Each changed field keeps `details[].source` so the frontend can still show whether the field came from `SampleRequests`, `Products`, or `Attachments`. Uploading an attachment creates an `Attachments` audit row with `FileName: null -> file name`; deleting it records `FileName: file name -> null`. Audit snapshots retain `attachmentId` and slot for traceability but never include storage paths or file bytes. Detail, history, and messages are scoped with `ICustomerVisibilityService.ApplySampleRequestVisibility`; users cannot read a sample request by id unless the request belongs to a customer in their current visibility scope.

History is also filtered by PLM field visibility. Users outside `ApplicationRoleSets.PLM.ProductTechnicalEditors` do not receive the `Products` history details `ColourName` (Tên màu) and `Additive` (Phụ gia); all other changed fields remain visible. Audit rows remain stored fully in `Audit.AuditLogs`; only the API response is filtered, and a timeline card with no visible details is omitted.

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

`POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests` vẫn hỗ trợ stable field codes từ `SampleRequestDataChangeFieldCatalog` như legacy approval flow. Hiện `SampleRequestLabApprovalRules.CurrentMode` là `DirectNotify`: Sale PATCH trực tiếp tất cả Product field trong contract, rồi gọi `direct-patch-notifications` để báo Lab. Khi cần bật lại chờ Lab duyệt, đổi mode sang `RequireLabApproval`; endpoint, command, handler, action card và decision endpoint của data-change-requests vẫn được giữ nguyên để FE dùng lại.

Catalog cũng nhận `product.grs` và `product.grs_consumer_type`; enum chỉ nhận số `0` (`PostConsumer`) hoặc `1` (`PreConsumer`).

Người dùng có thể PATCH, tạo yêu cầu thay đổi, và gửi thông báo sau khi PATCH khi Sample Request nằm trong customer visibility hiện tại của họ. Quyền không phụ thuộc role, `CreatedBy`, hoặc `ManagerBy`; rule mode ở trên quyết định PATCH direct hay yêu cầu duyệt.

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

For `LabUser`, Sample Request visibility includes the internal customer `KH_VIETAUS` (`CustomerVisibilityConstants.RestrictedCustomerId`) so `/api/v1/plm/sample-requests/summary` and other Sample Request read APIs can show internal sample requests to Lab. On `/api/v1/plm/sample-requests/summary`, `ACUser` also receives the full current-company Sample Request scope, including `KH_VIETAUS`, matching Lab's list visibility. The accounting exception is limited to this summary endpoint; it does not grant broader CRM, detail, history, or update access. Sale order and production order dashboard sources keep their own internal-customer exclusion rules.

Với user bị giới hạn theo phạm vi Sales, `/summary` mặc định không trả Sample Request của khách nội bộ `KH_VIETAUS`. Khi request có `keyword`, riêng query này cho phép khách nội bộ tham gia tập tìm kiếm rồi áp dụng đầy đủ điều kiện keyword; vì vậy user có thể tìm bằng `KH_VIETAUS` hoặc thông tin khác của record nhưng không nhận toàn bộ hồ sơ nội bộ nếu chúng không khớp keyword. Company scope vẫn luôn được giữ nguyên. Quy tắc ngoại lệ chỉ áp dụng cho danh sách tìm kiếm `/summary`, không tự mở quyền đọc detail, history hoặc update Sample Request nội bộ.

## Message Recipients

All Sample Request thread message types resolve default recipients through `SampleRequestRecipientResolver`. Static role/group rules live in `SampleRequestRecipientRules`. Locked required recipients are active employees in roles `President` and `LabAdmin`, plus the active account with username `qaqcad01` in the current company. Khi sender có role `SaleUser`, mọi leader active (`MemberInGroup.IsAdmin = true`) trong chính group active của sender cũng là required recipient (`source = sales_group_leader`); rule group-scoped này không dùng role `Leader` toàn công ty, loại sender và tự bỏ trùng. Default leader recipients cho Lab là active leaders trong current company và được resolved theo stable product category `ExternalId`:

- `CMP` (Compound), `PMA` (Masterbatch), `PPG` (Phụ gia), `AMB`, `VRG`, and `ADD` -> `QAQC.RD`.
- `PHM` (Hạt màu), `PBM` (Bột màu), `PDM` (Dry mix), `CMB`, and `PIG` -> `QAQC.MAU`.
- `GCO` (Gia công) -> `QAQC.RD` and `QAQC.MAU`.
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

The response contains `requiredRecipients`, `suggestedRecipients`, and `selectedRecipients`. `President`/`LabAdmin`, account `qaqcad01`, and active Sales-group leaders of a `SaleUser` sender are locked required recipients. For an existing thread, active participants and the sample request manager are also locked, except the current sender. Category-matched QAQC leaders are returned as default, unlocked recipients and are selected by default on the first preview. FE may remove or add these optional recipients, then submit the selected optional ids back through:

```text
CreateSampleRequestCommand.InitialLabRecipientEmployeeIds
CreateSampleRequestDataChangeRequestCommand.RecipientEmployeeIds
CreateSampleRequestDirectPatchNotificationCommand.RecipientEmployeeIds
SendSampleRequestMessageCommand.ExtraRecipientEmployeeIds
```

BE still validates all submitted recipient employee ids against the current company and active employees, and always resolves locked `President`/`LabAdmin` plus the current Sale sender's same-group leaders when FE does not send them. Existing active conversation participants remain participants so the history is not hidden from them.

For the initial preview, FE omits `selectedRecipientEmployeeIds` so category-matched leaders are selected by default. After the user edits the recipient list, FE sends the current optional ids; sending an empty array explicitly removes every optional default recipient.

### Silent watchers when creating a Sample Request

Preview request accepts `selectedSilentWatcherEmployeeIds`; response returns the validated people in
`selectedSilentWatchers` and `canAddSilentWatchers`. FE submits that same selection in
`CreateSampleRequestCommand.initialLabSilentWatcherEmployeeIds` when creating the Sample Request.

Each silent watcher is added to the new Sample Request conversation with `Role = Watcher` and `IsMuted = true`.
They can read the thread and may unmute it later through their own conversation preference. While muted, the initial
message and later thread messages still appear in their Notification Hub as already-read items, but do not create an
unread badge, SignalR event or Web Push alert. A silent watcher must
be active in the current company, cannot be the current creator, and cannot also be in the normal recipient list.
The initial Sample Request message uses the selected watcher ids directly, so it also creates their Hub item before
the new conversation participants have been persisted. The same rule applies to initial normal recipients; subsequent
messages also resolve the persisted, unmuted conversation participants.
When `selectedSilentWatcherEmployeeIds` is omitted on the first preview, all active `LabUser` and `LabAdmin`
employees absent from `requiredRecipients`, `suggestedRecipients` and `selectedRecipients` are selected as default
silent watchers. Sending an explicit empty array lets FE remove those optional defaults.
The create command applies the same fallback when `initialLabSilentWatcherEmployeeIds` is omitted, so an older FE
that has not yet sent the field still adds the default Lab watchers. An explicit `[]` remains an intentional choice
to add none.

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

Message được ghi vào conversation active hiện có với `RelatedType = SampleRequest` và
`RelatedId = sampleRequestId`; thao tác không tạo Quotation và không đổi trạng thái Sample Request. Người nhận là
toàn bộ employee active có role `President` trong cùng company; sender được thêm vào thread nhưng bị loại khỏi
notification của chính mình. Rule private/internal của `SampleRequestMessageRules` vẫn được áp dụng.

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

## Color Chip Records

API lưu read-model phục vụ hiển thị/in phiếu Color Chip được tách tại
`Features/PLM/ColorChipRecords`. Contract GET/POST/PATCH, company scope, quyền chỉnh sửa, attachment collection và
semantics `clearFields` được mô tả trong [Color Chip Records README](../ColorChipRecords/README.md).
