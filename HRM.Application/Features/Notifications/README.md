# Notification

## 1. Mục đích

Module Notification cung cấp hộp thư thông báo theo từng nhân viên và hai kênh chuyển phát:

- SignalR báo realtime khi ứng dụng đang mở.
- Web Push báo qua trình duyệt/hệ điều hành khi ứng dụng chạy nền hoặc đã đóng.

Notification trong database luôn là nguồn dữ liệu chính. SignalR và Web Push chỉ báo rằng
có dữ liệu mới; FE vẫn phải gọi API có xác thực để lấy feed hoặc chi tiết.

```text
Business handler
-> INotificationService.PublishAsync
-> Notification + NotificationRecipient + NotificationUserState
-> OutboxMessage(InAppPush) + OutboxMessage(WebPush)
-> OutboxProcessor + WebPushOutboxProcessor
-> SignalR/Web Push
-> FE gọi API Notification để lấy dữ liệu thật
```

## 2. Vai trò của các entity

### Notification

Lưu nội dung gốc của một thông báo:

- `Topic`: loại nghiệp vụ tạo thông báo.
- `TopicCode`: mã phân cấp ổn định cho frontend, được suy ra từ `Topic` khi trả API và không lưu database.
- `Severity`: mức độ như `Info`, `Warning`, `Error`.
- `Title`, `Message`: nội dung FE hiển thị.
- `Link`: đường dẫn nội bộ FE mở khi người dùng bấm thông báo.
- `PayloadJson`: metadata linh hoạt; không dùng làm nguồn dữ liệu nghiệp vụ chính.
- `CompanyId`: company sở hữu dữ liệu.
- `CreatedBy`: người tạo thông báo.
- `CreatedByNameSnapshot`: tên người tạo tại thời điểm gửi để hiển thị nhanh và giữ lịch sử.

Một hành động nghiệp vụ thường tạo một dòng `Notification`.

### NotificationRecipient

Lưu ý định gửi ban đầu để audit:

- `TargetUserId`: gửi đích danh cho một employee.
- `TargetRole`: gửi cho các employee có role tương ứng.
- `TargetTeamId`: gửi theo team/part.

Chỉ một trong ba target nên có giá trị trên mỗi dòng. Recipient không trực tiếp quyết định
notification có xuất hiện trong feed của user hay không.

### NotificationUserState

Là hộp thư và trạng thái riêng của từng employee:

- `NotificationId`: notification được nhận.
- `UserId`: `EmployeeId` của người nhận.
- `IsRead`, `ReadDate`: trạng thái đọc.
- `IsArchived`: ẩn khỏi inbox của riêng employee đó.

User chỉ xem được notification qua API khi có `NotificationUserState` tương ứng và state chưa
archive. Vì vậy, biết `notificationId` không đồng nghĩa với việc được phép đọc notification.

Một InternalMail participant có thể là `Watcher` với `IsMuted = true`: họ vẫn được phép đọc thread theo quyền
participant. Khi publish message mới, handler đưa họ vào `SilentUserIds`: backend vẫn tạo `NotificationUserState`
đã đọc để notification hiện trong Notification Hub, nhưng không đưa họ vào SignalR/Web Push và không làm tăng unread
badge. Đây phù hợp với người chỉ cần theo dõi thread; họ có thể tự unmute để nhận cảnh báo mới về sau.

### OutboxMessage

Là hàng đợi chuyển phát side effect:

- `Type = InAppPush`: dành cho SignalR.
- `Type = WebPush`: dành cho Web Push.
- `PayloadJson`: payload tối thiểu cần cho worker.
- `ProcessedAt = null`: chưa xử lý.
- `Attempts`, `Error`: theo dõi xử lý lỗi.

Outbox giúp việc lưu Notification không phụ thuộc trực tiếp vào kết nối SignalR hoặc push
service. Notification đã lưu vẫn tồn tại nếu quá trình chuyển phát tạm thời lỗi.

### WebPushSubscription

Lưu đăng ký Web Push theo browser/thiết bị và employee:

