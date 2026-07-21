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
5. Resolve target user và role thành các `EmployeeId` active trong cùng company.
6. Tạo một `NotificationUserState` cho mỗi employee thực tế được nhận.
7. Tạo outbox `InAppPush` cho SignalR.
8. Nếu Web Push được bật và có người nhận, tạo thêm outbox `WebPush`.
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
3. Tìm các `NotificationUserState` chưa archive.
4. Tìm subscription active của đúng employee và company.
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

- `category`: nhóm notification theo `NotificationTopicCategoryRules`.
- `take`: số lượng item, được giới hạn trong service.
- `afterId` và `afterCreated`: keyset paging để lấy trang cũ hơn.

Notification có `CreatedDate` trước `20/07/2026 00:00:00` luôn được trả về với
`category = LegacyData` (FE hiển thị là "Dữ liệu cũ"). Khi lọc category, dữ liệu trước mốc này chỉ xuất
hiện trong `LegacyData`; các category nghiệp vụ khác chỉ chứa dữ liệu từ mốc này trở đi. Quy tắc chỉ áp
dụng trên response và truy vấn API, không thay đổi `Topic`, `TopicCode` hoặc dữ liệu đã lưu trong database.

Response feed/detail giữ nguyên field `topic` legacy và bổ sung `topicCode`. `TopicNotifications` được lưu dạng
`int` trong database, các giá trị hiện có được khóa bằng số explicit và chỉ được append giá trị mới ở cuối.
Frontend mới ưu tiên `topicCode` để render icon, màu, nhóm nghiệp vụ, điều hướng và filter; frontend cũ tiếp tục
dùng `topic` mà không bị breaking change. Toàn bộ mapping nằm tại `NotificationTopicCodes`; không hard-code mã
topic trong service hoặc handler.

`GET /api/v1/notifications/unread-summary` trả `totalUnread` và đầy đủ các category ngoại trừ `All`, kể cả
category có `unreadCount=0`. Service chỉ tính UserState chưa đọc, chưa archive của đúng employee/company hiện
tại; dữ liệu được group theo `Topic` trong database rồi ánh xạ sang category bằng
`NotificationTopicCategoryRules`. Thống kê `LegacyData` cũng dùng cùng mốc `CreatedDate` như feed/detail.
API `/unread-count` cũ vẫn được giữ nguyên để không breaking change.

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

## 9. Giới hạn hiện tại

- Team target chưa được resolve đầy đủ thành `NotificationUserState`.
- Worker hiện phù hợp với một API instance; khi scale nhiều instance cần cơ chế claim/lock atomically
  cho cả outbox và WorkTask due reminder để giảm khả năng gửi trùng.
- Worker có thể gửi thành công rồi dừng trước khi lưu `ProcessedAt`; FE nên deduplicate bằng
  `notificationId`.
- Web Push chưa áp dụng quiet hours, mức severity tối thiểu hoặc channel preference.
- Không tạo/chạy migration trong phần tích hợp Web Push; database được giả định đã có schema.
