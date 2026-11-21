# API và tổ chức code

## Tổ chức code

Cấp gốc giữ Commands, Queries, Dtos và Services. Commands/Queries nhóm use case theo Requests, SampleTrials, ChangeRequests, Messaging, Reporting, Pricing, Products hoặc Formulas tùy trách nhiệm; mỗi command/query và handler vẫn ở file riêng.

Services nhóm code dùng chung theo nghiệp vụ: Requests, SampleTrials (gồm Receipts), ChangeRequests (Data/Formula), Messaging, Pricing, Formulas và Attachments. Các nhóm có thể chứa service, rule thuần, payload hoặc contract; việc đặt trong Services không biến rule thuần thành DI service. Loader/model chỉ dùng cho một query vẫn đặt cạnh query đó. Dtos tiếp tục chia theo loại response.

Namespace hiện có được giữ để không ảnh hưởng caller và DI ở module khác. Thay đổi này chỉ tổ chức file, giữ nguyên logic, quyền, transaction và public API.

## Product category options

## Lookup cho Sale Order

Quyền đọc SampleRequest theo quyền xem khách hàng, không chặn thêm theo ManagerBy.
Sale có quyền xem khách thì thấy TP của khách đó, kể cả Sale phụ trách TP ngoài nhóm.
Company, active, quyền KH_VIETAUS và điều kiện status/formula lên đơn vẫn giữ nguyên.
Riêng `forSaleOrder=true`, `orderType=0` và customer của đơn là KH_VIETAUS active,
lookup/detail vẫn cho chọn mẫu đủ điều kiện của mọi khách cùng company như trước.
Không tự cấp quyền sửa/làm thay; các gate capability/ownership của mutation vẫn giữ.

`GET /api/v1/plm/sample-requests/lookup` chỉ đưa Sample Request của customer nội bộ `KH_VIETAUS` vào kết quả khi request có `keyword`; các item này luôn nằm sau item của customer khác. Khi có `customerId`, lookup tìm trong tập customer đang chọn và `KH_VIETAUS`. Keyword được trim và tìm không phân biệt hoa thường bằng PostgreSQL `ILIKE` trên mã yêu cầu mẫu, mã/tên khách hàng, mã/tên sản phẩm và mã Formula. Lookup thông thường không tự giới hạn status nên trả mọi trạng thái khớp filter `status` nếu FE có truyền. Khi dropdown được gọi từ màn tạo Sale Order, FE truyền `forSaleOrder=true&orderType={0..3}`; backend luôn khóa dữ liệu về `CompanyId` của user hiện tại và chỉ trả Sample Request có `status` là `SampleSent` hoặc `Completed`. `customerId` là bắt buộc trong mode này: chưa chọn customer thì trả mảng rỗng. Chỉ khi `orderType=0` (Nội bộ) và customer đang chọn có `ExternalId = KH_VIETAUS`, backend mới trả Sample Request hợp lệ của **mọi customer trong cùng company**. Rule được BE tự nhận diện từ customer đã lưu, không tin một cờ FE gửi lên. Mỗi item có thêm `formulaId`/`formulaExternalId`: ưu tiên Formula đang chọn trên Sample Request, nếu chưa có thì lấy Formula active của Trial active mới nhất; FE dùng hai field này để điền Formula cho dòng Sale Order và disable item nếu `formulaId = null`. Các filter `isActive`, keyword và `status` vẫn cùng áp dụng; keyword vẫn phải khớp record như lookup thông thường. Nếu `status` là trạng thái khác hai giá trị trên thì kết quả là danh sách rỗng.

Lookup và `GET /api/v1/plm/sample-requests/{sampleRequestId}` dùng customer visibility của current user. Lookup luôn loại Sample Request inactive, Product inactive/khác company, Customer inactive/khác company và record ngoài customer scope. `KH_VIETAUS` bị loại khi chưa nhập keyword; ngay khi `keyword` có giá trị, lookup mở `KH_VIETAUS` để người dùng chủ động tìm mã/tên sản phẩm nội bộ. Trong mode Sale Order, record còn phải thỏa rule customer/status tạo đơn. `sampleRequestId` là định danh canonical duy nhất của item; endpoint không trả alias `id`.

