# InternalMail API và luồng Notification

## Phản hồi khách và thông tin giá của yêu cầu mẫu

Sự kiện giá gốc giữ ghi chú giá/căn cứ riêng. Dispatcher chỉ dùng sự kiện DB đã
lưu đúng công ty/hồ sơ/người ghi active trong whitelist publication để chọn nhóm
Giá; JSON AreaCode hoặc TopicOverride không cấp quyền gửi riêng. Membership Giá
dùng policy hiện có, không kích hoạt lại người đã bị loại; người nhận phải có
quyền approved selling price. Resolver dùng ApplicationPermissionCatalog.ResolveRole
với permission-model/AspNetRoleClaims giống lúc cấp token, giữ fallback theo từng
role chưa migrate. Tạo nhóm qua atomic group writer hiện có.

Sale nhập rõ phản hồi khách được chia sẻ thành companion CustomerPriceFeedbackShared,
RelatedEventId trỏ về sự kiện giá. Chỉ Note companion gửi vào Chung; không chép
PriceReference hoặc tự coi ghi chú giá lịch sử là phản hồi chia sẻ.

Tin giá cũ đã nằm trong Chung được che body/payload/preview/reply ở API với người
không có quyền xem approved selling price. Companion vẫn hiển thị cho Lab. Search
body loại tin giá bị hạn chế trước count/pagination để không dò giá qua kết quả.
Không sửa, di chuyển hay xóa tin cũ. Company/participant/area scope độc lập với
quyền xem giá.


Cảm xúc tin nhắn, API, quyền, realtime không tăng badge và hướng dẫn schema: [REACTIONS](REACTIONS.md).

## API đồng bộ thành viên nhóm mặc định

`POST /api/v1/internal-mail/conversations/{conversationId}/areas/ensure-defaults` (đã có, không cần body) đồng bộ theo một hồ sơ phối mẫu. Truyền ID conversation Chung hoặc nhóm con, không phải ID SampleRequest. Với TP_31054, ID Chung đã khảo sát là `01a190e7-1f19-7b41-a791-f4e9c4626166`.

- Kỹ thuật: LabUser, Leader Lab theo loại sản phẩm và President active cùng công ty.
- Giá: giữ SaleUser/SaleAdmin trong Chung và President active cùng công ty.
- API yêu cầu đăng nhập và quyền đọc conversation cùng company; có khóa/transaction và kiểm tra lại quyền. Tạo nhóm còn thiếu và chỉ thêm membership chưa tồn tại; không kích hoạt lại người đã bị loại, không đổi mute/archive, không mở rộng quyền đọc nguồn forward.
- Response success có `data: { "changed": true }` khi đã lưu thay đổi; `false` khi không cần thay đổi. Gọi lại không tạo thành viên trùng. FE nên tải lại detail/inbox sau đồng bộ có thay đổi.
- Chỉ ảnh hưởng thành viên mặc định và người nhận các tin chat phát sinh sau đó; không tạo tin nhắn, phát lại thông báo cũ, migration hay đồng bộ toàn công ty. Quyền xem observer không tự biến thành membership ngoài rule trên.

Ví dụ gọi bằng API client đã đăng nhập:

```http
POST /api/v1/internal-mail/conversations/01a190e7-1f19-7b41-a791-f4e9c4626166/areas/ensure-defaults
```

Inbox `GET /api/v1/internal-mail/conversations` bổ sung `quickGroups[].unreadCount`, ví dụ `{"code":"technical","conversationId":"<group-id>","unreadCount":2}`. Số đếm dành riêng employee đang đăng nhập, chỉ trả cho nhóm được phép xem; nhóm mặc định chưa tạo có ID null và số đếm 0. Cách tính giữ cùng contract area/detail: max giữa notification unread và message unread lịch sử thiếu audience marker, không cộng hai nguồn. Query theo lô các nhóm của trang, không gọi detail từng hồ sơ hay tạo nhóm. FE dùng số này cho badge Kỹ thuật/Giá; không thay đổi audience, quyền, mute hoặc dữ liệu lịch sử.

