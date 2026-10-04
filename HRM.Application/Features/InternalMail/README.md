# InternalMail API và luồng Notification

## Phân quyền khu vực (2026-10-04)

[AREAS.md](AREAS.md) mô tả Chung, Kỹ thuật và Báo giá. Mỗi khu vực dùng một conversation với participant riêng; không gán quyền khu vực theo role. Tái sử dụng bảng/cột hiện có, giữ lịch sử ở Chung, không migration/backfill. Chủ cuộc trao đổi tạo khu vực; chủ khu vực mời/gỡ người và thành viên có thể chuyển tiếp từng tin giữa các khu vực họ được phép sử dụng.

## Đồng bộ unread, lưu trữ và xóa tin

`GET /api/v1/internal-mail/conversations/{id}` trả `unreadCount` của employee hiện tại, cùng quy tắc với list:
lấy giá trị lớn hơn giữa số message chưa xóa, không phải do chính employee gửi và có read-state chưa đọc,
và số inbox notification chưa đọc/chưa archive liên kết conversation của employee trong cùng company.
Không cộng hai số vì cùng sự kiện có thể tồn tại ở cả hai nguồn. Notification được đếm trên toàn bộ dữ liệu,
không phụ thuộc trang feed FE đã tải; conversation và participant phải active. Vì vậy notification cũ thiếu
message read-state hoặc chỉ liên kết thread vẫn hiện badge trên thẻ. Đây là query đọc, không tự đánh dấu đã đọc/backfill.
Ví dụ sau khi đọc: `{ "conversationId": "...", "unreadCount": 0, "lastMessageId": "..." }`.
Số 0 là không còn tin chưa đọc tại thời điểm query; tin đến sau request có thể làm số này tăng lại.
FE dùng detail sau mutation để cập nhật cả conversation đang nằm ở trang cũ.

PATCH preferences với `isArchived: true` đồng thời archive notification hiện có của thread cho chính employee,
trong cùng transaction; mute không thay đổi nếu bỏ field. Khôi phục conversation không tự khôi phục notification
đã archive. Hoạt động mới sau mốc lưu trữ có thể tạo notification mới.
DELETE message giữ quyền sender/owner và company/participant scope hiện hành, đồng thời archive notification
liên kết đúng conversationId/messageId cho mọi người nhận trong company. Message và inbox state cập nhật nguyên tử;
không xóa cứng audit. Hành vi này áp dụng khi thao tác chạy; không tự backfill dữ liệu production đã xóa từ trước.

## Quyền đọc conversation từ Executive Sample Request Overview

Mặc định mọi API đọc conversation/message/attachment yêu cầu current employee là participant active của
conversation cùng company. Riêng President và Developer đã có quyền mở Executive Sample Request Pricing Overview
được đọc conversation có `RelatedType = SampleRequest` trong cùng company, kể cả khi không là participant. Rule
này chỉ dành cho read API (detail, messages, search/context và attachment) để action **Mở trao đổi** của Overview
không trả cờ cho phép sai với quyền backend. Nó không áp dụng cho conversation `Internal` hoặc `Quotation`, không
cho phép gửi/sửa/xóa message, và không mở quyền cho role khác.

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
| `GET` | `/conversations/{conversationId}/attachments` | Lấy attachment chat và tệp gốc của Sample Request liên kết theo `kind=All|Image|File`, phân trang độc lập với message |
| `GET` | `/conversations/{conversationId}/messages/search` | Tìm message trong một thread để FE hiển thị số kết quả và nhảy tới message |
| `GET` | `/conversations/{conversationId}/messages/{messageId}/context` | Lấy cụm message trước/sau message đích khi kết quả search chưa được load |
| `POST` | `/conversations/{conversationId}/messages` | Gửi message JSON hoặc multipart có reply/file cho toàn bộ participant |
| `GET` | `/attachments/{attachmentId}` | Mở/tải attachment của thread sau khi kiểm tra participant và company |
| `GET` | `/attachments/{attachmentId}/thumbnail` | Trả thumbnail WebP tối đa 480x480 cho ảnh, dùng cùng quyền truy cập attachment gốc |
| `PATCH` | `/messages/{messageId}` | Người gửi sửa body/isUrgent; message System không được sửa |
| `DELETE` | `/messages/{messageId}` | Soft delete, giữ audit |
| `POST` | `/conversations/{conversationId}/read` | Đánh dấu thread đã đọc cho current employee |
| `PATCH` | `/conversations/{conversationId}/preferences` | Archive/restore hoặc mute/unmute cá nhân |
| `GET` | `/conversations/{conversationId}/participants` | Danh sách người tham gia |
| `POST` | `/conversations/{conversationId}/participants` | Bất kỳ participant active nào thêm Member/Watcher |
| `DELETE` | `/conversations/{conversationId}/participants/{employeeId}` | Owner, President hoặc Developer gỡ participant theo xóa mềm, giữ message/read audit |