Sau khi chọn item từ lookup, FE phải gọi detail kèm context Sale Order:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}?forSaleOrder=true&customerId={customerId}&orderType={0..3}
```

BE không tin `customerId` từ FE như một quyền độc lập: customer phải active trong company hiện tại. Sample Request target cũng phải active, Product/Customer liên kết active cùng company, có status `SampleSent` hoặc `Completed`, và phải thuộc customer đang lập đơn hoặc `KH_VIETAUS`. Riêng khi `orderType=0` và customer của đơn là `KH_VIETAUS`, Sample Request hợp lệ của mọi customer trong company được phép dùng cho đơn nội bộ. Không truyền `forSaleOrder=true` giữ nguyên quyền detail thông thường.

`GET /api/v1/plm/sample-requests/form-options` only returns the active canonical product categories of the current company. Legacy categories remain in the database for historical records and are intentionally omitted from this form lookup. The `branches` collection is a fixed, ordered business reference list; each item returns its stable GUID `value`, stable uppercase `code`, and `displayName` instead of loading every company from the database. FE should use `code` for display rules and must not derive business logic from `displayName`.

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

Trial tạo mới chỉ thuộc hồ sơ còn mở; không khởi tạo sẵn kết quả Approved/Failed/Cancelled/ReworkRequested.
Kết quả phản hồi phải ghi qua composer customer-feedback để cập nhật đồng bộ Trial, hồ sơ, CRM và sự kiện.
PATCH Trial vẫn cho sửa metadata theo quyền; không đổi delivery/phản hồi của Trial đã kết thúc hoặc hồ sơ đóng.
Payload đầy đủ gửi lại giá trị workflow không đổi vẫn được chấp nhận. CanCreateTrial=false trên hồ sơ Completed/Cancelled.

Xác nhận giá lưu liên kết Formula theo nguồn đủ điều kiện của hồ sơ, không đợi SampleRequest.FormulaId được chọn khi
khách duyệt mẫu. Tham chiếu ngoài hệ thống giữ ProductPricingVersion Approved hiện hành; dòng báo giá đã gửi giữ
version giá/nguồn công thức lịch sử của chính dòng đó. Thu hồi giữ liên kết từ sự kiện xác nhận trước.
Nguồn tính readiness giá ưu tiên công thức vừa được Lab xác nhận của hồ sơ, không bị Formula của Trial cũ
đè lên. Formula trên dòng Trial vẫn hiển thị đúng phương án đã gửi; khóa nguồn giá nội bộ không thêm public field.

Dialog **Phản hồi khách hàng/Ghi chú phản hồi** dùng composer `POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback`. Endpoint chỉ dành cho Sale, bắt buộc `idempotencyKey`, tự resolve customer từ Trial và trong một transaction vừa cập nhật phản hồi Trial vừa tạo CRM interaction/reference `SampleTrial/trialId` cùng follow-up task tùy chọn. Contract đầy đủ nằm trong `Services/SampleTrials/README.md`.

Khi Lab gửi mẫu mới, Trial chuyển thẳng sang `WaitingCustomerFeedback`; message không tạo `sampleReceiptAction`. Sale ghi phản hồi trực tiếp vào Trial, không xác nhận nhận mẫu từ Lab.

`Trial.RequestReceivedDate` được giữ để đọc lịch sử Sale nhận từ Lab. Ngày Lab nhận mẫu chuẩn lấy từ `SampleRequest.LabReceivedDate`, không đổi nghĩa dữ liệu cũ.

`GET /api/v1/plm/sample-requests/sample-trials` trả danh sách phân trang theo từng Sample Request. Ngoại trừ `WaitingCustomerFeedback` trả từng Trial còn chờ phản hồi (kể cả Trial cũ), mỗi Sample Request chỉ trả một dòng/card và gắn Trial active mới nhất theo `TrialNo`; hồ sơ chưa có Trial vẫn xuất hiện với `hasTrial = false`. `trialCount` và `hasPreviousTrials` cho FE biết có thể mở lịch sử hay không. `reportType=All` (và không truyền `reportType`) trả mọi Sample Request active trong company/customer visibility, không loại theo trạng thái workflow. `CompletedSamples` lấy Sample Request `Completed` có Trial mới nhất `Approved`, lọc/sắp theo `CustomerReplyDate`; `WaitingCustomerFeedback` là hàng đợi theo dõi gồm Trial `SampleSent`, `WaitingCustomerFeedback` hoặc `PriceQuote`, đều lọc/sắp theo `SentDate`, không phụ thuộc ngày Sale nhận. Lịch sử được tải lười bằng `GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials`, trả các Trial active giảm dần theo `TrialNo`. Dữ liệu luôn loại khách nội bộ `KH_VIETAUS`; `additiveRate` và `labNote` trả `null` nếu current user không có quyền xem thông tin kỹ thuật PLM.

Các field giá trên card Trial dùng pricing capability chung: Sale chỉ nhận giá chuẩn đã duyệt (cùng ghi chú/thời điểm duyệt), còn `systemCalculatedStandardSellingPrice` luôn `null`; backend cũng không tải nguồn giá realtime cho Sale.

Mỗi dòng luôn trả `requestDeliveryDate` (ngày Sale yêu cầu có mẫu), `labReceivedDate` (ngày Lab thực tế nhận mẫu đầu vào từ Sale), `labReceivedByEmployeeId`/`labReceivedByName` (người Lab nhận mẫu), `isDelayed` (cờ delay hiện tại) và `expectedDeliveryDate` (ngày dự kiến có mẫu) từ Sample Request, kể cả khi `hasTrial = false`. Các field Lab nhận mẫu thuộc toàn bộ Sample Request và không phải `requestReceivedDate` của Trial, vốn là ngày Sale xác nhận nhận mẫu đã hoàn thành từ Lab. FE tạo hoặc chỉnh hai ngày yêu cầu/dự kiến qua contract hiện hành; PATCH có thể xóa chúng qua `clearFields` với mã `sample_request.request_delivery_date` hoặc `sample_request.expected_delivery_date`.

`dueDate` là hạn dùng chung cho thẻ công việc và nhóm Quá hạn, không thay đổi các ngày nguồn. Nhánh Báo giá không yêu cầu kg dùng `expectedPriceQuoteDate` (`dueDateSource=PriceQuoteExpected`), thiếu mốc này thì hạn là null. Nhánh còn lại ưu tiên `expectedDeliveryDate` do Lab lập lịch (`LabExpected`), sau đó `requestDeliveryDate` của Sale (`SaleRequested`), cuối cùng mới suy từ ngày Lab nhận mẫu chuẩn cộng số ngày giao mẫu (`LabReceiptPlusDays`). Mốc suy chỉ hợp lệ khi số ngày không âm và phép cộng không vượt giới hạn ngày; nếu không có nguồn hợp lệ thì trả null/`Unknown`. Vì vậy chỉnh lịch dự kiến cập nhật ngay hạn trên thẻ và điều kiện Quá hạn, còn ngày Sale yêu cầu giữ nguyên. Chỉ việc mở còn thuộc Lab được tính quá hạn.

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

PATCH `usageRate`, `expiryType` và clear `product.usage_rate`/`product.expiry_type` chỉ cho Developer, LabUser, President (tập role `ProductUsageAndExpiryEditors`). Áp dụng cả thao tác từ màn hình MFG. Quyền endpoint và company/customer visibility vẫn bắt buộc; Admin/PLPU/Sale không được sửa hai field này nếu không có thêm một trong ba role trên. Các field kỹ thuật khác giữ rule hiện tại.

Sample request detail is loaded by id:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}
```