- `CompanyId`, `EmployeeId`: chủ sở hữu subscription.
- `Endpoint`, `P256dh`, `Auth`: dữ liệu browser cấp để gửi push.
- `DeviceName`, `UserAgent`: mô tả thiết bị.
- `IsActive`: subscription còn được sử dụng hay không.
- `LastSuccessAt`, `LastFailureAt`, `FailureCount`: trạng thái chuyển phát gần nhất.

Endpoint và các khóa subscription là dữ liệu nhạy cảm, không được ghi log hoặc trả lại cho FE.

`NotificationTemplate` và `UserNotificationSetting` hiện chưa tham gia đầy đủ vào flow v1.
Không tự động áp dụng template, quiet hours hoặc channel preference nếu chưa có nghiệp vụ rõ ràng.

## 3. Publish notification từ nghiệp vụ

Business handler gọi duy nhất `INotificationService.PublishAsync`. Không gọi SignalR hoặc
Web Push trực tiếp từ handler.

Các nghiệp vụ chạy nền cũng đi qua cùng service. Cụ thể, CRM dùng
`CustomerFollowUpTaskDueReminderWorker` để quét WorkTask đến hạn mỗi phút rồi publish topic
`CustomerFollowUpTaskDue`; worker không tự ghi trực tiếp Notification/UserState/Outbox.

```csharp
await notificationService.PublishAsync(new PublishNotificationRequest
{
    Topic = TopicNotifications.SampleRequestUpdated,
    Severity = NotificationSeverity.Info,
    Title = "Yêu cầu phối mẫu đã cập nhật",
    Message = "Thông tin yêu cầu phối mẫu đã thay đổi.",
    Link = $"/plm/sample-requests/{sampleRequestId}",
    TargetUserIds = employeeIds,
    TargetRoles = ["LABADMIN"]
}, cancellationToken);
```

`PublishAsync` thực hiện theo thứ tự:

1. Lấy `CompanyId` và người tạo từ request hoặc current user.
2. Tạo `Notification`.
3. Chuẩn hóa target user/role/team và loại giá trị trùng.
4. Tạo `NotificationRecipient` để lưu ý định gửi.
5. Resolve normal target và silent target thành các `EmployeeId` active trong cùng company.
6. Tạo `NotificationUserState` unread cho normal recipient; silent recipient có state đã đọc để vẫn thấy trong Hub.
7. Tạo outbox `InAppPush` cho SignalR chỉ với normal recipient.
8. Nếu Web Push được bật và có normal recipient, tạo thêm outbox `WebPush` chỉ cho các employee đó.
9. Lưu toàn bộ thay đổi bằng `SaveChangesAsync`.

`TargetTeamIds` hiện được lưu recipient nhưng chưa được resolve thành employee trong
`NotificationService`. Không chỉ truyền team nếu chưa bổ sung nguồn membership chắc chắn;
nên đồng thời resolve thành `TargetUserIds` hoặc dùng role phù hợp.

## 4. SignalR

Hub được map tại:

```text
/hubs/notifications
```

Khi kết nối, `NotificationHub` đọc claim và tự join các group:

```text
company:{companyId dạng N}
user:{employeeId}
role:{companyId dạng N}:{ROLE viết hoa}
```

`OutboxProcessor` chỉ đọc outbox `InAppPush`, gửi event `notify` với payload nhỏ:

```json
{
  "notificationId": "guid"
}
```

FE nhận event thì reload feed/unread/detail khi cần. Không dùng payload SignalR làm dữ liệu
hiển thị chính và không tin vào `notificationId` nếu API detail không cho phép đọc.

## 5. Web Push

API quản lý subscription:

```http
GET  /api/v1/web-push/public-key
POST /api/v1/web-push/subscriptions
POST /api/v1/web-push/subscriptions/unsubscribe
```

`WebPushOutboxProcessor` thực hiện:

1. Đọc outbox `WebPush` chưa xử lý.
2. Lấy Notification theo `notificationId`.
3. Tìm các `NotificationUserState` chưa archive thuộc danh sách employee trong Web Push outbox.
4. Tìm subscription active của đúng employee và company. Payload outbox cũ chưa có danh sách này vẫn tương thích
   và dùng toàn bộ state chưa archive như trước.
5. Gửi payload tối thiểu qua `IWebPushSender`.
6. Cập nhật success/failure cho từng subscription.
7. Nếu push service trả `404` hoặc `410`, đặt subscription hết hạn thành inactive.
8. Đánh dấu outbox đã xử lý sau khi đã thử gửi cho batch thiết bị.

Payload Web Push không chứa đầy đủ nội dung nghiệp vụ:

```json
{
  "notificationId": "guid",
  "title": "Bạn có thông báo mới",
  "url": "/notifications"
}
```

Chi tiết cấu hình VAPID, service worker và logout được mô tả trong [WEB_PUSH.md](WEB_PUSH.md).

## 6. API hộp thư

`remove recipient` không xóa cứng dữ liệu. API chỉ set `NotificationUserState.IsArchived = true`
cho employee bị thu hồi để notification biến mất khỏi feed/unread-summary của riêng người đó,
trong khi `Notification`, `NotificationRecipient` và audit vẫn được giữ lại.

Quyền gọi `remove recipient`:

- Người tạo notification (`Notification.CreatedBy`) được thu hồi người nhận của notification mình tạo.
- Các role trong `ApplicationRoleSets.Notifications.RecipientManagers` được thu hồi recipient cho notification cùng company.
- Role set hiện tại gồm `Developer` và `President`; nếu muốn thêm/bớt role thì sửa tại
  `HRM.Application/Commons/Authorization/ApplicationRoleSets.cs`, không hard-code trực tiếp trong service.

```http
GET  /api/v1/notifications/feed
GET  /api/v1/notifications/unread-count
GET  /api/v1/notifications/unread-summary
GET  /api/v1/notifications/{id}
POST /api/v1/notifications/{id}/read
POST /api/v1/notifications/read-all
POST /api/v1/notifications/{id}/archive
POST /api/v1/notifications/{id}/recipients/{employeeId}/remove
```

`feed` hỗ trợ:

- `categoryCode`: nhóm nghiệp vụ cấp cao như `sales_order`, `production`, `quotation`.
- `eventGroupCode`: nhóm sự kiện con như `delivery`, `complaint`, `change`, `schedule`.
- `take`: số lượng item, được giới hạn trong service.
- `afterId` và `afterCreated`: keyset paging để lấy trang cũ hơn.

Notification có `CreatedDate` trước `20/07/2026 00:00:00` luôn được trả về với
`categoryCode = legacy_data` (FE hiển thị là "Dữ liệu cũ"). Khi lọc category, dữ liệu trước mốc này chỉ xuất
hiện trong `legacy_data`; các category nghiệp vụ khác chỉ chứa dữ liệu từ mốc này trở đi. Quy tắc chỉ áp
dụng trên response và truy vấn API, không thay đổi `Topic`, `TopicCode` hoặc dữ liệu đã lưu trong database.

Response feed/detail trả `topic`, `topicCode`, `categoryCode`, `eventGroupCode`, `context`, `conversationId`,
`conversationTitle` và `messageId`. Khi notification gắn một conversation mà current employee vẫn là participant
active cùng company, `conversationTitle` là subject hiện tại của thread; FE dùng đây làm title canonical khi render
feed đã lọc theo category/event group. `context.aggregateCode` vẫn là mã nghiệp vụ ngắn, không thay thế title.
`TopicNotifications` được lưu dạng
`int` trong database, các giá trị hiện có được khóa bằng số explicit và chỉ được append giá trị mới ở cuối.
Frontend dùng `categoryCode` cho menu nghiệp vụ, `eventGroupCode` cho filter con và `topicCode` cho icon, màu,
điều hướng, action cụ thể. `NotificationTopicCatalog` là nguồn duy nhất chứa mapping các code này và
`aggregateType`; không hard-code mapping trong handler hoặc FE.
`NotificationService.PublishAsync` từ chối topic chưa có definition; fallback `notification.unknown.<number>` chỉ
dùng để API vẫn đọc an toàn dữ liệu lịch sử lạ, không được dùng cho notification mới.

