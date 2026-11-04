# Notification Hub API

Với message mới có notificationAudienceVersion=1, unreadCount và unreadOnly phản ánh notification được định
tuyến đến employee; trạng thái đọc message vẫn giữ riêng. Lịch sử thiếu marker giữ logic đối soát bên dưới.
Xem [notification routing](../InternalMail/NOTIFICATION_ROUTING.md) cho rule audience và @tag.

## Mục đích

Thời gian `conversationInfo.lastMessageAt` dùng cùng activity projection với inbox: nhóm con rỗng không lấy
thời điểm tạo tab làm tin mới; fallback là mốc tạo hồ sơ gốc. Không thay đổi timestamp của notification độc lập.

`conversationInfo.quickGroups` cung cấp lựa chọn Kỹ thuật/Giá đã lọc quyền theo lô cho trang inbox.
Hover chỉ dùng metadata này, không gọi detail hoặc ensure-defaults. ID null là preview nhóm chưa tạo;
chỉ bấm mới khởi tạo và kiểm tra lại quyền. Xem [contract lựa chọn nhanh](../InternalMail/AREAS.md#lựa-chọn-nhanh-trong-inbox).

`GET /api/v1/notification-hub/items` là read-model dành riêng cho danh sách Notification Hub. Endpoint không thay
thế hoặc thay đổi contract của `GET /api/v1/notifications/feed` và
`GET /api/v1/internal-mail/conversations`; các API read/write, read-state, archive, message và attachment cũ vẫn giữ
nguyên trách nhiệm.

Query hỗ trợ `categoryCode`, `eventGroupCode`, `urgentOnly`, `unreadOnly`, `take` và opaque `cursor`. Category/event-group, visibility theo
`NotificationUserState`, legacy-data, thứ tự và keyset paging được tái sử dụng từ notification feed. `nextCursor`
được tạo từ item cuối và dùng trực tiếp cho trang kế tiếp.

Chọn sự kiện mới nhất được thực hiện theo nhóm conversation trên inbox của employee/company, lấy mốc thời gian
lớn nhất rồi xử lý hòa bằng ID. Quyền xem vẫn áp dụng ở query ngoài; category, event-group và cursor lọc sau
khi chọn sự kiện đại diện để tin cũ không xuất hiện lại ở tab khác hoặc trang sau. Tab nghiệp vụ bỏ qua chat
khi chọn sự kiện đại diện; tab Trao đổi/Tất cả dùng sự kiện mới nhất thực tế. Tối ưu này không cần migration.

`unreadOnly=true` lọc tại DB trước phân trang, giữ các conversation còn message unread hoặc inbox notification
unread của current employee. Tin mới nhất đã đọc vẫn đại diện cho conversation nếu còn tin cũ chưa đọc.
Notification không gắn conversation chỉ xuất hiện khi chính nó chưa đọc. Có thể kết hợp `urgentOnly=true`.
`GET /api/v1/notifications/feed?unreadOnly=true` dùng cùng quy tắc (và chọn sự kiện mới nhất cho mỗi conversation);
API InternalMail list đã có `unreadOnly=true` để lấy cả conversation chưa đọc không có notification.
Bỏ tham số hoặc false giữ hành vi cũ. Ví dụ `/api/v1/notification-hub/items?unreadOnly=true&take=30` trả
cùng cấu trúc `items`/`nextCursor` bên dưới; danh sách rỗng là không còn kết quả phù hợp. Bộ lọc không thay
read-state, quyền, topic, recipient, payload hay SignalR/Web Push và không cần migration.

`urgentOnly=true` phục vụ tab **Gấp** và có thể kết hợp với mọi `categoryCode`/`eventGroupCode` ở thanh tab phía
trên. Tab này chỉ tổng hợp notification có conversation mà current employee là participant active, cùng company, và
conversation hiện còn ít nhất một tin nhắn do người khác gửi với `isUrgent=true` (đã đọc vẫn là Gấp cho đến khi tin
nhắn bị bỏ đánh dấu hoặc xóa). Notification không có conversation không nằm trong tab Gấp. Backend dùng query liên kết
JSON hiện có để lấy ID notification thuộc inbox và conversation Gấp được phép xem một lần, trước khi chọn sự kiện
mới nhất và keyset/Take. Query chọn sự kiện dùng tập ID này để tránh lặp lại truy vấn JSON/quyền trong các subquery;
vẫn kiểm tra inbox/company/area ở query trả dữ liệu và chỉ enrich trang kết quả; không quét tuần tự
nhiều lô thông báo thường. Scope company/area và participant active vẫn được kiểm tra, kể cả với executive.

## Response contract

Observer hợp lệ được đọc hội thoại active cùng company dù không có participant; `conversationInfo` dùng cùng scope
`InternalMailAreaAccessService.InboxConversations` như API conversations. Notification feed vẫn chỉ gồm inbox của employee,
không tự tạo notification, membership hay read-state để mở quyền đọc. President giữ phạm vi Sample Request đã được cấp;
Developer giữ phạm vi công ty. Người không có quyền observer vẫn cần participant theo scope hiện hành.

Mỗi `items[]` gồm `notification` theo contract feed hiện hành và `conversationInfo`. Khi notification không có
`conversationId`, payload lịch sử không đọc được, conversation không active/cùng company hoặc current employee
không còn quyền đọc theo scope participant/observer thì `conversationInfo = null`; notification vẫn được trả bình thường.

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

Với cả trang thường và `urgentOnly`, endpoint chạy số query cố định theo trang: feed hiện hành, một batch conversation,
rồi một batch đếm notification unread cho các conversation, tối đa một batch Sample Request và một batch Quotation.
Không tăng số batch khi tin Gấp thưa; response và cursor giữ contract cũ. Không cần đổi FE, migration hoặc read-state.

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

### Phân loại theo sự kiện mới nhất

`GET /api/v1/notification-hub/items` chọn một notification đại diện cho mỗi conversation
trong inbox chưa archived của employee hiện tại, sau khi kiểm tra company và quyền xem.
Chọn theo `CreatedDate DESC, Id DESC` trước khi lọc category/eventGroup và cursor.
Các eventGroup nghiệp vụ (request, pricing, status, pricing-alert...) lấy sự kiện cuối
không thuộc nhóm `message`; chat mới không làm mất vị trí trong tab nghiệp vụ.
Tab `message` chỉ hiện khi sự kiện cuối thực sự là message. Tất cả lấy sự kiện cuối thực sự.
Notification không có conversation vẫn là item riêng. Tab legacy_data giữ feed lịch sử.
Lịch sử hội thoại, unread counts, publish, recipient, SignalR/Web Push không thay đổi.
API feed notification thông thường vẫn trả lịch sử; quy tắc mới chỉ áp dụng Hub items.
Việc chọn đại diện chạy trong SQL, không tải toàn bộ inbox về bộ nhớ; metadata vẫn theo trang.
