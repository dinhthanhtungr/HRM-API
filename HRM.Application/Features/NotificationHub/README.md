# Notification Hub read API

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
đã mở tin.

Khi không dùng `urgentOnly`, endpoint chạy số query cố định theo trang: feed hiện hành, một batch conversation,
rồi tối đa một batch Sample Request và một batch Quotation. Tab Gấp có thể quét thêm batch feed khi các tin Gấp thưa;
mỗi batch vẫn resolve conversation và dữ liệu liên quan theo tập hợp, không query theo từng notification.