Khi không tải được detail, response error có `code` ổn định: `sample_request_not_found` (404, không tồn tại/đã inactive/khác company), `sample_request_forbidden` (403, cùng công ty nhưng user không có visibility), hoặc `sample_request_invalid_relationship` (422, Sample Request active nhưng Product hoặc Customer liên kết không còn active/cùng company). Riêng role `SaleUser` được đọc detail của Sample Request thuộc khách nội bộ `KH_VIETAUS` trong công ty hiện tại; ngoại lệ read-only này không tự cấp quyền history, message, PATCH hoặc mutation khác. FE không retry bằng `productId`; chỉ dùng `sampleRequestId` từ lookup.

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

The initial message is persisted synchronously in the transaction that creates the request, using the
creation event ID as its stable message ID. Successful creation therefore does not depend on a background
worker creating the summary later. `SampleRequestWorkflowNotification` is still queued atomically,
but for this initial message its worker reuses the stored message and only publishes the notification.
Notification/SignalR/Web Push failures are retried independently and cannot roll back the saved request
or summary. A notification retry does not recreate message read states or change its sent time/latest-message
position. Validation and database persistence failures still fail creation; internal-request suppression remains
unchanged. This change does not automatically repair previously swallowed workflow jobs or missing old summaries.

Admin job retry supports the exact legacy error `No handler for type SampleRequestWorkflowNotification`
mistakenly marked processed, only with no event message/notification IDs and no existing event-ID message.
It locks and requeues the original company-scoped row. Repeated retry returns AlreadyQueued/Completed;
it never clones the event/job. Normal successful processed jobs remain non-retryable. Existing retry permissions apply.

