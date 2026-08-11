# Notification Final Report Rules

Đọc file này trước khi trả lời cuối nếu task có tạo/sửa notification topic, publish flow, payload, recipient rule, SignalR/Web Push hoặc outbox worker.

Câu trả lời cuối phải nói rõ:

- Đã thêm/sửa topic nào, enum nào, numeric value nào, `topicCode` nào và category nào.
- Mapping nằm ở file/catalog nào.
- Notification được publish từ handler/service nào và đi qua `INotificationService.PublishAsync` ra sao.
- Recipient được resolve theo employee/user/role/team nào.
- Role nào là global, role nào bị giới hạn theo group/team/department.
- Có loại trừ current user/sender, inactive employee, wrong company hoặc muted participant không.
- Payload chứa gì và vì sao không chứa dữ liệu nhạy cảm.
- SignalR/Web Push có thay đổi gì không; nếu không thay đổi cũng nói rõ.
- README/WEB_PUSH docs đã cập nhật hay vì sao không cần cập nhật.
- Build/test đã chạy và kết quả.

Không được chỉ trả mỗi đường dẫn file khi task có tạo/sửa notification; phải mô tả hành vi nghiệp vụ và tác động FE/API.
