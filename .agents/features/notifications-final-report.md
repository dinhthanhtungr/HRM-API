# Notification Final Report Rules

Áp dụng khi sửa topic, publish flow, payload, recipient, SignalR/Web Push hoặc outbox. Dùng nội dung đã có trong context; không cần đọc lại trước mỗi câu trả lời cuối.

Báo ngắn hành vi nghiệp vụ và tác động FE/API, không chỉ trả đường dẫn file. Chỉ trình bày các nhóm chịu tác động:

- Topic/mapping: enum, numeric value, topicCode, category và catalog.
- Publish: handler/service phát sự kiện và đường đi qua `INotificationService.PublishAsync`.
- Recipient/quyền: employee/user/role/team, global hay group/team/department, company/active/sender/muted exclusions.
- Payload: dữ liệu gửi và cách tránh lộ dữ liệu nhạy cảm.
- Transport: thay đổi SignalR/Web Push/outbox; nêu phần giữ nguyên khi cần làm rõ tác động của thay đổi.

Luôn báo tài liệu cập nhật hoặc lý do không cần, build/test thực tế và blocker. Không liệt kê mọi thành phần không đổi.