`NotificationCategory` enum cũ không còn thuộc public contract. `Delivery` và complaint không phải category cấp cao:
topic giao hàng và complaint của đơn hàng dùng `categoryCode = sales_order`, lần lượt có
`eventGroupCode = delivery` và `eventGroupCode = complaint`.

Các topic `0-28` được giữ nguyên để giải mã dữ liệu lịch sử. Code mới append các topic cụ thể:

```text
SampleRequestDataChangeRequested = 29 -> plm.sample_request.data_change.requested -> sample_request/change
SampleRequestDataChangeApproved  = 30 -> plm.sample_request.data_change.approved  -> sample_request/change
SampleRequestDataChangeRejected  = 31 -> plm.sample_request.data_change.rejected  -> sample_request/change
MerchandiseOrderDeliveryPaused   = 32 -> sales.merchandise_order.delivery.paused  -> sales_order/delivery
MerchandiseOrderDeliveryResumed  = 33 -> sales.merchandise_order.delivery.resumed -> sales_order/delivery
QuotationSent                    = 34 -> crm.quotation.sent                       -> quotation/status
QuotationMessageCreated          = 35 -> crm.quotation.message.created            -> quotation/message
QuotationRequested               = 36 -> crm.quotation.requested                  -> quotation/request
SampleRequestSampleSent          = 37 -> plm.sample_request.sample_sent           -> sample_request/sample
SampleRequestFormulaCompleted    = 38 -> plm.sample_request.formula.completed     -> sample_request/formula
SampleRequestFormulaUpdateRequested = 39 -> plm.sample_request.formula_update.requested -> sample_request/formula
SampleRequestFormulaUpdateApproved  = 40 -> plm.sample_request.formula_update.approved  -> sample_request/formula
SampleRequestFormulaUpdateRejected  = 41 -> plm.sample_request.formula_update.rejected  -> sample_request/formula
SampleRequestFormulaUpdateCancelled = 42 -> plm.sample_request.formula_update.cancelled -> sample_request/formula
SampleRequestDirectPatchNotified    = 43 -> plm.sample_request.direct_patch.notified    -> sample_request/change
CustomerAiSummaryAutomationStatus   = 44 -> dev.customer.ai_summary.automation_status   -> system/automation
SampleRequestCustomerFeedbackRecorded = 47 -> plm.sample_request.customer_feedback.recorded -> sample_request/sample
QuotationPricingApproved             = 48 -> crm.quotation.pricing.approved             -> quotation/pricing
SampleRequestFormulaApproved         = 49 -> plm.sample_request.formula.approved         -> sample_request/formula
QuotationPricingExpired              = 50 -> crm.quotation.pricing.expired              -> quotation/pricing-alert
```

`SampleRequestFormulaApproved` được phát sau khi Lab xác nhận Formula trong ngữ cảnh một Sample Request.
Notification dùng link `/crm/quotations/product-pricing-options?keyword={ColourCode}` để mở tra cứu giá theo
mã màu. Payload chỉ giữ metadata của thread/Sample Request; không chứa material cost, giá sản xuất, giá bán
hoặc margin. Recipient được resolve theo participant hiện có, manager và required recipient của Sample Request;
SignalR/Web Push tiếp tục nhận notification qua outbox chung, không có kênh realtime riêng.

`SampleRequestUpdateRequested = 25`, `SampleRequestUpdateApproved = 27` và
`SampleRequestUpdateRejected = 28` vẫn giữ nguyên mapping cũ nhưng không còn được dùng cho luồng data-change mới.
Notification data-change chỉ chứa metadata định vị
`conversationId`, `messageId`, `sampleRequestId`; proposal đầy đủ nằm trong `InternalMessage.PayloadJson`
và được đọc qua API thread có kiểm tra participant.