Bảng nhóm riêng, code đợt 2 và script triển khai thủ công: [group-storage](design/group-storage/README.md).
Code chuyển nguồn đọc nhóm đã có, mặc định tắt; thứ tự migration/backfill do người vận hành tự chạy và cách bật/rollback: [CUTOVER](design/group-storage/CUTOVER.md).
Quy trình chuyển dữ liệu hiện hành dùng [API đồng bộ + Excel](design/group-storage/API_SYNC.md): một lần gọi mặc định làm mapping + bù membership President Kỹ thuật/Giá còn thiếu; không phụ thuộc Policy. `target=mapping` giữ chế độ chỉ mapping. Không cần trigger/backfill SQL trong Up. Write-through nằm ở DbContext, quyền/badge hiện tại được giữ nguyên.
Entity/EF và bộ đọc mapping đã được bổ sung, nhưng endpoint hiện tại vẫn đọc cấu trúc hội thoại cũ.
Không có startup migration, backfill tự động hoặc query bảng mới trong luồng quyền/badge hiện hành.

## Tạo nhóm riêng không cần tin nhắn

Nút + tại thanh nhóm gọi endpoint `groups/forward` hiện có với `name`, `employeeIds` và `messageIds: []`.
Tên 1–100 ký tự, tổng 2–50 thành viên gồm người tạo; người tạo phải có quyền gửi source (gồm observer Developer đã có capability gửi; President phải là participant active). Với nhóm rỗng, có thể chọn nhân viên active cùng công ty, vẫn theo eligibility sản phẩm hiện hành
và quy tắc ẩn Developer. Không thêm họ vào source, không sao chép tin hoặc file nguồn. Có messageIds thì giữ
điều kiện membership của người forward và kiểm tra nguồn tin/nguồn forward cũ, tối đa 20 tin.
Nhóm rỗng không tạo message và không đẩy thời gian hồ sơ lên đầu: CreatedAt là thời gian tạo thực,
LastMessageAt dùng baseline CreatedAt của hồ sơ gốc đến khi có tin mới. Không đổi quyền Dev/President hay scope company.

`GET /api/v1/internal-mail/conversations/{id}/groups/member-search?keyword=Tùng` chỉ dành cho người có quyền gửi
source của hồ sơ phối mẫu còn active trong công ty. Không có quyền hoặc không đúng loại hồ sơ trả 404.
Keyword 0–100 ký tự sau trim; quá giới hạn trả `[]`. Rỗng trả 30 ứng viên đầu theo họ tên/ID.
Query ILIKE họ tên không phân biệt hoa/thường,
escape wildcard literal, tối đa 30 nhân viên theo họ tên/ID; không tải toàn bộ nhân viên rồi lọc.
Response ví dụ `[{"employeeId":"...","employeeName":"Trần Tùng","role":1}]`: đây là ứng viên, không phải
membership đã lưu; không có số điện thoại/email hoặc quyền đọc nguồn. Developer được ẩn trước LIMIT với người khác.
Không có kết quả trả `[]`. Submit kiểm tra lại company/active/eligibility, không tin ID từ lookup.
FE tìm DB mặc định khi mở và khi gõ từ khóa, debounce 350ms, abort và bỏ response cũ. Kết quả DB không
được dùng cho luồng forward tin cũ. Tìm không dấu chỉ được chuẩn hóa trên danh sách đã tải; DB ILIKE giữ dấu.

## Định tuyến thông báo và tag tên

[NOTIFICATION_ROUTING.md](NOTIFICATION_ROUTING.md) là contract audience Sample Request, mentionedEmployeeIds,
edit mention và badge cần chú ý. Message mới có notificationAudienceVersion=1 lấy badge từ notification unread;
mô tả đối soát message/notification bên dưới áp dụng cho lịch sử thiếu marker. Participant vẫn xem message,
nhưng membership không đồng nghĩa với nhận Notification.

## Phân quyền khu vực (2026-10-04)

### Tải detail và đồng bộ nhóm

Detail gom role Developer/Leader/LabUser của participants và creator trong một SELECT, dùng chung snapshot trong
request cho privacy, xếp Leader và ReaderGroup. Không cache role giữa request hoặc user; giữ quy tắc employee inactive
vẫn ẩn khi có role Developer active, còn Leader/Lab yêu cầu employee active. Parent đã tải dùng lại cho metadata và
DescribeAsync; hồ sơ Sample Request không query owner/can-create để dựng preview nhóm của module khác.

`POST /api/v1/internal-mail/conversations/{id}/areas/ensure-defaults` giữ nguyên kiểm tra quyền, transaction/lock,
đồng bộ default members và không kích hoạt lại người đã bị loại. Success envelope bổ sung `data: { changed: boolean }`,
phản ánh SaveChanges có thay đổi persist hay không. Client cũ bỏ qua data vẫn hoạt động. FE mới chỉ bỏ GET detail
trùng khi BE trả `changed=false`; `changed=true` hoặc response BE cũ thiếu flag vẫn tải lại. Không đổi badge/payload detail,
không tạo message mới và không dùng metadata FE để quyết định có đồng bộ membership hay không.

