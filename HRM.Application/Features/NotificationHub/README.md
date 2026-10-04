# Notification Hub API

## Mục đích

`GET /api/v1/notification-hub/items` là read-model dành riêng cho danh sách Notification Hub. Endpoint không thay
thế hoặc thay đổi contract của `GET /api/v1/notifications/feed` và
`GET /api/v1/internal-mail/conversations`; các API read/write, read-state, archive, message và attachment cũ vẫn giữ
nguyên trách nhiệm.

Query hỗ trợ `categoryCode`, `eventGroupCode`, `urgentOnly`, `take` và opaque `cursor`. Category/event-group, visibility theo
`NotificationUserState`, legacy-data, thứ tự và keyset paging được tái sử dụng từ notification feed. `nextCursor`
được tạo từ item cuối và dùng trực tiếp cho trang kế tiếp.

`urgentOnly=true` phục vụ tab **Gấp** và có thể kết hợp với mọi `categoryCode`/`eventGroupCode` ở thanh tab phía
trên. Tab này chỉ tổng hợp notification có conversation mà current employee là participant active, cùng company, và
conversation hiện còn ít nhất một tin nhắn do người khác gửi với `isUrgent=true` (đã đọc vẫn là Gấp cho đến khi tin
nhắn bị bỏ đánh dấu hoặc xóa). Notification không có conversation không nằm trong tab Gấp. Do `conversationId` là
metadata JSON tương thích lịch sử, backend duyệt theo keyset của feed rồi lọc theo batch; không dùng nối chuỗi JSON.

## Response contract

Mỗi `items[]` gồm `notification` theo contract feed hiện hành và `conversationInfo`. Khi notification không có
`conversationId`, payload lịch sử không đọc được, conversation không active/cùng company hoặc current employee không
còn là participant active thì `conversationInfo = null`; notification vẫn được trả bình thường.

```json
{
  "items": [
    {
      "notification": {
        "id": "019f...",
        "categoryCode": "sample_request",
        "eventGroupCode": "formula",
        "conversationId": "019e...",
        "messageId": "019e..."
      },
      "conversationInfo": {
        "displayTitle": "TP_22350 - BH51012C",
        "lastSenderName": "Nguyễn Văn A",
        "lastMessageBody": "Đã hoàn tất công thức",
        "lastMessageAt": "2026-09-09T10:30:00+07:00",
        "unreadCount": 2,
        "isUrgent": false,
        "sampleRequestInfo": {
          "requestCode": "TP_22350",
          "customerCode": "KH_1234",
          "customerName": "CÔNG TY ABC",
          "saleName": "Lê Anh"
        },
        "quotationInfo": null
      }
    }
  ],
  "nextCursor": "opaque-value-or-null"
}
```

`sampleRequestInfo` chỉ có khi conversation liên kết `SampleRequest`; `quotationInfo` chỉ có khi liên kết
`Quotation`. Hai block dùng cùng DTO và resolver theo batch với Internal Mail nên không suy Customer/Sale từ title,
body hoặc người gửi cuối. `lastMessage*`, `unreadCount` và `isUrgent` là trạng thái hiện tại của toàn conversation,
không phải riêng event group đang lọc. `isUrgent` không phụ thuộc read-state để tab Gấp không biến mất khi người nhận
đã mở tin. `unreadCount` lấy giá trị lớn hơn giữa message unread và toàn bộ inbox notification unread của
employee/company liên kết thread, kể cả notification nằm ngoài trang feed đang tải. Cùng quy tắc với
Internal Mail list/detail; không cộng hai nguồn để tránh đếm đôi cùng sự kiện.

Khi không dùng `urgentOnly`, endpoint chạy số query cố định theo trang: feed hiện hành, một batch conversation,
rồi một batch đếm notification unread cho các conversation, tối đa một batch Sample Request và một batch Quotation.
Tab Gấp có thể quét thêm batch feed khi các tin Gấp thưa;
mỗi batch vẫn resolve conversation và dữ liệu liên quan theo tập hợp, không query theo từng notification.

## Đánh dấu tất cả đã đọc

```http
POST /api/v1/notification-hub/read-all
```

Endpoint dành cho nút **Đánh dấu tất cả đã đọc** của Notification Hub. Trong một transaction Repeatable Read, backend dùng bulk
update để đồng bộ cả hai lớp trạng thái đọc của current employee:

- `NotificationUserState`: notification chưa archive thuộc company hiện tại.
- `InternalMessageReadState`: message chưa đọc, chưa xóa trong conversation active cùng company mà employee vẫn là
  participant active.
- `InternalConversationParticipant.LastReadAt`: mốc đọc của những conversation đang có message chưa đọc.

Endpoint không lặp một request cho từng message và không thay đổi contract của
`POST /api/v1/notifications/read-all`, vốn chỉ xử lý notification. Response:

```json
{
  "notificationsUpdated": 150,
  "messagesUpdated": 1000,
  "conversationsUpdated": 55
}
```

Các giá trị là số row thực tế được cập nhật trong snapshot; `0` nghĩa là không có row phù hợp được cập nhật,
không khẳng định inbox hiện tại không còn unread. Cả ba cập nhật dùng cùng snapshot dữ liệu đã commit tại câu
SQL đầu tiên trong transaction. Tin/notification commit sau snapshot vẫn chưa đọc; không dùng bộ lọc timestamp
riêng cho message và notification vì hai thời điểm tạo có thể khác nhau trong cùng transaction gửi tin.
LastReadAt không bị lùi. Nếu transaction xung đột, toàn bộ thao tác rollback và client giữ trạng thái cũ để thử lại.
FE chỉ cập nhật lạc quan những item đã có trước request, giữ thread có tin mới đến giữa chừng, rồi reload unread
summary. Không ép badge tổng về 0 từ response này.