Product data in the create, detail and patch contracts includes `grs` (boolean; omitted on create defaults to `false`) and `grsConsumerType` (nullable enum: `0 = PostConsumer`, `1 = PreConsumer`). `grsConsumerType` may be cleared only through PATCH `clearFields: ["product.grs_consumer_type"]`.

Internal sample requests do not need an InternalMail thread or notification. Backend suppresses SampleRequest message sending when `SampleRequest.RequestType` is an internal value (`private`, `Nội bộ`, `Noi bo`, `Internal`, including common space/underscore/hyphen variants) or when the customer is the internal customer `KH_VIETAUS`. The suppress rule is enforced in `SendSampleRequestMessageCommandHandler`, so create, direct patch notification, data-change/formula-change message flows, sample-sent/completed/cancelled lifecycle messages and other SampleRequest message calls no-op before creating a conversation or publishing a notification. FE should hide the recipient/message confirmation step for these cases; if FE still submits message fields, BE returns success with empty ids and no side effect.

Patch a sample request:

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}
```

### Đổi mã màu trước khi gửi mẫu

Lab đổi riêng mã màu và phát thông báo bằng một request:

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}/colour-code
```

```json
{
  "colourCode": "BH_D",
  "idempotencyKey": "00000000-0000-0000-0000-000000000001",
  "expectedUpdatedDate": "2026-09-09T10:30:00",
  "isUrgent": false,
  "recipientEmployeeIds": []
}
```

Endpoint chỉ dành cho `ApplicationRoleSets.PLM.ProductTechnicalEditors`. Sample Request phải còn ở `New`,
`Pending` hoặc `InProgress`, và Product không được có Trial active đã rời `Draft` hoặc có `SentDate`. Khi prefix
được gửi ở dạng template có dấu `_`, backend sinh mã theo đúng quy tắc tạo Product lần đầu: lấy số lớn nhất
đang có của prefix rồi cộng một, sau đó ghép suffix/additive. Ví dụ đang có `BH001C`, `BH002C` thì `BH_D`
sinh `BH003D`, không giữ lại số của mã hiện tại. Đuôi phải khớp additive code/group trong reference data.
Mã kết quả phải duy nhất trong các Product active. Cùng một thao tác retry phải giữ nguyên `idempotencyKey` để
backend trả kết quả đã xử lý thay vì sinh thêm mã kế tiếp.