Các filter chính của `GET /conversations`:

- `relatedType`: enum `InternalMailRelatedType`; bỏ trống để lấy tất cả.
- `unreadOnly`: chỉ lấy conversation có message hoặc inbox notification chưa đọc; lọc trước phân trang.
- `archived`: lấy inbox thường hoặc archive cá nhân.
- `keyword` hoặc `search`, `pageNumber`, `pageSize`: search và phân trang. Search không phân biệt hoa/thường theo subject, mã liên kết, nội dung tin nhắn; với conversation `SampleRequest` còn tìm theo mã/tên khách hàng và tên Sale phụ trách (`SampleRequest.ManagerBy`). Metadata nguồn luôn bị giới hạn cùng company và Sample Request active.

Mỗi item danh sách trả cả `subject` và `displayTitle`. `subject` là tên conversation được lưu nguyên vẹn;
`displayTitle` là tiêu đề ngắn cho cột inbox. Với Sample Request lịch sử, backend loại prefix
`Trao đổi yêu cầu phối mẫu` khỏi `displayTitle` để mã yêu cầu và mã màu không bị phần mô tả chung chiếm chỗ.
Conversation mới có subject ngắn vẫn trả nguyên subject làm `displayTitle`. Các related type khác mặc định dùng
subject và fallback về `relatedExternalId` khi subject trống. Không có backfill hoặc migration.

Khi `relatedType = SampleRequest`, cả list và detail trả thêm `sampleRequestInfo` để sidebar và header hiển thị
nhất quán: `requestCode`, `colourCode`, `customerId`, `customerCode`, `customerName`, `saleEmployeeId` và
`saleName`. `sale*` được lấy từ `SampleRequest.ManagerBy`, không phải `lastSenderName`. Metadata đọc từ
Sample Request/Customer/Employee hiện tại, luôn lọc cùng company và Sample Request active. Với conversation
không phải Sample Request, record nguồn inactive hoặc quan hệ nguồn không cùng company, `sampleRequestInfo = null`;
FE vẫn dùng `displayTitle`/`subject` như trước.

`sampleRequestInfo.productCategory` trả loại sản phẩm hiện tại dưới dạng
`{ "categoryId": "<uuid>", "code": "PIG", "name": "Bột màu" }`. Mã/tên lấy trực tiếp từ
`SampleRequest.Product.Category`, không suy đoán từ tiêu đề tin nhắn và không phải snapshot lúc gửi.
Trường này là `null` khi sản phẩm/danh mục inactive, khác company, thiếu danh mục hoặc danh mục không thuộc
loại `Product`; `code`/`name` có thể null nếu dữ liệu nguồn chưa có. Áp dụng cho
`GET /api/v1/internal-mail/conversations`, `GET /api/v1/internal-mail/conversations/{conversationId}` và
metadata conversation trong `GET /api/v1/notification-hub/items`. FE có thể dùng `code` để phân loại,
`name` để hiển thị. Đây là metadata, không thay thế kiểm tra quyền truy cập ở backend.
`notifications/feed` và `notifications/unread-summary` không có trường này.

