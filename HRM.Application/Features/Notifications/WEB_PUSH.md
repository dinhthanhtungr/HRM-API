# Web Push cho Notification

## Mục đích

Web Push là kênh chuyển phát bổ sung cho Notification hiện tại. Notification và
`NotificationUserState` vẫn là nguồn dữ liệu chính; Web Push chỉ giúp trình duyệt hoặc hệ
điều hành báo có thông báo mới khi tab không mở hoặc SignalR chưa kết nối.

```text
Business feature
-> INotificationService.PublishAsync
-> Notification + NotificationUserState
-> OutboxMessage(WebPush)
-> WebPushOutboxProcessor
-> push service của trình duyệt
-> service worker FE hiển thị thông báo hệ điều hành
-> người dùng bấm thông báo
-> FE mở link và gọi API có phân quyền để lấy dữ liệu thật
```

Payload Web Push có `notificationId`, danh sách employee nhận alert, tiêu đề chung và đường dẫn. Không gửi nội dung
nghiệp vụ đầy đủ qua push để tránh lộ dữ liệu trên màn hình khóa.

## API cho FE

Tất cả endpoint đều yêu cầu đăng nhập:

```http
GET  /api/v1/web-push/public-key
POST /api/v1/web-push/subscriptions
POST /api/v1/web-push/subscriptions/unsubscribe
```

Đăng ký subscription:

```json
{
  "endpoint": "https://push-service.example/...",
  "keys": {
    "p256dh": "...",
    "auth": "..."
  },
  "deviceName": "Chrome trên Windows"
}
```

Hủy đăng ký:

```json
{
  "endpoint": "https://push-service.example/..."
}
```

BE lấy `CompanyId`, `EmployeeId` và `User-Agent` từ phiên đăng nhập/request hiện tại; FE
không được truyền các field này. Endpoint và khóa subscription không được trả lại sau khi lưu.

Khi logout, FE nên gọi API hủy đăng ký trước, sau đó gọi
`PushSubscription.unsubscribe()` ở browser rồi mới logout tài khoản. Cách này tránh thiết bị
dùng chung tiếp tục nhận push của tài khoản cũ.

## Cấu hình VAPID

Không lưu private key trong source hoặc `appsettings.json`. Cấu hình bằng environment
variable hoặc user secret:

```text
WebPush__Enabled=true
WebPush__VapidSubject=mailto:admin@example.com
WebPush__VapidPublicKey=<public-key>
WebPush__VapidPrivateKey=<private-key>
```

Cặp VAPID key chỉ tạo một lần và phải được giữ ổn định. Đổi key sẽ làm các subscription cũ
không còn dùng được và FE phải đăng ký lại.

## SignalR và Web Push

BE vẫn có thể phát cả SignalR và Web Push cho cùng Notification:

- SignalR dùng để FE đang mở cập nhật feed/unread theo thời gian thực.
- Web Push dùng để hiện thông báo hệ điều hành khi phù hợp.
- FE nên xem event SignalR là tín hiệu reload dữ liệu, không hiện thêm toast trùng nếu service
  worker đã hiện Web Push.

## Retry và bảo mật

- Worker gửi best-effort theo từng subscription và không retry lại cả batch đã gửi thành công.
- Push service trả `404` hoặc `410` thì subscription được vô hiệu hóa.
- Feed/detail API vẫn kiểm tra company và `NotificationUserState`; biết `notificationId` không
  đồng nghĩa với việc được phép đọc notification.
- Không log endpoint, `p256dh`, `auth`, VAPID private key hoặc payload nghiệp vụ nhạy cảm.

Module này giả định bảng `notification.web_push_subscriptions` đã tồn tại. Không tạo hoặc chạy
EF migration trong thay đổi này.
