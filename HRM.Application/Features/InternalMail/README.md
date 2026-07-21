# InternalMail API và luồng Notification

## Mục tiêu

InternalMail là nguồn dữ liệu thật của hòm thư nội bộ:

- `InternalConversation`: một cuộc trao đổi được gộp thành một dòng trong hòm thư.
- `InternalMessage`: từng nội dung trao đổi, không bị gộp hoặc xóa khi FE gộp conversation.
- `InternalConversationParticipant`: quyền truy cập và trạng thái archive/mute cá nhân.
- `InternalMessageReadState`: trạng thái đọc chi tiết của từng message.
- `Notification`: chuông báo/inbox realtime; không thay thế conversation hoặc message.

Luồng gửi:

```text
Business API hoặc InternalMail API
-> tạo InternalMessage
-> tạo Notification cho participant không mute
-> OutboxMessage
-> SignalR notify { notificationId }
-> FE reload notification/conversation bằng API có phân quyền
```

## API InternalMail

Base route: `/api/v1/internal-mail`

| Method | Route | Ý nghĩa |
|---|---|---|
| `GET` | `/conversations` | Hòm thư đã gộp theo conversation của current employee |
| `POST` | `/conversations` | Tạo cuộc trao đổi nội bộ tự do (`RelatedType = Internal`) |
| `GET` | `/conversations/{conversationId}` | Lấy header và participant |
| `GET` | `/conversations/{conversationId}/messages` | Lấy thread có phân trang |
| `POST` | `/conversations/{conversationId}/messages` | Reply/gửi thêm message cho toàn bộ participant |
| `PATCH` | `/messages/{messageId}` | Người gửi sửa body/isUrgent; message System không được sửa |
| `DELETE` | `/messages/{messageId}` | Soft delete, giữ audit |
| `POST` | `/conversations/{conversationId}/read` | Đánh dấu thread đã đọc cho current employee |
| `PATCH` | `/conversations/{conversationId}/preferences` | Archive/restore hoặc mute/unmute cá nhân |
| `GET` | `/conversations/{conversationId}/participants` | Danh sách người tham gia |
| `POST` | `/conversations/{conversationId}/participants` | Owner thêm Member/Watcher |
| `DELETE` | `/conversations/{conversationId}/participants/{employeeId}` | Owner gỡ participant, giữ message/read audit |

Các filter chính của `GET /conversations`:

- `relatedType`: enum `InternalMailRelatedType`; bỏ trống để lấy tất cả.
- `unreadOnly`: chỉ lấy conversation có message chưa đọc.
- `archived`: lấy inbox thường hoặc archive cá nhân.
- `keyword`, `pageNumber`, `pageSize`: search và phân trang.

## API SampleRequest

Trao đổi SampleRequest phải đi qua feature PLM để BE tự validate hồ sơ, company và người nhận mặc định:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/messages
GET  /api/v1/plm/sample-requests/{sampleRequestId}/messages
```

Route cũ `/{sampleRequestId}/notifications` đã được bỏ. Request gửi mới dùng `ReplyToMessageId`; không còn `ReplyToNotificationId` hoặc `ReplyMode`.

Mỗi SampleRequest có tối đa một conversation active theo:

```text
RelatedType = SampleRequest
RelatedId = sampleRequestId
```

`PriceQuoteRequest`, `ChangeRequest`, `UpdateRequest` và `GeneralMessage` đều là message trong cùng conversation. Mỗi message vẫn tạo notification riêng; FE gộp danh sách theo `ConversationId`.

## Notification category

`TopicNotifications` mô tả sự kiện cụ thể. `NotificationCategory` là nhóm lớn để FE lọc:

```http
GET /api/v1/notifications/feed?category=SampleRequest
```

`category=All` hoặc bỏ trống lấy tất cả. Yêu cầu báo giá trong ngữ cảnh SampleRequest vẫn thuộc category `SampleRequest`; topic cụ thể là `SampleRequestPriceQuoteRequested`.

Topic enum được xem là append-only vì có thể đang lưu dạng số trong DB. Không chèn topic mới vào giữa danh sách.

## Quyền và bảo mật

- Tất cả controller có `[Authorize]`.
- Query luôn lọc `CompanyId` của current user.
- Chỉ participant mới đọc conversation/message, tránh IDOR qua GUID.
- FE không được truyền `CompanyId`, sender hoặc owner tùy ý.
- Chỉ sender sửa message; sender hoặc owner mới được soft delete.
- Chỉ owner thêm/gỡ participant; endpoint thêm không cho tự gán role `Owner`.
- Message đã xóa không trả lại body/payload cho FE.
- Notification không có API public để FE tự tạo; notification được publish như side effect của nghiệp vụ.
- SignalR chỉ gửi `notificationId`, không gửi nội dung đầy đủ.

## Lưu ý triển khai

- Thay đổi này không tạo migration và không thêm bảng/cột.
- API attachment của message chỉ trả `AttachmentId`, tên và dung lượng; endpoint tải file vẫn phải kiểm tra quyền với entity cha.
- Reminder/scheduler chưa thuộc v1.