Inbox và metadata Hub dùng trực tiếp cùng scope `Conversations()` với detail: participant hoặc observer hợp lệ
(Developer trong công ty; President và các role khác phải có membership active).
Không lọc membership lần hai sau khi đã xác thực quyền đọc. Observer chưa có membership mặc định không archive/mute;
không tự tạo membership, không tạo thêm notification. Metadata khách hàng/loại sản phẩm/Sale được resolve theo lô
với cùng company/record scope như các conversation khác, kể cả khi chỉ được xem bằng quyền observer.

### Thời gian hoạt động khi mở nhóm

List inbox, detail và enrichment Hub dùng thời điểm gửi message thực tế. Nhóm con không có message dùng
`CreatedAt` của hồ sơ gốc làm baseline; không dùng thời điểm tạo nhóm, đọc tin, mời thành viên hoặc đổi preference.
Inbox dùng cùng baseline trước OrderBy/Skip/Take và trong DTO, tránh nhóm rỗng chiếm đầu trang do nullable sort.
Ensure-defaults giữ CreatedAt thật để audit, khởi tạo LastMessageAt bằng baseline gốc và không tạo message.
Các nhóm rỗng cũ có timestamp sai được chuẩn hóa ở read-model, không migration/backfill hoặc sửa dữ liệu lịch sử.
Nhóm có message lấy SentAt thực, nên message mới trong nhóm được phép xem vẫn đẩy hồ sơ lên đầu.

### Developer quan sát trong công ty và privacy participant

Capability `internal-mail.company-conversations.observe` tại ApplicationPermissions/RoleSets/PermissionRoleSets là quyền hệ thống gắn riêng với role Developer. CurrentUserPermissionService áp dụng cả token explicit cũ thiếu claim; claim capability đơn lẻ không cấp quyền cho role khác. Catalog và script `seed-company-observer-permission.sql` đồng bộ metadata cấu hình, không cần migration; script không bắt buộc để runtime hoạt động. Thay đổi/thu hồi role có hiệu lực sau refresh/login hoặc khi token cũ hết hạn.

Developer authenticated có company/employee hợp lệ đọc được mọi conversation active trong company, gồm Internal, Sample Request, Quotation và nhóm kỹ thuật/giá/custom có parent active cùng company. List inbox và enrichment Hub dùng cùng scope. Quyền đọc không tạo participant, read-state hay notification mới.

Capability riêng `internal-mail.company-messages.send` gắn riêng với Developer, kể cả token explicit cũ.
Developer gửi vào mọi nhóm active cùng company có parent hợp lệ, không cần membership; `areas[].canSend` và command
cùng dùng quyết định này. President chỉ đọc/gửi khi có membership active, kể cả Sample Request và custom; claim
observer/send cũ không vượt được role gate. Default membership của President trong Kỹ thuật/Giá vẫn được tạo theo
rule hiện hành, không tự kích hoạt lại người đã bị gỡ. Không thay quyền quản lý thành viên của người đã được truy cập.

Developer active cùng company nhận mọi notification mới qua PublishAsync, kể cả nhóm riêng, audience chính rỗng,
UseExactAudience, mute hoặc silent list; bao gồm event do chính Dev phát. Không tự thêm/kích hoạt lại Dev participant.
Thông báo vào inbox unread và outbox in-app/Web Push; dispatch kiểm tra lại role/employee active, company và nhóm/parent
còn active. Không phát lại thông báo lịch sử. Xem thêm Notifications README về recipient vận hành.

Với người xem không phải Developer, BE loại nhân viên có membership role Developer active khỏi detail.participants, GET participants, invite candidates và metadata creator của conversation. Áp dụng mọi loại hội thoại, không dựa vào họ tên/mã nhân viên và không phụ thuộc employee còn active hay không. ReaderGroup được resolve sau khi lọc, nên group count/leader reference không làm lộ người đã ẩn. Số thành viên/gợi ý tag/thanh tên dùng tập đã lọc. Developer thấy đầy đủ participants. Lớp lọc FE cũ vẫn được giữ làm dự phòng tương thích.

Tên tác giả, tên người gửi cuối và nội dung tin nhắn do Developer gửi vẫn hiển thị như tin thường; không ẩn hoặc viết lại nội dung lịch sử. Đây là privacy của danh sách thành viên, không phải cơ chế ẩn danh tác giả hay xóa audit.