`QuotationSent` được publish bởi handler `MarkQuotationSentCommandHandler` sau khi lưu trạng thái báo giá,
CustomerInteraction và InternalMail message. Recipient là leader của sale group chứa `quotation.SaleEmployeeId`
(`MemberInGroup.IsAdmin = true` trong group `CMR` hoặc `CMR.*`) cộng President/Developer active cùng công ty,
loại người thao tác. Payload gồm `conversationId`, `messageId`, `quotationId`,
`quotationExternalId`, `customerInteractionId` và `customerId`; link điều hướng là
`/crm/quotations/{quotationId}`. Notification service tiếp tục tạo SignalR outbox và Web Push outbox theo cấu hình
chung, không phát trực tiếp từ module Quotation.

`QuotationRequested` được publish bởi `RequestQuotationCommandHandler` sau khi thêm action message vào
InternalMail conversation của báo giá. Mỗi lần gọi tạo notification/message mới nhưng tái sử dụng conversation theo
`CompanyId + RelatedType=Quotation + RelatedId=quotationId`. Recipient là leader của sale group chứa
`quotation.SaleEmployeeId` cộng President/Developer active cùng công ty, loại người thao tác. Payload camelCase gồm
`contentType`, `relatedType`, `relatedId`, `relatedExternalId`, `conversationId`, `messageId`, `isUrgent`.
Notification service vẫn tạo SignalR outbox và Web Push outbox theo cấu hình chung.
Payload còn có `action.code = Quotation.OpenPricingOptions` và `action.parameters` chứa `quotationId`,
`quotationExternalId`. `Notification.Link` để trống; từng client ánh xạ action nghiệp vụ sang route riêng.

`QuotationPricingApproved` được publish bởi `ApproveProductPricingVersionCommandHandler` sau khi version giá đã
được lưu `Approved`. Handler reconcile các báo giá `PendingApproval` cùng company/currency trước, tự snapshot và
chuyển báo giá sang `Approved` nếu mọi line đã đủ giá. Sau đó mỗi Sale phụ trách active cùng company nhận một
notification cho từng báo giá bị ảnh hưởng, trừ khi chính Sale đó là người duyệt.
người duyệt. Payload không chứa material cost, manufacturing cost hoặc margin; chỉ chứa quotation id/code,
product id/code, pricing version id/version, `quotationStatus`, `isQuotationPricingComplete` và
`action.code = Quotation.Open`. Topic có
`categoryCode = quotation`, `eventGroupCode = pricing`, `topicCode = crm.quotation.pricing.approved`.
Notification vẫn đi qua `INotificationService.PublishAsync`, vì vậy SignalR/Web Push tiếp tục dùng outbox chung;
module Quotation không gọi realtime channel trực tiếp và không tạo thêm InternalMail message cho sự kiện này. Khi
báo giá đã có conversation active cùng `CompanyId + RelatedType=Quotation + RelatedId=quotationId`, notification
được gắn `conversationId` để Hub gom với thread; nếu chưa có conversation, backend không tạo thread giả.

`QuotationPricingExpired` được `QuotationPricingExpiryReminderWorker` publish khi giá chuẩn đã duyệt quá
`Features:Quotations:ApprovedPricingReviewAfterDays` và vẫn được một quotation `Approved` chưa gửi dùng. Worker
chuyển báo giá về `PendingApproval`, ghi history, giữ snapshot cũ, tạo action message trong conversation rồi publish
topic `crm.quotation.pricing.expired` với
`categoryCode = quotation`, `eventGroupCode = pricing-alert`, severity `Warning`. Payload camelCase chứa quotation,
product, product pricing version, `pricingReviewDueDate`, `quotationStatus`, `conversationId`, `messageId` và action
`Quotation.OpenPricingWorkspace`; không chứa cost, margin hoặc tiers. Người nhận gồm sale phụ trách, leader sale
group, President và Developer active cùng company. Vì publish qua `INotificationService`, SignalR/Web Push outbox,
inbox state và recipient audit dùng hoàn toàn cơ chế chuẩn.