Một lần lưu cập nhật `Product.ColourCode`, `Additive` nếu template resolve được additive, snapshot của mọi Trial
`Draft` cùng Product, subject của mọi conversation Sample Request active cùng Product và snapshot/subject của báo
giá `Draft`. Backend cũng thay mã cũ bằng mã mới trong body/payload của message và title/message/link/payload của
notification thuộc đúng các Sample Request liên quan trong cùng company. Các message/notification thông báo đổi
mã trước đó được giữ nguyên để không làm sai dấu vết audit. Backend ghi Product audit với reason
`SampleRequestColourCodeChanged`, sau đó thêm message mới
`Mã màu đã được cập nhật từ {old} thành {new}.` và publish topic hiện có
`SampleRequestDirectPatchNotified` (`plm.sample_request.direct_patch.notified`). Notification title là
`Mã màu đã đổi thành {new}`. Trial đã gửi và báo giá không còn Draft không bị viết lại.

Response thành công trả mã cũ/mới, số Trial Draft và conversation đã đồng bộ, cùng các id của message/notification:

```json
{
  "success": true,
  "data": {
    "sampleRequestId": "00000000-0000-0000-0000-000000000010",
    "productId": "00000000-0000-0000-0000-000000000020",
    "oldColourCode": "BH001C",
    "newColourCode": "BH003D",
    "updatedDraftTrialCount": 1,
    "updatedConversationCount": 1,
    "updatedMessageCount": 2,
    "updatedNotificationCount": 2,
    "conversationId": "00000000-0000-0000-0000-000000000030",
    "messageId": "00000000-0000-0000-0000-000000000040",
    "notificationId": "00000000-0000-0000-0000-000000000050"
  }
}
```

Các id message/notification trả `Guid.Empty` khi Sample Request nội bộ thuộc rule không phát message, hoặc khi dữ
liệu đã lưu thành công nhưng bước gửi notification thất bại; trường hợp sau được mô tả trong `message` của response.

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

Summary không còn trả `productionOrders`. Thay vào đó, mỗi item trả ba danh sách công thức cùng contract
`FormulaId` như `GET /api/v1/plm/formulas`:

```text
formulaSelects   = Manufacturing Formula từng được chọn cho Production Order của Product
formulaDevs      = Formula phát triển active của Product
formulaStandard  = Manufacturing Formula từng được đặt làm chuẩn cho Product
```

Ba danh sách được batch-load cho các Product trên trang hiện tại và luôn khóa theo cặp `ProductId + CompanyId`.
`formulaSelects` loại trùng theo `ManufacturingFormulaId`, giữ lần dùng gần nhất và sắp theo `lastDateUse` giảm dần.
`formulaDevs` sắp theo ngày cập nhật/tạo giảm dần. `formulaStandard` loại trùng theo `ManufacturingFormulaId`, ưu tiên
bản ghi chuẩn hiện hành (`ValidTo IS NULL`), sau đó sắp công thức hiện hành trước và `lastDateUse` giảm dần.

Nếu không có dữ liệu, collection tương ứng trả `[]`, không trả `null`. Mỗi item gồm `id`, `externalId`, `name`,
`status`, `note`, `createdByName`, `price`, `itemCount`, `lastDateUse`; các field không áp dụng với nguồn dữ liệu
tương ứng giữ giá trị mặc định giống contract `GetFormulas`.