Khi `relatedType = Quotation`, cả list và detail trả thêm `quotationInfo`: `quotationCode`, `customerId`,
`customerCode`, `customerName`, `saleEmployeeId` và `saleName`. `sale*` lấy từ `Quotation.SaleEmployee`, không phải
`lastSenderName`. Contract dùng điều kiện company và báo giá active tương tự Sample Request; nếu không đúng điều kiện,
`quotationInfo = null`. Search `keyword`/`search` cũng áp dụng cho mã/tên khách và Sale phụ trách của cả Sample Request
và Quotation.

Ví dụ response rút gọn:

```json
{
  "displayTitle": "TP_22350 - BH51012C",
  "sampleRequestInfo": {
    "requestCode": "TP_22350",
    "colourCode": "BH51012C",
    "customerCode": "KH_1234",
    "customerName": "CÔNG TY ABC",
    "saleName": "Lê Anh"
  }
}
```

## Đồng bộ trạng thái đã đọc

`POST /api/v1/internal-mail/conversations/{conversationId}/read?throughMessageId={messageId}` giữ response
`204 No Content`. `throughMessageId` là query tùy chọn: FE gửi ID tin mới nhất trong trang messages vừa tải,
backend xác minh tin thuộc đúng conversation/company rồi đánh dấu đến tin đó (thứ tự `SentAt`, message ID).
Mốc ID hợp lệ không bị loại vì `SentAt` lớn hơn đồng hồ API: thời gian đã lưu dùng để xác định thứ tự,
giờ xử lý chỉ dùng cho audit. Tin đã xóa còn xuất hiện dưới dạng tombstone vẫn có thể làm mốc;
read-state của chính tin đã xóa không được cập nhật.
Tin đến sau lúc tải trang không bị đánh dấu nhầm. Client cũ không gửi query vẫn đọc đến mốc bắt đầu request.

Trong cùng transaction, backend cập nhật message read-state, notification liên kết qua cả `conversationId` và
`messageId`, và `LastReadAt` không lùi khi hai request chạy đồng thời. Chỉ áp dụng cho employee hiện tại còn là
participant active trong conversation active cùng company. Notification phải chưa archive và liên kết đúng
tập message được chụp trước cập nhật. Khi có `throughMessageId`, không lọc thêm CreatedDate theo giờ API để
tránh sót thông báo vì lệch đồng hồ. Client cũ không gửi mốc ID vẫn giới hạn notification theo giờ bắt đầu request.
Notification độc lập, JSON hỏng liên kết hoặc trỏ tới message đã xóa được giữ nguyên.

Snapshot lấy trực tiếp từ message, không yêu cầu có sẵn read-state. Backend upsert read-state cho employee
đang đọc để sửa dữ liệu lịch sử bị thiếu; cập nhật đồng thời không tạo trùng khóa và không ghi đè thời điểm đã đọc.
SQL upsert truyền `ReadAt` bằng tham số PostgreSQL `timestamp without time zone`, đúng convention hiện có.
Giữ nguyên giờ địa phương; không để raw SQL suy kiểu `timestamptz` rồi từ chối `DateTimeKind.Local`.
Riêng `QuotationPricingApproved = 48` là sự kiện cấp conversation, publisher không tạo message tương ứng.
Khi đọc thread, notification này được xác nhận nếu đúng conversation và được tạo không muộn hơn lúc request
bắt đầu. Payload phải thiếu `messageId` hoặc có giá trị null; ID hỏng hoặc ID ngoài snapshot không được bỏ qua.
Các topic khác vẫn yêu cầu liên kết message hợp lệ. Không thêm recipient hoặc phát lại notification.

