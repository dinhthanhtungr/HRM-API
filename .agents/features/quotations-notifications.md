# Quotation Notification Rules

Đọc khi sửa publish/recipient/topic/payload/conversation, mark-sent hoặc side effect message/notification của báo giá. Đọc kèm `notifications.md` và file con theo router đó; báo cáo theo `notifications-final-report.md`.

## Notification Và Recipient

- Mọi notification liên quan báo giá CRM phải có category `Quotation` và topicCode namespace `crm.quotation...`.
- Khi cần topic báo giá mới, append enum ở cuối `TopicNotifications` với explicit numeric value kế tiếp và thêm mapping vào
  `NotificationTopicCatalog`; không đổi số, rename, reorder hoặc tái sử dụng topic cũ.
- `mark-sent` hiện publish `QuotationSent` (`crm.quotation.sent`, category `Quotation`) từ
  `MarkQuotationSentCommandHandler` qua `INotificationService.PublishAsync`.
- Recipient của `QuotationSent` không được lấy bằng role `Leader` global. Rule đúng là:
  `sale group leaders của quotation.SaleEmployeeId + President/Developer active cùng company - sender employee`.
- Sale group leader = employee active có `MemberInGroup.IsAdmin = true` trong group sale `CMR` hoặc `CMR.*`.
- Rule sale group leader dùng service `ISaleGroupRecipientResolver`; không viết lại query recipient riêng trong từng quotation handler nếu service đã đáp ứng.
- Conversation báo giá phải có `RelatedType = Quotation`, `RelatedId = quotationId`, `RelatedExternalId = quotationExternalId`.
- Payload notification/thread báo giá nên có `conversationId`, `messageId`, `relatedType`/`quotationId`, mã báo giá và id liên quan tối thiểu để FE gom feed với conversation.
