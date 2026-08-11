# Notification Topic Rules

Đọc file này khi task thêm/sửa notification topic, `topicCode`, `categoryCode`, `eventGroupCode`, context hoặc mapping FE.

## Contract

Tất cả notification phải theo contract topic hiện có:

- Handler phải set `PublishNotificationRequest.Topic` bằng enum `TopicNotifications`, không hard-code string topic trong handler/service.
- `TopicNotifications` là giá trị lưu database và có thể đang lưu dạng số; enum này là append-only.
- Mọi giá trị enum hiện có được xem là đã khóa: không chèn vào giữa, không đổi số, không xóa, không rename và không tái sử dụng số cũ.
- Topic mới phải đặt explicit numeric value bằng số lớn nhất hiện có cộng một và chỉ append ở cuối enum.
- Nếu một topic cũ quá chung hoặc sai nghĩa, giữ topic/mapping cũ để đọc dữ liệu lịch sử; code mới append topic cụ thể hơn và ngừng publish topic cũ cho nghiệp vụ mới.

## Mapping

- `NotificationTopicCatalog` là nguồn duy nhất ánh xạ `TopicNotifications` sang `topicCode`, `categoryCode`, `eventGroupCode` và `aggregateType`.
- `categoryCode` là nhóm nghiệp vụ cấp cao theo aggregate như `sales_order`, `production`, `quotation`; không tạo category riêng cho hành động như giao hàng hoặc khiếu nại.
- `eventGroupCode` là nhóm sự kiện con như `delivery`, `complaint`, `change`, `schedule`, `material`.
- `NotificationTopicCodes` và `NotificationTopicCategoryRules` phải delegate về catalog.
- `NotificationService.PublishAsync` phải từ chối topic chưa được cấu hình trong catalog.
- Fallback `notification.unknown.<number>` chỉ dành cho đọc dữ liệu lịch sử lạ, không phải topic hợp lệ để publish mới.

`topicCode` dùng format phân cấp ổn định như `plm.sample_request.updated`, `crm.customer.follow_up.due`, `internal_mail.message.created`. Topic mới phải mô tả đúng sự kiện nghiệp vụ theo cấu trúc `<domain>.<aggregate>.<event>` hoặc `<domain>.<aggregate>.<sub_resource>.<event>`.

Không dùng topic `*.updated` cho sự kiện có ý nghĩa riêng như approved/rejected/paused/resumed/due/assigned; tạo topic cụ thể để FE xử lý chính xác.

Notification mới có đối tượng nghiệp vụ phải set `AggregateId` và `AggregateCode` trên `PublishNotificationRequest`.
Nếu notification thuộc thread thì set thêm `ConversationId` và `MessageId`. `NotificationService` tự gộp metadata
chuẩn vào `PayloadJson`; handler không tự tạo một cấu trúc `context` khác. Entity và schema notification không đổi.

## Checklist Khi Tạo Topic Mới

1. Kiểm tra số lớn nhất hiện có trong `HRM.Domain/Enums/Notifications/TopicNotifications.cs`.
2. Append enum mới ở cuối với explicit numeric value kế tiếp.
3. Thêm đúng một definition gồm topic code, category code, event group code và aggregate type vào `NotificationTopicCatalog`.
4. Publish topic mới qua `PublishNotificationRequest.Topic = TopicNotifications.<Name>`.
5. Set aggregate/thread metadata trên `PublishNotificationRequest` nếu sự kiện liên quan đối tượng nghiệp vụ.
6. Cập nhật `HRM.Application/Features/Notifications/README.md` và README feature phát notification nếu topic public cho FE.