Mở lại thread vẫn đối chiếu các message đã đọc trước đó để sửa notification còn sót, kể cả notification không
nằm trong trang feed FE đang tải. Không cần migration, không phát lại SignalR/Web Push. Sau thành công, FE tải lại
unread-summary và inbox; không suy số tổng từ feed phân trang. Đây không phải thao tác đọc tất cả các conversation.

## Reply Và Attachment

Mỗi item từ API messages và message context trả thêm `replyTo` cùng `attachments`. `replyTo` là preview tối đa 300 ký tự của message gốc, người gửi, loại message và trạng thái xóa; nó có `null` nếu message không phải reply. Khi message gốc đã xóa, `replyTo.isDeleted = true` và `bodyPreview` rỗng để không trả lại nội dung đã xóa.

Mỗi attachment trả `attachmentId`, `fileName`, `sizeBytes`, `contentType`, `kind`, `isImage`, `contentUrl`, `thumbnailUrl` và `downloadUrl`. Với ảnh, FE dùng `thumbnailUrl` để render danh sách/grid và chỉ dùng `contentUrl` khi người dùng mở ảnh gốc. File không phải ảnh có `thumbnailUrl = null`. Không dùng storage path nội bộ.

Thumbnail được tạo WebP tối đa 480x480, giữ tỉ lệ và lưu trong storage cạnh file gốc. Ảnh mới tạo thumbnail ngay lúc upload; ảnh lịch sử được tạo và cache ở lần gọi thumbnail đầu tiên. Endpoint thumbnail vẫn kiểm tra company và participant active như endpoint file gốc, không public storage path và không cần migration.

Sidebar ảnh/file dùng `GET /conversations/{conversationId}/attachments?kind=Image|File&pageNumber=1&pageSize=30`. API này đọc toàn bộ attachment chat trong conversation và, nếu conversation liên kết `SampleRequest`, tự gộp thêm tệp gốc của yêu cầu phối mẫu. Mỗi item có `source = Chat|SampleRequest`; item `Chat` kèm `messageId`, sender và `sentAt` để FE gọi message context rồi scroll về tin gốc, còn item `SampleRequest` có `messageId = null` và dùng nhãn nguồn `SampleRequest`.

Tệp nguồn `SampleRequest` trả `contentUrl`/`downloadUrl` theo route scoped `/conversations/{conversationId}/related-attachments/{attachmentId}` (và `/thumbnail` cho ảnh). Route này kiểm tra current employee vẫn là participant active của conversation, conversation và Sample Request cùng company, attachment còn active và đúng collection của Sample Request. Không gộp hoặc tự trả tệp Formula.

Route gửi message giữ tương thích JSON cũ. Để gửi file, dùng cùng route với `multipart/form-data`:

```http
POST /api/v1/internal-mail/conversations/{conversationId}/messages
Content-Type: multipart/form-data

body=Gửi ảnh xác nhận
replyToMessageId={messageId}
isUrgent=false
files=@anh-xac-nhan.png
files=@bao-gia.pdf
```

Body có thể trống khi có file. Tối đa 10 file, tổng dung lượng tối đa 50 MB; loại file áp dụng rule `AttachmentSlot.InternalMail`. Slot này nhận ảnh, audio/video, PDF, Office, OpenDocument, text/JSON/XML và archive phổ biến, nhưng chặn executable/script. Backend tạo collection/link attachment cho message, chỉ participant cùng company mới đọc/tải file. Message notification payload có thêm `attachmentCount`; SignalR vẫn chỉ gửi `notificationId`.

Search trong thread:

```http
GET /api/v1/internal-mail/conversations/{conversationId}/messages/search?q=ok&pageNumber=1&pageSize=50
```

Endpoint này chỉ tìm trong `InternalMessage.Body` của conversation mà current employee là participant, không trả message đã xóa. Response là
`PagedResult<InternalMessageSearchResultDto>` để FE hiển thị `totalCount`, danh sách `messageId`, `snippet`, người gửi và thời gian.