API mark-conversation-read dùng quyền đọc thống nhất từ `Conversations()`: chấp nhận observer không có membership
(Developer trong company hoặc participant active của nhóm),
cùng company và đúng khu vực/mốc tin. Không cấp thêm quyền đọc/gửi hoặc quyền quản lý.
Nhánh này chỉ acknowledge notification inbox của chính người gọi; không tạo participant hoặc message read-state mới,
không cập nhật LastReadAt của thành viên khác. Participant hiện có vẫn dùng upsert/read-state như trước.
Snapshot ID dùng để khớp notification chấp nhận capability quan sát toàn công ty sau khi scope đọc đã được xác thực;
không yêu cầu membership lần hai với Developer. Company, nhóm active, nguồn forward và mốc tin vẫn bị giới hạn như cũ.
Điều này tránh lỗi `Conversation was not found` từ bước đánh dấu đã đọc sau khi gửi/tải tin thành công.

[AREAS.md](AREAS.md) là contract nhóm Chung/Kỹ thuật/Giá và nhóm tùy chỉnh: default membership theo role/phạm vi hồ sơ, chỉ Developer quan sát mọi nhóm cùng company; President cần membership active, Dev không tự được thêm vào thành viên, mời thành viên và chuyển tiếp giữ quyền nguồn. Tái sử dụng bảng/cột hiện có; lịch sử giữ ở Chung, không migration/backfill.

## Đồng bộ unread, lưu trữ và xóa tin

Quyền xóa: mapping fallback tại `ApplicationPermissionRoleSets` chỉ gồm Developer; mapping DB ở
`AspNetRoleClaims`. Khi triển khai với token dùng permission DB, chạy script idempotent
`seed-message-delete-permission.sql` trên bảng Identity hiện có rồi login/refresh token.
Token có tập permission tường minh nhưng thiếu capability bị từ chối; role khác vẫn bị từ chối dù được gán claim.
API thành công trả `204` không body; thất bại giữ contract `400` với OperationResult như trước.

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
DELETE message chỉ dành cho Developer có capability `internal-mail.message.delete`, trong company hiện tại,
không yêu cầu là sender/owner/participant. Áp dụng cho mọi loại tin và khu vực trong conversation còn active;
không mở rộng quyền đọc/gửi/sửa hoặc quyền truy cập attachment. Đồng thời archive notification
liên kết đúng conversationId/messageId cho mọi người nhận trong company. Message và inbox state cập nhật nguyên tử;
không xóa cứng audit. Hành vi này áp dụng khi thao tác chạy; không tự backfill dữ liệu production đã xóa từ trước.

## Quyền đọc conversation từ Executive Sample Request Overview

Mặc định mọi API đọc conversation/message/attachment yêu cầu current employee là participant active của
conversation cùng company. Riêng President và ACCUser đã có quyền mở Executive Sample Request Pricing Overview
được đọc conversation có `RelatedType = SampleRequest` trong cùng company, kể cả khi không là participant. Rule
này chỉ dành cho read API (detail, messages, search/context và attachment) để action **Mở trao đổi** của Overview
không trả cờ cho phép sai với quyền backend. Nó không áp dụng cho conversation `Internal` hoặc `Quotation`, không
cho phép gửi/sửa message, và không mở quyền cho role khác. Khu vực riêng dùng membership,
trừ quyền observer riêng Developer trong company theo AREAS.md; President không bypass membership.
Developer có ngoại lệ đọc/gửi toàn công ty theo capability ở mục trên; phần giới hạn read-only của Overview
áp dụng các role còn lại. Quyền xóa của Developer là quyền moderation riêng.

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
| `DELETE` | `/messages/{messageId}` | Chỉ Developer có quyền moderation, cùng company; soft delete, giữ audit |
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