Tin nhắn trả lời trong InternalMail conversation có `RelatedType = Quotation` publish
`QuotationMessageCreated`, thay vì topic chung `InternalMailMessageCreated`. Payload notification liên kết thread
được serialize camelCase và có `contentType`, `conversationId`, `messageId`, `relatedType`, `relatedId`,
`isUrgent`, `attachmentCount`. FE dùng `conversationId` để gom notification và conversation thành một item; dữ liệu PascalCase đã
lưu trước đây vẫn được FE đọc tương thích.

`GET /api/v1/notifications/unread-summary` trả `totalUnread`, danh sách category và `eventGroups` lồng bên trong,
kể cả nhóm có `unreadCount=0`. Service chỉ tính UserState chưa đọc, chưa archive của đúng employee/company hiện
tại; dữ liệu được group theo `Topic` trong database rồi ánh xạ qua `NotificationTopicCatalog`.
Thống kê `legacy_data` cũng dùng cùng mốc `CreatedDate` như feed/detail.
API `/unread-count` cũ vẫn được giữ nguyên để không breaking change.

`CustomerAiSummaryAutomationStatus` được publish bởi
`CustomerInteractionAiSummaryAutomationProcessor` sau lượt tự động có gọi AI hoặc chạm rate limit. Processor
publish riêng theo từng `CompanyId`; recipient là employee active có role global `Developer` trong đúng công ty.
Severity là `Info` khi hoàn tất bình thường, `Warning` khi chạm giới hạn Gemini và `Error` khi có AI request lỗi.
Message và payload camelCase chứa thời gian chạy, các tổng đếm và thống kê theo `Monthly`/`Yearly`/`Lifetime`;
notification tổng kết chứa tối đa 20 mã khách lỗi. Ngay khi một AI request lỗi, processor publish thêm một
notification `Error` dùng cùng topic, có `contentType = CustomerAiSummaryAutomationFailure`, `customerId`, mã/tên
khách, scope/năm/tháng và lỗi đã rút gọn; chỉ Developer cùng company nhận được. Payload không chứa nội dung
interaction, prompt, API key hoặc secret. Lượt chỉ đọc cache và không chạm rate limit không tạo notification.
Với Monthly batch, `aiRequestCount` là số request Gemini thực tế (một request có thể tạo tối đa năm summary),
còn `successCount`/`failedCount` là số summary khách hàng được lưu thành công/lỗi.
Notification tiếp tục dùng outbox SignalR và Web Push chung, không thay đổi payload realtime/push.

Các query user-facing luôn lọc theo current company, current employee và UserState chưa
archive. `remove recipient` chỉ archive UserState để giữ audit, không xóa dữ liệu thật.

## 7. Phân biệt SignalR và Web Push

Một Notification có thể đi qua cả hai kênh nhưng vẫn chỉ là một bản ghi nghiệp vụ:

- SignalR: cập nhật feed/unread khi ứng dụng đang mở.
- Web Push: hiển thị notification hệ điều hành.

Để tránh người dùng thấy hai cảnh báo giống nhau, FE nên dùng SignalR như tín hiệu reload dữ
liệu và để service worker chịu trách nhiệm hiển thị notification hệ điều hành. Dùng
`notificationId` làm `tag` hoặc deduplication key ở service worker.

## 8. Quy tắc bảo mật

- Mọi API Notification/Web Push phải có xác thực.
- Mọi query phải kiểm tra company và quyền của current employee.
- Không cho FE truyền `CompanyId`, `EmployeeId`, `CreatedBy` hoặc role đặc quyền tùy ý.
- Không log cookie, token, VAPID private key, endpoint, `p256dh` hoặc `auth`.
- Link từ Web Push chỉ được FE mở nếu là đường dẫn nội bộ hợp lệ.
- Payload realtime/push không thay thế kiểm tra quyền tại API detail.
- Archive/thu hồi người nhận dùng soft state để giữ lịch sử audit.
- Quyền override recipient phải đi qua role set common `ApplicationRoleSets.Notifications.RecipientManagers`.

