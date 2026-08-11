# CRM Quotations

Đọc file này khi task liên quan báo giá, quotation, quotation line, refresh price, mark sent, pricing snapshot hoặc dropdown công thức/giá cho sale.

## Business Rules

- Báo giá là snapshot tại thời điểm lập/gửi; không để báo giá cũ bị thay đổi giá khi product/formula/giá nguồn đổi sau này.
- Line phải snapshot product code/name/unit và unit price.
- Nếu thêm formula/pricing source cho quotation, line nên snapshot `FormulaId`, `FormulaExternalId`, `PriceDate`, `ConfirmedBy`, `ConfirmedDate` nếu contract yêu cầu.
- Refresh price là hành động rõ ràng, không âm thầm đổi giá line đã gửi nếu rule không cho phép.
- Mark sent phải cập nhật status/history theo pattern hiện có.

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
- Khi sửa notification báo giá, câu trả lời cuối phải nói rõ topic/topicCode/category, nơi publish, recipient rule, role global/group-scoped, payload và SignalR/Web Push có đổi hay không.

## Security

- Mỗi query/command phải check company scope của quotation, customer, product và formula nếu có.
- Không cho FE set `CompanyId`, `SaleEmployeeId`, created/updated fields hoặc status đặc quyền trực tiếp.
- Validate line count, quantity, unit price, discount/tax percent, product active và cùng company.

## Dropdown Formula/Pricing

- Nếu sale cần chọn công thức và xem giá gần nhất, tạo endpoint lookup/options riêng cho báo giá thay vì dùng lại sample request lookup.
- Dropdown chỉ là preview; BE vẫn phải validate lại formula/product/company và lưu snapshot khi tạo/cập nhật báo giá.
- Giá/label trả về nên là contract rõ ràng; status/type/source nên là enum/code ổn định nếu FE có thể tự map.

## Documentation

Nếu đổi API/DTO/rule/status/side effect của báo giá, cập nhật `HRM.Application/Features/CRM/Quotations/README.md`.

