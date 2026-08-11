# Notification Web Push Rules

Đọc file này khi task đụng Web Push, VAPID, push payload, public key, subscription hoặc outbox push worker.

## Contract

- Web Push chỉ là kênh đánh thức/nhắc người dùng, không thay thế inbox notification.
- Notification đã lưu và `NotificationUserState` là nguồn thật để FE đọc feed/detail.
- Business handler không gọi `IWebPushSender` trực tiếp; handler publish notification, outbox/worker xử lý push.
- Không gửi Web Push cho user không có quyền xem notification trong feed.

## Payload

- Payload Web Push chỉ chứa dữ liệu tối thiểu như `notificationId`, `topicCode`, `category`, `link` nếu an toàn.
- Không gửi full nội dung nghiệp vụ nhạy cảm qua Web Push.
- Không chứa token, password, refresh token, cookie, secret, VAPID private key, endpoint, `p256dh`, `auth` hoặc dữ liệu cross-company.
- Không log endpoint, `p256dh`, `auth`, VAPID private key hoặc payload nhạy cảm.

## Config

- VAPID private key không được lưu trong source; dùng environment variable hoặc user secret.
- Nếu đổi Web Push config, worker, retry, timeout hoặc payload contract, cập nhật `HRM.Application/Features/Notifications/WEB_PUSH.md`.