## 9. Giới hạn hiện tại

- Team target chưa được resolve đầy đủ thành `NotificationUserState`.
- Worker hiện phù hợp với một API instance; khi scale nhiều instance cần cơ chế claim/lock atomically
  cho cả outbox và WorkTask due reminder để giảm khả năng gửi trùng.
- Worker có thể gửi thành công rồi dừng trước khi lưu `ProcessedAt`; FE nên deduplicate bằng
  `notificationId`.
- Web Push chưa áp dụng quiet hours, mức severity tối thiểu hoặc channel preference.
- Không tạo/chạy migration trong phần tích hợp Web Push; database được giả định đã có schema.
## Sample Request sample-receipt action

Topic không đổi: `SampleRequestSampleSent = 37`, `topicCode = plm.sample_request.sample_sent`, category `sample_request`, event group `sample`. Payload message/notification bổ sung `sampleReceiptAction.sampleRequestSampleTrialId` với trạng thái ban đầu `Pending` để FE hiển thị nút xác nhận nhận mẫu; payload không chứa công thức, giá hoặc dữ liệu kỹ thuật nhạy cảm.

Khi Sale xác nhận, backend cập nhật Trial và payload của `InternalMessage` sang `Confirmed`. Luồng xác nhận không publish notification mới và không thay đổi SignalR/Web Push/outbox; FE dùng response hoặc tải lại thread để lấy trạng thái action mới nhất.

## Sample Request Sales-group recipients

Khi current sender có role `SaleUser`, `SampleRequestRecipientResolver` bổ sung toàn bộ leader active
(`MemberInGroup.IsAdmin = true`) và employee có role `SaleAdmin` cùng nằm trong đúng group active chứa sender thành recipient thường với
`source = sales_group_leader` hoặc `sales_group_admin`. Rule luôn lọc company, active membership/employee, loại sender và gộp id trùng;
không dùng role `Leader`/`SaleAdmin` toàn công ty. Rule được áp dụng ở preview lẫn `SendSampleRequestMessageCommandHandler`,
nên các recipient này trở thành participant và nhận notification theo topic Sample Request hiện hữu. Không thêm topic, payload,
SignalR hay Web Push channel mới: publish tiếp tục đi qua `INotificationService.PublishAsync` và outbox chuẩn.

## Complaint decision topics

- `ComplaintInitialDecision = 45`: topic code `plm.complaint.initial_decision`, `sales_order/complaint`.
- `ComplaintFinalDecision = 46`: topic code `plm.complaint.final_decision`, `sales_order/complaint`.

Complaint decision notifications are published through `INotificationService.PublishAsync`. Initial decisions target the Sale employee who created the complaint. Final decisions target the creator, active CAPA assignees and the effectiveness verifier, excluding the current decision actor. `NotificationService` filters inactive and wrong-company employees while resolving inbox states.

The notification payload only contains complaint id, status, decision, resolution and optional handling-order id. It contains no customer content, root cause, lot data, price or attachment. SignalR/Web Push behavior is unchanged: the existing outbox only signals the notification id and FE reloads feed/detail APIs.

## Sample Request price quote request

`POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests` tái sử dụng topic append-only
`SampleRequestPriceQuoteRequested = 23` (`plm.sample_request.price_quote.requested`, category `sample_request`,
event group `quotation`). Recipient là employee active có global role `President` trong đúng company; current sender
không nhận notification của chính mình nhưng vẫn là participant của conversation.

Payload có `contentType = SampleRequestPriceQuoteRequested`, `conversationId`, `messageId`, `sampleRequestId`,
external id và `priceQuoteRequest` gồm Product cùng Formula đã resolve. Payload không chứa material cost,
manufacturing cost, selling price, margin hoặc tier. Notification tiếp tục đi qua
`INotificationService.PublishAsync`; SignalR và Web Push dùng outbox hiện hữu, không có channel hoặc worker mới.