### Header yêu cầu phối mẫu và công thức gần nhất

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}/formula-options
```

Endpoint trả header rút gọn của yêu cầu phối mẫu cùng ba collection `formulaSelects`, `formulaDevs` và
`formulaStandard`. Mỗi collection dùng đúng contract và thứ tự của `GET /api/v1/plm/formulas`, nhưng chỉ trả tối đa
5 công thức sau khi loại trùng. Collection không có dữ liệu trả `[]`.

```json
{
  "header": {
    "sampleRequestId": "00000000-0000-0000-0000-000000000000",
    "sampleRequestExternalId": "SR-001",
    "productId": "00000000-0000-0000-0000-000000000000",
    "colourCode": "VA-001",
    "saleNote": "Ghi chú Sale",
    "labNote": "Ghi chú Lab",
    "specialRequirement": "Yêu cầu đặc biệt",
    "requirement": "Yêu cầu sản xuất/QC"
  },
  "formulaSelects": [],
  "formulaDevs": [],
  "formulaStandard": []
}
```

`colourCode` và `requirement` lấy từ Product hiện tại; `saleNote` và `specialRequirement` lấy từ Sample Request;
`labNote` lấy từ Product và trả `null` khi current user không có quyền xem thông tin kỹ thuật PLM. Các chuỗi nguồn
không có dữ liệu giữ `null`. Giá trong từng công thức tiếp tục trả `null` nếu current user không có quyền xem giá
công thức. Endpoint bắt buộc đăng nhập, khóa theo company hiện tại và áp dụng cùng customer visibility/ownership
scope với API detail Sample Request, gồm quyền Sale đọc yêu cầu khách nội bộ trong cùng công ty; record không thuộc
scope không được trả dữ liệu.

Khi `PATCH /api/v1/plm/formulas/{formulaId}/status` chuyển Formula sang `SampleSent`, backend đồng thời chuyển các
Sample Request active đang trỏ tới `FormulaId` đó sang `SampleRequestStatus.SampleSent`.

## Query Organization

`GetSampleRequestSummaryQueryHandler` should stay focused on query orchestration.

Keep query-only projection models in:

```text
Queries/Requests/GetSampleRequestSummary/Models/GetSampleRequestSummaryModels.cs
```

Keep query-specific data loading in:

```text
Queries/Requests/GetSampleRequestSummary/Services/SampleRequestSummaryRelatedDataLoader.cs
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

For `LabUser`, Sample Request visibility includes the internal customer `KH_VIETAUS` (`CustomerVisibilityConstants.RestrictedCustomerId`) so `/api/v1/plm/sample-requests/summary` and other Sample Request read APIs can show internal sample requests to Lab. Mọi `SaleUser` trong cùng công ty được mở detail và dùng PATCH chung `PATCH /api/v1/plm/sample-requests/{sampleRequestId}` cho Sample Request của `KH_VIETAUS`, không phụ thuộc ownership/group của khách nội bộ. Ngoại lệ mutation này chỉ bật internal-customer visibility ngay trong `PatchSampleRequestCommandHandler`; nó không mở shared customer scope, không mở khách thường ngoài ownership, và vẫn giữ toàn bộ validation/approval/lifecycle/concurrency hiện có của PATCH. `ACUser` không nhận ngoại lệ này nếu tài khoản không đồng thời có `SaleUser`. Sale order và production order dashboard sources giữ rule loại khách nội bộ riêng của chúng.

Với user bị giới hạn theo phạm vi Sales, `/summary` mặc định không trả Sample Request của khách nội bộ `KH_VIETAUS`. Khi request có `keyword`, riêng query này cho phép khách nội bộ tham gia tập tìm kiếm rồi áp dụng đầy đủ điều kiện keyword; vì vậy user có thể tìm bằng `KH_VIETAUS` hoặc thông tin khác của record nhưng không nhận toàn bộ hồ sơ nội bộ nếu chúng không khớp keyword. Company scope vẫn luôn được giữ nguyên. Role `SaleUser` có thể mở detail và PATCH kết quả nội bộ tìm được; ngoại lệ này không tự mở history hoặc message.

