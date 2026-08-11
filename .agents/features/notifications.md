# Notifications, SignalR, Web Push

Đọc file này khi task liên quan notifications, notification recipients, user states, outbox, SignalR, Web Push hoặc internal mail notification.

File này là router của module notification. Sau khi đọc file này, đọc tiếp file con phù hợp với phần đang sửa.

## Luật Nền Luôn Áp Dụng Khi Đụng Notification

Notification hiện tại là inbox + realtime:

- `notifications`: nội dung thông báo.
- `notification_recipients`: dấu vết ý định gửi cho user/role/team.
- `notification_user_states`: inbox/read/unread/archive theo từng employee.
- `outbox_messages`: hàng đợi để worker đẩy SignalR/Web Push.

Business handler gọi `INotificationService.PublishAsync`. Handler nghiệp vụ không gọi SignalR/Web Push trực tiếp; mọi side effect realtime/push phải đi qua notification service/outbox hiện có.

SignalR và Web Push chỉ là kênh báo hiệu. Không đẩy full nội dung nghiệp vụ qua SignalR/Web Push. FE nhận tín hiệu thì gọi API feed/detail/thread để lấy dữ liệu thật. User chỉ thấy notification trong feed nếu có `NotificationUserState` và chưa archived.

Nếu thu hồi người nhận nhầm, ưu tiên set `IsArchived = true`, không xóa cứng. Web Push không log endpoint, `p256dh`, `auth`, VAPID private key hoặc payload nhạy cảm.

## Đọc Thêm Theo Ngữ Cảnh

- Nếu thêm/sửa topic, `topicCode`, category, catalog hoặc mapping FE: đọc `.agents/features/notifications-topic.md`.
- Nếu thêm/sửa recipient, role, team, group, owner, sender exclusion hoặc quyền thu hồi người nhận: đọc `.agents/features/notifications-recipient.md`.
- Nếu thêm/sửa Web Push, VAPID, push payload, public key, subscription hoặc push worker: đọc `.agents/features/notifications-web-push.md`.
- Nếu task liên quan outbox worker hoặc SignalR dispatch sâu, đọc file này và README notification hiện có; nếu nội dung dài lên, tạo thêm `.agents/features/notifications-realtime.md`.
- Nếu task liên quan báo giá và notification: đọc thêm `.agents/features/quotations.md`.
- Nếu task liên quan sample request/PLM và notification: đọc thêm `.agents/features/plm.md`.
- Trước khi trả lời cuối cho task có sửa notification: đọc `.agents/features/notifications-final-report.md`.

## Documentation

Nếu đổi notification topic, payload, recipient rule, SignalR/Web Push side effect hoặc outbox worker, cập nhật:

- `HRM.Application/Features/Notifications/README.md`
- `HRM.Application/Features/Notifications/WEB_PUSH.md` nếu liên quan Web Push

Không được chỉ trả mỗi đường dẫn file khi task có tạo/sửa notification; phải mô tả hành vi nghiệp vụ và tác động FE/API theo `notifications-final-report.md`.