Khi FE cần scroll tới một kết quả chưa nằm trong DOM:

```http
GET /api/v1/internal-mail/conversations/{conversationId}/messages/{messageId}/context?before=10&after=10
```

Response trả `targetMessageId`, danh sách `messages` theo thứ tự thời gian tăng dần, `hasOlderMessages` và `hasNewerMessages`. FE merge cùng thread hiện tại, scroll tới
`targetMessageId` và highlight kết quả.

## API SampleRequest

Trao đổi SampleRequest phải đi qua feature PLM để BE tự validate hồ sơ, company và người nhận mặc định:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/messages
GET  /api/v1/plm/sample-requests/{sampleRequestId}/messages
POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests
POST /api/v1/plm/sample-requests/{sampleRequestId}/data-change-requests/{messageId}/decision
```

Route cũ `/{sampleRequestId}/notifications` đã được bỏ. Request gửi mới dùng `ReplyToMessageId`; không còn `ReplyToNotificationId` hoặc `ReplyMode`.

Mỗi SampleRequest có tối đa một conversation active theo:

```text
RelatedType = SampleRequest
RelatedId = sampleRequestId
```

`PriceQuoteRequest`, `ChangeRequest`, `UpdateRequest` và `GeneralMessage` đều là message trong cùng conversation. Mỗi message vẫn tạo notification riêng; FE gộp danh sách theo `ConversationId`.

Notification chỉ là side effect cho unread/realtime/push, không phải nguồn dữ liệu của hộp thư. Tab Báo giá trong
nhóm Sample Request phải đọc conversation bằng:

```http
GET /api/v1/internal-mail/conversations?relatedType=SampleRequest&eventGroupCode=quotation
```

Backend lọc các conversation có ít nhất một message active mang
`contentType = SampleRequestPriceQuoteRequested`. Vì vậy người gửi vẫn tìm lại được yêu cầu trong tab Báo giá dù
không nhận notification của chính mình.

API `price-quote-requests` là cổng nghiệp vụ dành riêng cho yêu cầu báo giá. Nó tự resolve Formula hiện tại,
người nhận President và structured payload; FE không dùng API `messages` để tự giả lập loại message này.
Block `priceQuoteRequest.action` dùng action code `SampleRequest.OpenPriceQuote` và cung cấp
`sampleRequestId`, `sampleRequestExternalId`, `productId`, `productCode`, `formulaId`. FE ánh xạ action code sang
route hiện hành; không suy route từ title/body của message.

Yêu cầu đổi dữ liệu kỹ thuật cũng nằm trong conversation này. Message gốc có
`contentType = SampleRequestDataChangeRequest` và proposal trong `dataChangeRequest` của `PayloadJson`.
Hub dùng payload đó để render giá trị cũ/mới và trạng thái từng field; không cần GET detail riêng cho
data-change request. Lab quyết định qua API PLM, không dùng API sửa message chung. Quyết định tạo reply
vào cùng thread; approve mới gọi logic PATCH, reject không thay đổi SampleRequest/Product.

## InternalMail của báo giá

`POST /api/v1/crm/quotations/{quotationId}/request` và
`POST /api/v1/crm/quotations/{quotationId}/mark-sent` là các cổng nghiệp vụ tạo thư nội bộ của báo giá. FE không gọi
API InternalMail chung để tự tạo các thông báo nghiệp vụ này.

Mỗi báo giá có tối đa một conversation active theo:

```text
RelatedType = Quotation
RelatedId = quotationId
RelatedExternalId = quotationExternalId
```

Người xác nhận là `Owner`; Leader và President active cùng công ty là `Member`. Message đầu tiên có
`MessageType = Action`, nội dung chứa customer cùng snapshot sản phẩm/giá của báo giá và payload
`contentType = QuotationSent`. Người gửi có read-state đã đọc, người nhận có read-state chưa đọc.

Mỗi lần gọi endpoint `request`, backend tạo một action message có `contentType = QuotationRequested`, thêm
`InternalMessageReference` trỏ tới báo giá và publish topic `QuotationRequested`. Backend luôn tìm conversation bằng
`CompanyId + RelatedType + RelatedId`, nên gọi nhiều lần chỉ nối thêm message vào đúng mail báo giá, không tạo một
dòng hội thoại mới cho từng yêu cầu. Response trả `conversationId` và `messageId` để FE mở đúng nội dung. Payload
message có `action.code = Quotation.OpenPricingOptions` cùng `quotationId` và `quotationExternalId`; Hub ánh xạ
action nghiệp vụ sang route của client để mở bảng tra cứu giá.

Tin nhắn trả lời trong conversation báo giá vẫn được gửi qua API InternalMail, nhưng publish topic
`QuotationMessageCreated` (`crm.quotation.message.created`, `quotation/message`). Payload notification dùng
camelCase và luôn có `contentType = InternalMailMessage`, `conversationId`, `messageId`, `relatedType`,
`relatedId`, `isUrgent`. Notification phục vụ unread/realtime/push; FE gom nó với conversation theo
`conversationId`, vì vậy Hub chỉ hiển thị một dòng Báo giá.

## Notification Hub presentation

`TopicNotifications` mô tả sự kiện cụ thể. FE lọc bằng `categoryCode` và `eventGroupCode`:

```http
GET /api/v1/notifications/feed?categoryCode=sample_request&eventGroupCode=message
```

Khi feed item có `conversationId`, response bổ sung `conversationTitle` là subject conversation hiện tại sau khi
kiểm tra current employee vẫn là participant active. FE dùng `conversationTitle` làm tên thread trong mọi filter;
`context.aggregateCode` chỉ là mã nghiệp vụ ngắn như `TP_29739`.

Bỏ trống cả hai filter để lấy tất cả. Yêu cầu báo giá trong ngữ cảnh SampleRequest vẫn thuộc
`categoryCode = sample_request`, `eventGroupCode = quotation`; topic cụ thể là `SampleRequestPriceQuoteRequested`.

Topic enum được xem là append-only vì có thể đang lưu dạng số trong DB. Không chèn topic mới vào giữa danh sách.

## Quyền và bảo mật

- Tất cả controller có `[Authorize]`.
- Query luôn lọc `CompanyId` của current user.
- Chỉ participant active mới đọc conversation/message, tránh IDOR qua GUID. Gỡ participant đặt `IsActive = false`, lưu `DeletedAt` và `DeletedByEmployeeId`; message/read-state vẫn giữ audit. Thêm lại cùng employee sẽ kích hoạt lại row cũ và xóa dấu gỡ/archive/mute.
- FE không được truyền `CompanyId`, sender hoặc owner tùy ý.
- Chỉ sender sửa message; sender hoặc owner mới được soft delete.
- Bất kỳ participant active nào cũng có thể thêm participant cùng company; owner, President hoặc Developer có thể gỡ participant. Endpoint thêm không cho tự gán role `Owner`.
- Message đã xóa không trả lại body/payload cho FE.
- Notification không có API public để FE tự tạo; notification được publish như side effect của nghiệp vụ.
- Payload quyết định không nhận giá trị mới từ FE; chỉ nhận field code và đọc proposal từ message gốc.
- `InternalMessage.PayloadJson` là concurrency token để hai Lab không xử lý cùng một trạng thái đồng thời.
- SignalR chỉ gửi `notificationId`, không gửi nội dung đầy đủ.

## Lưu ý triển khai

- Thay đổi này không tạo migration và không thêm bảng/cột.
- API attachment của message trả metadata hiển thị và URL đọc/tải qua InternalMail; endpoint tải file kiểm tra participant và company của conversation cha.
- Reminder/scheduler chưa thuộc v1.