Tối ưu tải (2026-10-04): detail tái sử dụng conversation đã được lọc quyền để dựng `areas`, gộp
unread của khu vực hiện tại vào cùng lần truy vấn notification và chỉ kiểm tra eligibility của
các thành viên trong conversation. `GET /conversations/{id}/participants` dùng query riêng chỉ
lấy thành viên active, giữ company/membership/area/product scope; không tải messages, unread hay
thông tin PLM/báo giá. Route và cấu trúc response giữ nguyên. FE có thể lấy participants ngay
trong response detail để bỏ request riêng khi mở cuộc trao đổi.
Danh sách hiển thị từ detail/participants ưu tiên role Leader active trong cùng company, sau đó theo thời điểm tham gia và employee ID. Thứ tự được xử lý tại BE sau bộ lọc ẩn observer hiện hành; không mở rộng quyền xem thành viên.
Detail và `GET /conversations/{id}/participants` bổ sung `isLeader` (role Leader active) và `readerGroup` nullable `{ groupId, groupType, leaderEmployeeId }`.
Sau bộ lọc visibility/eligibility cuối cùng, BE dùng membership active cùng company của `QAQC.RD` và `QAQC.MAU` để gom người Lab.
Leader Lab (role Leader trong nhóm hoặc membership IsAdmin) giữ mục riêng và đứng trước; những người còn lại nhận metadata nhóm.
Leader tham chiếu chỉ lấy trong tập participant được phép hiển thị; nếu không có thì `leaderEmployeeId = null`. Người ở nhiều nhóm được gán một nhóm ổn định theo GroupId, không đếm lặp.
FE hiển thị `Tên leader… (N)` với N là số thành viên thực sự được gom sau bộ lọc UI, không tính leader đã hiện riêng; khi bấm mở các thành viên đó từ dữ liệu hiện có, không gọi API mới.
Không thay đổi membership, notification audience, bảng/cột hoặc quyền truy cập.
LabUser active cùng company chưa được gán membership RD/Pha màu được gom vào Lab chung (`groupType: "LAB"`, `groupId: "00000000-0000-0000-0000-000000000000"` là khóa hiển thị ảo, không phải group DB). Leader có cả role LabUser và Leader vẫn hiện riêng ở đầu; số người và leader tham chiếu chỉ lấy từ tập visible. Membership RD/Pha màu thật được ưu tiên, không gán trùng vào Lab chung.

Read receipt lọc notification theo conversation tại DB trước khi duyệt batch 200; vẫn giữ parser
payload lịch sử và mốc message để không đánh dấu tin mới ngoài snapshot. Không thay topic,
recipient, payload, SignalR/Web Push; không thêm migration hay sửa dữ liệu hàng loạt.

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
- Chỉ sender sửa message; chỉ Developer có capability `internal-mail.message.delete` được soft delete mọi tin trong company, không cần tham gia hội thoại.
- Bất kỳ participant active nào cũng có thể thêm participant cùng company; owner, President hoặc Developer có thể gỡ participant. Endpoint thêm không cho tự gán role `Owner`.
- Message đã xóa không trả lại body/payload cho FE.
- Notification không có API public để FE tự tạo; notification được publish như side effect của nghiệp vụ.
- Payload quyết định không nhận giá trị mới từ FE; chỉ nhận field code và đọc proposal từ message gốc.
- `InternalMessage.PayloadJson` là concurrency token để hai Lab không xử lý cùng một trạng thái đồng thời.
- SignalR chỉ gửi `notificationId`, không gửi nội dung đầy đủ.

## Cấu hình thành viên nhóm mặc định

Entity/config/DbSet và API version/rollback được mô tả tại [DefaultMembership/README.md](DefaultMembership/README.md). Đây là giai đoạn chuẩn bị; chưa thay resolver hiện hành hoặc đồng bộ thành viên trên DB thật.

## Lưu ý triển khai

- Thay đổi này không tạo migration và không thêm bảng/cột.
- API attachment của message trả metadata hiển thị và URL đọc/tải qua InternalMail; endpoint tải file kiểm tra participant và company của conversation cha.
- Reminder/scheduler chưa thuộc v1.


### Quotation send confirmation action

QuotationSendConfirmation action messages stay in the existing quotation conversation. Body stores the FE reason;
PayloadJson stores comparison and pending/confirmed state. GET messages adds viewer-specific canConfirmSent, true
only with crm.quotation.send.confirm and a matching pending quotation in the current company. Confirmation uses
POST /api/v1/crm/quotations/{quotationId}/confirm-sent, never read/unread or UpdateMessage. These workflow messages
cannot be edited/deleted through ordinary message endpoints. See the quotation README section 12 for FE examples.

Quotation send request payloads may also have status=superseded after an unsent quotation edit. Keep the original
Body/snapshot as history, clear urgency and hide confirmation action. canConfirmSent additionally requires the
message snapshot token to equal the current quotation updatedDate. See quotation README section 12.0.1 for save
response metadata, replacement request IDs and reload events.

canConfirmSent also requires the current comparisonRuleVersion (1). Legacy requests remain visible as history
but require a status-90 quotation save to replace their snapshot. The button is a permission/token hint;
confirm-sent authoritatively rechecks effective approved pricing and full comparison snapshot, so expiry or a
source change can still reject confirmation and require reload/save. New comparison metadata is documented in
quotation README section 12.
