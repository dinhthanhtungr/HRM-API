# InternalMail API và luồng Notification

## Server-Timing của thao tác đọc (Development)

Middleware hiện có đo thêm `POST /api/v1/internal-mail/conversations/{id}/read` trong
Development, chỉ trả header cho request authenticated thành công (200 hoặc 204). Không
thêm flag/config, không nới authorization, không ghi thêm dữ liệu hoặc log ID/SQL/payload.
Các môi trường khác không bật timing POST này. Chạy lại API sau build để nạp code mới;
build ra artifacts không cập nhật tiến trình đang chạy.

Trong Network → Timing/Headers của request read:

| Metric | Ý nghĩa |
|---|---|
| `nm_read_write_total` | Tổng handler ghi đọc, gồm scope, snapshot và commit |
| `nm_read_access`, `nm_read_membership`, `nm_read_boundary` | Kiểm tra quyền, membership, mốc tin đã tải |
| `nm_read_inbox_scope` | Chọn ID inbox chưa đọc và kiểm tra quyền theo đúng tập hội thoại ứng viên |
| `nm_read_event_snapshot`, `nm_read_legacy_check`, `nm_read_message_snapshot` | Chụp sự kiện/tin và xác định nhánh tương thích |
| `nm_read_begin`, `nm_read_messages`, `nm_read_participant`, `nm_read_commit` | Mở transaction, upsert read-state, cập nhật LastReadAt, commit |
| `nm_read_notifications` | Tổng đối soát notification |
| `nm_read_inbox_update`, `nm_read_inbox_legacy` | Cập nhật tập chuẩn và toàn bộ nhánh batch/JSON lịch sử |
| `nm_read_state_total` | Tổng lấy trạng thái trả về khi returnState=true |
| `nm_read_state_access`, `nm_read_state_groups` | Kiểm tra lại quyền và lấy nhóm được xem |
| `nm_read_state_counts`, `nm_read_state_latest` | Tính lại số chưa đọc, lấy mốc tin mới nhất |
| `app_pre_dispatch`, `app_total` | Thời gian pipeline trước dispatch và đến lúc bắt đầu trả header |

Tên metric và duration là whitelist, không có ID/role/giá/thông tin nghiệp vụ. Bước không
chạy có thể không có metric. Total và bước con lồng nhau không được cộng chồng; các số đo
EF gồm dịch query/network/materialization, không phải thời gian PostgreSQL thuần. Không
đổi SQL hoặc thứ tự thao tác để thêm timing. Các GET đã có nm_handler giữ nguyên ý nghĩa;
write/state có tổng riêng để hai handler chạy nối tiếp không ghi đè thời gian của nhau.

## Đọc hội thoại có lịch sử dài

Receipt chọn ID notification chưa đọc/chưa archived của employee/company và đúng conversation
trước khi kiểm tra quyền. Tập ứng viên không phải quyền: vẫn qua ConversationsForCandidates,
visibility của reaction và liên kết JSON hiện hành. Snapshot ID đã được xác thực chỉ dùng trong
request, dùng chung cho chọn message, sự kiện thread-only, cập nhật canonical và legacy.
Không giữ query quyền toàn inbox trong các bước ghi. Query message/boundary giới hạn đúng
conversation nhưng vẫn kiểm tra area/group và quyền như Messages(). Tin đến sau snapshot,
nhóm chưa mở và message sau boundary không bị đánh dấu. Giao dịch và response không đổi.

Nhánh cập nhật inbox giữ scope quyền/company/employee; mỗi tập ID canonical hoặc legacy
đã giới hạn đúng conversation nên không lặp thêm phép nối JSON conversation ở outer scope.
Khi snapshot đầu request không có liên kết message kiểu cũ, bỏ hoàn toàn vòng truy vấn legacy;
liên kết đến sau snapshot không bị kéo vào thao tác đọc đang chạy. API và transaction không đổi.

`POST conversations/{id}/read?throughMessageId=...&returnState=true` giữ nguyên contract,
quyền company/participant/observer và mốc SentAt/ID. Snapshot xử lý gồm tin cần tạo/sửa
read-state hoặc còn notification chưa đọc cần đối soát; không chuyển lại toàn bộ ID
tin đã đọc không còn việc cần xử lý. Upsert bỏ qua read-state đã đọc và giữ ReadAt cũ.
Notification có MessageId UUID chuẩn cập nhật theo tập hợp trong một lệnh, vẫn xử lý
mọi delivery trùng của cùng tin. Payload lịch sử có GUID không chuẩn tiếp tục đi parser
và batch cũ; không tự coi JSON thiếu/sai MessageId là sự kiện cấp thread hợp lệ.
Thread-only pricing approval dùng đúng snapshot lúc bắt đầu; observer không được tạo
membership/read-state. Tin ngoài snapshot, sai area/company và nhóm chưa mở giữ nguyên.
Số đếm trả về vẫn theo policy hiện hành, không thay bằng số dòng notification vừa cập nhật.

Regression `ReadLargeHistoryPreservesBoundaryAndOtherGroups` đo xen kẽ 5 mẫu sau warm-up
trên PostgreSQL loopback tạo mới, 20.000 tin, 800/8.000 notification và bốn role; đối chiếu
toàn bộ inbox state với handler tham chiếu trước tối ưu. Đây là phép đo handler với helper
persistence chung, không phải HTTP/DB production hoặc thời gian toàn màn hình.

## Badge danh sách và xác nhận đọc

`GetInternalConversations` và compact read-state dùng `PageNotificationUnreadCountsAsync`
cho đúng các ID đã được phân quyền trong trang/nhóm liên quan. Nhánh notification
và legacy được tính riêng rồi cộng theo conversation, không gộp thành một query UNION
lớn. Liên kết payload conversation dùng UUID có kiểm tra định dạng thay vì cast khóa
conversation sang text; giữ nguyên company, employee, active, participant/observer,
loại trừ reaction và quy tắc BBG đếm distinct message. Không thêm index/schema/cache.

Benchmark `QuotationUnreadApisExecuteWithRealMetadataOnIsolatedPostgres` so ID/số đếm
và đo luân phiên 5 lần sau warm-up trên cluster loopback tạo mới, không đọc appsettings
hay DB thật; kết quả từng role ở `artifacts/quotation-unread-performance/*-page-badges.json`.
Đây là số đo bước badge của fixture, không phải thời gian HTTP trên máy chủ triển khai.

## Icon sự kiện trên danh sách hội thoại

Icon Hòm thư biểu thị loại notification cuối cùng trong feed đã tải, không dò ngược lịch sử để ưu tiên sự kiện nghiệp vụ cũ hơn chat. `GetInternalConversations` không gọi query `icons`; hai field optional `representativeTopicCode`/`representativeEventGroupCode` được giữ để tương thích nhưng trả null. Dòng chỉ có conversation chưa có notification trong feed dùng icon loại hội thoại ở FE. Badge, quyền, thứ tự và phân trang giữ nguyên; không thêm cache/index/schema.

Đo trước thay đổi nghiệp vụ ngày 12/11/2026: query icon riêng từng tốn khoảng 212 ms warm ở trang Báo giá đại diện; bước đó hiện đã bỏ khỏi handler. Đây không phải phép đo HTTP sau thay đổi hoặc xác nhận tiến trình API đã nạp mã mới.

## Đo từng bước Notification Hub items

`GET /api/v1/notification-hub/items` trả `Server-Timing` cho response 200 đã xác thực theo cùng cơ chế feed/conversations. `nm_hub_feed` bao gồm các phase feed lồng bên trong (`nm_feed_scope`, `nm_feed_page`, `nm_titles`...); `nm_hub_conversations`, `nm_hub_badges`, `nm_hub_groups`, `nm_hub_parents`, `nm_hub_samples`, `nm_hub_quotations`, `nm_hub_quick_groups`, `nm_hub_masking` tách phần enrichment. `nm_handler` là tổng handler ngoài cùng; không cộng nó hoặc `nm_hub_feed` với các phase con. Header/log chỉ chứa tên phase cố định và thời lượng, không chứa ID, nội dung, bộ lọc hoặc SQL. Không thêm API call FE, cache hay thay đổi body/cursor.

Query chọn sự kiện mới nhất gom ngày lớn nhất theo hội thoại, sau đó gom UUID lớn nhất trong các dòng hòa ngày (UUID canonical dạng text), thay cho probe `NOT EXISTS` trên lịch sử cho từng dòng. Giữ inbox người nhận/company/archived, phạm vi visible ở ngoài, standalone, loại business/message và thứ tự lọc category/event/cursor hiện hành.

Đối chiếu ngày 12/11/2026 trên cùng transaction repeatable-read chỉ đọc, tài khoản đại diện với role từ DB và cấu hình GroupStorage/Reactions hiện hành, 30 dòng/trang: median 3 lượt warm query phối mẫu Tất cả trang 1 **2757 → 767 ms**, trang 2 **2735 → 764 ms**; lọc `quotation` trang 1 **1239 → 721 ms**, trang 2 **1213 → 696 ms**. Hai bản trả cùng ID/topic/ngày/thứ tự ở cả bốn trang. Đây là query EF, không phải HTTP/browser hoặc phiên user trong ảnh. Handler mới warm đo riêng khoảng 1,10 giây (Tất cả) và 1,03 giây (`quotation`); không dùng các số này để khẳng định tiến trình HTTP đã nạp code. Thử truyền ID trang sâu vào query badge cho kết quả đúng nhưng chậm hơn (khoảng 80 → 133–138 ms), nên không áp dụng thay đổi đó. Không thêm index/schema/cache.

## Phạm vi truy vấn hội thoại chưa đọc

Notification Hub dùng các bước đọc riêng trong `InternalMailAreaAccessService.UnreadKeys.cs`
để lấy khóa unread, standalone và sự kiện đại diện. Các tập suppression, standalone và
lịch sử cạnh tranh được giới hạn 4.096 ứng viên mỗi tập; gặp phần tử thứ 4.097 thì quay
về truy vấn SQL đầy đủ, không cắt lịch sử hay trang kết quả. Winner vẫn xét full inbox,
ngày rồi UUID; kiểm quyền ở truy vấn trả dữ liệu cuối cùng được giữ nguyên.
Nhánh Chưa đọc thông thường xác định khóa unread trước rồi giới hạn các tập quyền
phụ thuộc vào đúng các hội thoại đó; tập trên 4.096 khóa vẫn dùng scope đầy đủ. Các
khóa chỉ là ứng viên, không cấp quyền; standalone và winner xét đầy đủ lịch sử như cũ.

`PageNotificationUnreadCountsAsync` trong `InternalMailAreaAccessService.UnreadCounts.cs`
tách đếm notification và fallback legacy, rồi cộng theo các ID của trang được phép xem.
Legacy chỉ tính message không có notification state của chính employee, kể cả state
đã đọc/archive vẫn chặn fallback. Hub tái dùng `ConversationUnreadCount` khác null mà
feed đã tính cho quotation, bao gồm giá trị 0; các hội thoại còn lại vẫn được đếm.
Trong một lần tải unread, kết quả legacy đã kiểm quyền được dùng lại cho badge feed,
và page scope vẫn được kiểm tra. Không lưu kết quả qua request, đổi quyền hay phiên đăng nhập.
Các đường detail/summary dùng truy vấn hiện hành; body, thứ tự, cursor và quyền không đổi.

`ConversationsForCandidates` giao các tập phân vùng/quyền theo conversation ID với cùng
tập ứng viên trước khi nối dữ liệu. Các gate company/active/member/observer/research/R&D
vẫn được đánh giá trong SQL; đây không phải cache quyền. Feed cá nhân truyền tập event
đầy đủ đã giới hạn vào các truy vấn JSON link để tránh mở rộng lịch sử ngoài tập đó.
Quick groups dùng lại count canonical có sẵn của trang; chỉ các nhóm còn thiếu mới được
đếm qua đường page-batched, với cùng notification/legacy và quyền hiện hành.

Với group storage canonical, `GetInternalConversationsQueryHandler` truyền relatedType đang lọc vào truy vấn khóa
chưa đọc. Predicate root/child giống bộ lọc của inbox; không mất Kỹ thuật/Giá/nhóm riêng
của đúng loại hồ sơ. Không truyền loại thì giữ hành vi mọi loại. Unread vẫn xét trước
COUNT/phân trang và giữ kiểm tra company/membership/observer/legacy hiện hành. Không
thay đổi DTO, thứ tự, số chưa đọc, icon hoặc quick groups. Kết quả và đo paired baseline
được mô tả tại [Notifications](../Notifications/UNREAD_PERFORMANCE.md).

`QuickGroupsAsync` lấy ID nhóm được phép xem trong các root của trang một lần cho mỗi
lời gọi, rồi tái dùng trong query root và nhóm con. Scope company/active/membership,
observer, phân vùng và mapping vẫn chạy trong SQL trước khi đọc ID. Tập ID không được
cache qua request; lần gọi sau kiểm tra quyền lại. Nhóm con đã có nhưng không được xem
vẫn chặn preview tạo thay thế, và số chưa đọc tiếp tục lấy từ nguồn đếm hiện hành.
Các tập quyền phụ thuộc được giới hạn bằng đầy đủ ID nhóm của root đang đọc; quá
4.096 nhóm thì dùng truy vấn đầy đủ. Metadata màu chỉ xét root của trang. Đường
Notification Hub truyền ID hội thoại vào `InboxConversations` để giữ cùng bộ lọc
quyền trên tập của trang; caller không truyền ID vẫn dùng hành vi hiện hành.

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

### Sản phẩm liên quan trong hội thoại Báo giá

`GET /api/v1/internal-mail/conversations/{conversationId}/related-products` yêu cầu đăng nhập và quyền đọc chính hội thoại Báo giá cùng company. Nguồn không tồn tại, không còn active, không phải Báo giá hoặc không được phép đọc trả `404`.

Response chỉ là dữ liệu điều hướng, không gồm giá/cost/payload:

```json
[{ "productId": "<id>", "productCode": "P001", "productName": "Tên sản phẩm đã lưu trên BBG",
   "conversations": [{ "conversationId": "<general-conversation-id>", "sampleRequestId": "<id>", "requestCode": "TP_001" }] }]
```

Mỗi sản phẩm xuất hiện một lần theo thứ tự dòng báo giá (SortOrder rồi ID); code/name lấy snapshot của dòng đầu tiên. Chỉ xét dòng còn active, sản phẩm cùng company và báo giá active; sản phẩm về sau inactive vẫn giữ snapshot của BBG lịch sử. `conversations` là các hội thoại **Chung** hiện có, được phép đọc, của yêu cầu mẫu active cùng company và đúng ProductId:

- Khi dòng BBG có `SampleRequestId`, chỉ dùng liên kết tường minh đó. Liên kết sai, thiếu dữ liệu hoặc bị thu hồi quyền không được thay bằng yêu cầu mẫu khác.
- Khi `SampleRequestId` của dòng là `null` (BBG chỉ lưu sản phẩm), liệt kê **tất cả** yêu cầu mẫu đúng ProductId có hội thoại Chung được phép đọc; không tự chọn yêu cầu mới nhất. Không đối chiếu bằng mã/tên, không giới hạn theo khách hàng BBG vì đây là liên kết sản phẩm và quyền của từng hội thoại vẫn được kiểm tra riêng.

Nhiều dòng trùng sản phẩm gộp các đích hợp lệ của từng dòng, loại trùng và sắp theo requestCode/ID; nếu có cả dòng liên kết và dòng null thì đích fallback của dòng null vẫn được gộp. Không tự tạo nhóm. Danh sách sản phẩm rỗng nghĩa là không có dòng hợp lệ; `conversations: []` nghĩa là chưa có đích hợp lệ hoặc không có quyền xem, không tiết lộ ID nhóm bị ẩn. GET không đổi unread, membership, giá hoặc phát notification.

`POST /api/v1/internal-mail/conversations/{conversationId}/areas/ensure-defaults` (đã có, không cần body) đồng bộ theo một hồ sơ phối mẫu. Truyền ID conversation Chung hoặc nhóm con, không phải ID SampleRequest. Với TP_31054, ID Chung đã khảo sát là `01a190e7-1f19-7b41-a791-f4e9c4626166`.

- Kỹ thuật: LabUser, member/leader team Lab theo loại sản phẩm và President active cùng công ty.
- Giá: giữ SaleUser/SaleAdmin trong Chung và President active cùng công ty.
- API yêu cầu đăng nhập và quyền đọc conversation cùng company; có khóa/transaction và kiểm tra lại quyền. Tạo nhóm còn thiếu và chỉ thêm membership chưa tồn tại; không kích hoạt lại người đã bị loại, không đổi mute/archive, không mở rộng quyền đọc nguồn forward.
- Response success có `data: { "changed": true }` khi đã lưu thay đổi; `false` khi không cần thay đổi. Gọi lại không tạo thành viên trùng. FE nên tải lại detail/inbox sau đồng bộ có thay đổi.
- Chỉ ảnh hưởng thành viên mặc định và người nhận các tin chat phát sinh sau đó; không tạo tin nhắn, phát lại thông báo cũ, migration hay đồng bộ toàn công ty. Quyền xem observer không tự biến thành membership ngoài rule trên.

Ví dụ gọi bằng API client đã đăng nhập:

```http
POST /api/v1/internal-mail/conversations/01a190e7-1f19-7b41-a791-f4e9c4626166/areas/ensure-defaults
```

Inbox `GET /api/v1/internal-mail/conversations` bổ sung `quickGroups[].unreadCount`, ví dụ `{"code":"technical","conversationId":"<group-id>","unreadCount":2}`. Số đếm dành riêng employee đang đăng nhập, chỉ trả cho nhóm được phép xem; nhóm mặc định chưa tạo có ID null và số đếm 0. Cách tính dùng chung với area/detail và summary: notification unread cộng message lịch sử đủ điều kiện chưa có notification state của employee; loại trùng trước khi cộng. Query theo lô các nhóm của trang, không gọi detail từng hồ sơ hay tạo nhóm. FE dùng số này cho badge Kỹ thuật/Giá; không thay đổi audience hay dữ liệu lịch sử.

Bảng nhóm riêng, code đợt 2 và script triển khai thủ công: [group-storage](design/group-storage/README.md).
Code chuyển nguồn đọc nhóm đã có, mặc định tắt; thứ tự migration/backfill do người vận hành tự chạy và cách bật/rollback: [CUTOVER](design/group-storage/CUTOVER.md).
Quy trình chuyển dữ liệu hiện hành dùng [API đồng bộ + Excel](design/group-storage/API_SYNC.md): một lần gọi mặc định làm mapping + bù membership President Kỹ thuật/Giá còn thiếu; không phụ thuộc Policy. `target=mapping` giữ chế độ chỉ mapping. Không cần trigger/backfill SQL trong Up. Write-through nằm ở DbContext, quyền/badge hiện tại được giữ nguyên.
Entity/EF và bộ đọc mapping đã được bổ sung, nhưng endpoint hiện tại vẫn đọc cấu trúc hội thoại cũ.
Không có startup migration, backfill tự động hoặc query bảng mới trong luồng quyền/badge hiện hành.

## Tạo nhóm riêng không cần tin nhắn

### Nhóm riêng R&D mặc định, tạo khi gửi tin đầu tiên

`GET conversations/{rootId}/participants?areaCode=rd` xem trước danh sách @ từ thành viên R&D active cùng công ty, sau khi kiểm tra quyền đọc root phối mẫu và quyền dùng nhóm R&D. Không tạo nhóm, participant hay notification khi xem trước; `JoinedAt` chưa có membership dùng giá trị mặc định DTO. FE dùng danh sách này trước tin đầu tiên và gửi `mentionedEmployeeIds` cùng bản nháp; gửi tin vẫn kiểm tra lại team hiện hành trong transaction tạo nhóm.

Với tin Chung mới, Watcher hệ thống thuộc đúng team nhận tin được chuyển thành Member, bỏ mute mặc định trong cùng transaction gửi; mute của Member có sẵn không bị đổi. Đồng bộ người còn thiếu dùng khóa company/conversation hiện có để tránh thêm participant trùng khi hai người gửi đồng thời. Luồng gửi nghiệp vụ phối mẫu cũng chuyển Watcher của team sản phẩm, không chuyển Watcher của team khác.

Hòm thư của member/leader R&D và Màu mặc định lọc phối mẫu theo `SampleRequestLabTeamRules` trước count/phân trang, gồm cả nhóm con được phép đọc. Có từ khóa tìm kiếm thì bỏ bộ lọc loại sản phẩm, giữ nguyên company/authorization. Thành viên active R&D/Màu được đọc nhóm Chung của phối mẫu cùng công ty qua tìm kiếm hoặc ID, kể cả chưa có participant; quyền này không tự cấp quyền gửi/quản lý hay đọc nhóm riêng/Giá. Feed dùng cùng bộ lọc mặc định, không sửa read-state hoặc badge tổng. Gửi tin Chung mới resolve toàn team Lab theo sản phẩm; hồ sơ cũ chỉ bổ sung người nhận chưa từng có participant, không tự khôi phục người đã bị gỡ.

- Với hồ sơ phối mẫu được phép đọc, thành viên active của nhóm nhân viên `QAQC.RD` cùng công ty thấy sẵn tab R&D. Developer dùng capability quan sát hiện có, không tự thêm vào danh sách thành viên hay cấp quyền mới. President và các observer phòng ban khác không có ngoại lệ đọc nhóm này.
- Tab ảo trả về trong `areas` với `code: "rd"`, `conversationId: null`, `canSend: true`. Chỉ xem tab không tạo nhóm, participant, message hay notification. FE mở ô nhập tin đầu tiên; gửi thành công chuyển sang lịch sử chat bình thường.
- Gửi vào `POST /api/v1/internal-mail/conversations/{rootId}/messages` với `areaCode: "rd"` dùng transaction và khóa company → root hiện có. Nhóm Custom, thành viên R&D hiện hành và tin đầu tiên được commit cùng nhau; lỗi gửi rollback, hai yêu cầu đồng thời dùng chung nhóm. Không backfill 25k hồ sơ, không thêm enum, constraint, bảng/cột hay migration.
- Nhóm được nhận diện bằng marker server `internal-mail:sample-request:rd:v1` trong `RelatedExternalId` hiện có, không dựa vào tên R&D. Nhóm người dùng tự đặt cùng tên vẫn theo quyền Custom cũ; forward tạo nhóm khác không sao chép marker này.
- Thành viên được đồng bộ từ nhân viên active trong team R&D khi gửi tin, chỉ trong nhóm đang gửi và cùng transaction; mở xem không ghi membership. Thành viên R&D mới được đọc/gửi theo team hiện hành. Quyền đọc, eligibility và delivery luôn kiểm tra lại team: rời R&D mất quyền kể cả participant cũ còn active. Không thêm người ngoài R&D qua invite; danh sách thành viên loại người không còn eligible.
- Notification của tin nhắn và unread tiếp tục theo luồng chat riêng; không phát thông báo chỉ vì tab ảo xuất hiện. Developer giữ cơ chế observer/delivery/privacy sẵn có. Attachment, tìm kiếm và truy cập bằng ID dùng chung scope BE, không chỉ ẩn tab ở FE.

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

Thread messages và message context trả thêm `senderSampleWorkAudience: "Sale" | "Lab" | null` cho
tin thuộc yêu cầu mẫu. Đây chỉ là gợi ý phía nghiệp vụ để FE mở Theo dõi mẫu theo người gửi khi người
xem là Ban giám đốc/Developer; không trả danh sách role và không cấp quyền mở hồ sơ.
API bổ sung metadata theo batch sau kiểm tra company, conversation/area và che thông tin giá.
Sự kiện workflow gắn đúng message, conversation, hồ sơ và người ghi được ưu tiên để phân biệt tài khoản
kiêm Sale/Lab. Tin không có sự kiện dùng role nghiệp vụ không mâu thuẫn của sender active cùng company;
thiếu hoặc mâu thuẫn dữ kiện trả null. Không đọc body/JSON người dùng để đoán phía gửi, không sửa lịch sử,
không thêm người nhận/thông báo/realtime và không thêm bảng/cột.
`sampleWorkTrackingEligible` dùng ngày tạo của hồ sơ cha đã kiểm tra company, trả true khi thuộc
nhóm Tất cả mẫu (`CreatedDate >= 01/11/2026`), false cho hồ sơ cũ và null cho tin không xác định được
hồ sơ mẫu. FE mở chi tiết hồ sơ khi false/unknown, tránh điều hướng tới danh sách trống; không suy từ ngày gửi tin.

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

Inbox trả thêm `representativeTopicCode` và `representativeEventGroupCode` cho icon: sự kiện nghiệp vụ mới nhất liên kết chính xác với message chưa xóa trong conversation/area được phép đọc. Chỉ trả mã catalog, không trả payload hoặc mở quyền đọc inbox của người khác. Chat/reaction không thay thế icon nghiệp vụ; không có metadata thì FE dùng icon loại hồ sơ. Query theo lô trang hiện tại, không thêm HTTP request từng dòng.

Quyền xóa: mapping fallback tại `ApplicationPermissionRoleSets` chỉ gồm Developer; mapping DB ở
`AspNetRoleClaims`. Khi triển khai với token dùng permission DB, chạy script idempotent
`seed-message-delete-permission.sql` trên bảng Identity hiện có rồi login/refresh token.
Token có tập permission tường minh nhưng thiếu capability bị từ chối; role khác vẫn bị từ chối dù được gán claim.
API thành công trả `204` không body; thất bại giữ contract `400` với OperationResult như trước.

`GET /api/v1/internal-mail/conversations/{id}` trả `unreadCount` của employee hiện tại, cùng quy tắc với list:
lấy số inbox notification chưa đọc/chưa archive cộng phần message lịch sử chưa có notification state của chính employee.
Message lịch sử phải chưa xóa, khác người gửi, chưa có audience marker, có read-state chưa đọc và thuộc inbox chưa archive.
Người xem im lặng/mute không nhận fallback ở nhóm Chung; nhóm riêng vẫn giữ quy tắc unread khi mute.
Notification state đã đọc hoặc archived cũng chặn fallback của cùng message. Hai tập được loại trùng trước khi cộng;
list, detail, quick groups và tổng dùng cùng quy tắc. Notification được đếm trên toàn bộ dữ liệu,
không phụ thuộc trang feed FE đã tải; conversation và participant phải active. Vì vậy notification cũ thiếu
message read-state hoặc chỉ liên kết thread vẫn hiện badge trên thẻ. Đây là query đọc, không tự đánh dấu đã đọc/backfill.
Ví dụ sau khi đọc: `{ "conversationId": "...", "unreadCount": 0, "lastMessageId": "..." }`.
Số 0 là không còn tin chưa đọc tại thời điểm query; tin đến sau request có thể làm số này tăng lại.
`POST /api/v1/internal-mail/conversations/{id}/read` giữ mặc định `204`; caller mới có thể gửi
`returnState=true` cùng `areaCode`/`throughMessageId` để nhận `200` gồm `conversationId`, `areaCode`,
`throughMessageId`, `unreadCount`, `lastMessageId`, `lastMessageAt`, `areaUnreadCounts`.
Query trạng thái kiểm tra lại quyền sau mutation, chỉ đếm các sibling được phép xem và không tải participants,
capabilities hoặc metadata mẫu/báo giá. Thu hồi quyền trong lúc xử lý trả `404`, không dùng snapshot để vượt quyền.
FE có snapshot detail hợp lệ chỉ vá unread vào snapshot; giữ preview/identity tin, tên và quyền tab, không đưa
ID tin mới từ receipt vào preview cũ. BE cũ trả `204` hoặc caller chưa có snapshot tiếp tục đọc detail sau mutation.

Detail dùng số unread thống nhất từ `DescribeAsync`; bước projection ban đầu bỏ phép đếm read-state lịch sử
vốn sẽ bị ghi đè ngay sau đó. Helper projection giữ mặc định legacy cho các consumer chưa đi qua bước đối soát này;
không đổi field, quyền hay nội dung response.

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
| `GET` | `/conversations/{conversationId}/messages` | Lấy thread có phân trang, trang đầu hỗ trợ `includeUnreadBadges=true` |
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
- `unreadOnly`: chỉ lấy conversation có message hoặc inbox notification chưa đọc; lọc bằng kiểm tra tồn tại trước phân trang, không GROUP BY/COUNT để quyết định có unread. Số badge chính xác được đếm sau khi có ID trang hiện tại và truyền các ID đó vào nguồn query. Company/membership/observer và điều kiện fallback lịch sử giữ cùng quy tắc với số đếm.

Với `unreadOnly`, handler tách lấy DISTINCT conversation keys đủ điều kiện ra khỏi câu SQL đếm tổng không phân trang. COUNT và lấy trang sau đó vẫn áp dụng đầy đủ outer company/role/group scope với tập key này; không trả key ra client, không bỏ kiểm tra quyền hoặc giả số tổng. Không có key thì trả trang rỗng sớm. Metadata icon join trực tiếp message được phép xem → link đúng conversation/message → notification cùng company, chỉ lấy ID/thời điểm/topic thuộc catalog nghiệp vụ; không join recipient state, không DISTINCT hoặc kéo body/payload. Metadata của message được phép xem không mở quyền inbox hay thay đổi unread. Các phase `unread-keys`, `total-count`, `page` ghi warning nếu mất từ 1.000 ms hoặc lỗi; log chỉ có phase, thời gian, số dòng hoặc loại exception, không SQL/parameter/ID/payload và không tự retry/tăng timeout.
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

Ở môi trường Development, GET chi tiết hội thoại và GET `/messages` có
`Server-Timing` cho kiểm quyền, tải trang và tính badge (`nm_thread_access`,
`nm_thread_page`, `nm_thread_badges`). So sánh với thời gian chờ phản hồi trong
Network và thời điểm nội dung hiện trong Performance để tách thời gian API,
truyền dữ liệu và hiển thị. Chỉ phản hồi GET 200 đã xác thực có header này.

Khi Hub yêu cầu `includeUnreadBadges=true` ở trang đầu, từng tin nhắn trả
`unreadBadges` với mã dùng để khử trùng và nhóm badge liên quan. Metadata chỉ
tính các khoản chưa đọc của nhân viên hiện tại, trong phạm vi công ty và luồng
được phép đọc. Dữ liệu dùng cùng phạm vi đếm với `unread-summary` cho notification
liên kết, tin nội bộ cũ, mention/reply và reaction. FE dùng khoản giảm này để cập
nhật badge trong lúc gửi xác nhận đọc, rồi thay bằng số tổng chính thức khi các
yêu cầu ghi kết thúc. Các lượt đọc trang sau và caller cũ giữ contract hiện tại.

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
Khi đọc thread với `throughMessageId`, API chụp ID notification duyệt giá đã tồn tại của đúng employee/company/conversation
trước khi acknowledge. Notification trong tập này được xác nhận ngay cả khi CreatedDate lệch về tương lai so với đồng hồ API.
Notification đến sau tập chụp vẫn chưa đọc, kể cả CreatedDate bị ghi lùi. Client cũ không gửi mốc message giữ giới hạn giờ request.
Payload phải thiếu `messageId` hoặc có giá trị null; ID hỏng hoặc ID ngoài snapshot không được bỏ qua.
Các topic khác vẫn yêu cầu liên kết message hợp lệ. Không thêm recipient hoặc phát lại notification.

Mở lại thread vẫn đối chiếu các message đã đọc trước đó để sửa notification còn sót, kể cả notification không
nằm trong trang feed FE đang tải. Không cần migration, không phát lại SignalR/Web Push. Sau thành công, FE tải lại
unread-summary và inbox; không suy số tổng từ feed phân trang. Đây không phải thao tác đọc tất cả các conversation.

## Reply Và Attachment

Luồng video dùng chung attachment, upload có thể tiếp tục, enum media, API phát, worker FFmpeg,
realtime và hướng dẫn cấu hình/release: [Đính kèm ảnh/video](media/README.md).
`thumbnailUrl` vẫn chỉ dành cho ảnh; video dùng `posterUrl`, `videoInfoUrl`, `videoRetryUrl` và `playbackUrl`.

Mỗi item từ API messages và message context trả thêm `replyTo` cùng `attachments`. `replyTo` là preview tối đa 300 ký tự của message gốc, người gửi, loại message và trạng thái xóa; nó có `null` nếu message không phải reply. Khi message gốc đã xóa, `replyTo.isDeleted = true` và `bodyPreview` rỗng để không trả lại nội dung đã xóa.

Mỗi attachment trả `attachmentId`, `fileName`, `sizeBytes`, `contentType`, `kind`, `isImage`, `contentUrl`, `thumbnailUrl` và `downloadUrl`. Với ảnh, FE dùng `thumbnailUrl` để render danh sách/grid và chỉ dùng `contentUrl` khi người dùng mở ảnh gốc. File không phải ảnh có `thumbnailUrl = null`. Không dùng storage path nội bộ.

Thumbnail được tạo WebP tối đa 480x480, giữ tỉ lệ và lưu trong storage cạnh file gốc. Ảnh mới tạo thumbnail ngay lúc upload; ảnh lịch sử được tạo và cache ở lần gọi thumbnail đầu tiên. Endpoint thumbnail vẫn kiểm tra company và participant active như endpoint file gốc, không public storage path và không cần migration.

Bộ sinh ảnh thu nhỏ dùng chung với Gallery PLM chỉ lấy khung đầu của ảnh động,
không phóng lớn ảnh nhỏ và kiểm tra giới hạn 40 triệu pixel trước khi decode với
stream seekable. Ảnh gốc và thumbnail đã lưu giữ nguyên; quy tắc này áp dụng khi
sinh thumbnail mới. Không thay quyền xem file, route hoặc nội dung tin nhắn.

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

### Đo hai API Báo giá chưa đọc

`UnreadConversationIdsAsync` (17/11/2026) là đường lấy khóa của bộ lọc `conversations?unreadOnly=true` khi bật group storage. Tách own unread notification/badge policy → DISTINCT conversation ID trong SQL → đầy đủ `Conversations()` và membership (hoặc company observer), rồi hợp với tin legacy theo đúng mute/area/ownership và chống trùng liên kết. Reaction bị loại từ đầu như query cũ, không đổi chính sách actor/tác giả của inbox cá nhân. Handler vẫn áp dụng scope inbox, bộ lọc và sort trước phân trang như cũ. Không giới hạn số unread, không cache hoặc thay count; chỉ giữ khóa hội thoại trong bộ nhớ, không tải toàn bộ ID notification lịch sử ra Application. `LegacyUnreadCountsByConversationAsync` dùng chung bước legacy với summary để tránh hai chính sách fallback khác nhau. Nhánh storage cũ giữ query cũ; không tự bật/tắt config. Xem số đo và giới hạn tại `../Notifications/UNREAD_PERFORMANCE.md`.

Fixture PostgreSQL cô lập với dữ liệu giả, 5 mẫu warm cho Developer/LabUser/SaleUser: conversations giảm từ 2.374–2.401 ms xuống 173–181 ms; feed giảm từ 261–262 ms xuống 127–129 ms. Đây là thời gian handler trên fixture, không phải HTTP server thật. Bottleneck chính của conversations là query icon nhân bản theo recipient rồi loại trùng; feed giới hạn candidate conversation thuộc Báo giá trước khi chọn sự kiện mới nhất, vẫn xét toàn bộ sự kiện trong mỗi thread để không làm hiện lại thông báo cũ. Test đối chiếu ID/thứ tự/nội dung feed, icon, unread và scope role trước/sau.

Danh sách conversations ở mọi bộ lọc ghi một warning tổng khi từ 1.000 ms hoặc lỗi,
với tên nhánh như `sample-conversations-all`, `sample-conversations-unread`,
`quotation-conversations-all` và `quotation-conversations-unread`. Các phase gồm
total-count, page, icons, badges, group-info, parents, sample-info, quotation-info,
quick-groups và masking; unread-keys chỉ chạy khi lọc chưa đọc. Feed Báo giá chưa đọc
tiếp tục có timing riêng. Không log SQL, parameter, ID hoặc nội dung; không tăng
timeout, tự retry hay đổi DB. Dùng warning này để phân biệt truy vấn chậm với thời
gian HTTP/mạng khi triển khai.

### Xem thời gian ngay trong Network

GET `/api/v1/internal-mail/conversations` và GET `/api/v1/notifications/feed` trả
header `Server-Timing` cho người đã đăng nhập khi response HTTP 200, cả Development
và môi trường triển khai. Không đổi body, query, quyền hoặc CORS. Đây chỉ là tên
metric cố định và số mili giây, không có SQL, ID, keyword hay nội dung nghiệp vụ.

- `app_pre_dispatch`: từ middleware đo thời gian đến sau CORS/authentication/
  authorization, trước khi dispatch vào MVC; không gọi đây là thời gian auth thuần.
- `nm_handler`: thời gian đoạn xử lý nghiệp vụ được đo trong Application.
- `nm_total_count`, `nm_page`, `nm_badges`, `nm_sample_info`,
  `nm_quick_groups`, v.v.: các phase thật sự đã chạy. Feed dùng `nm_feed_page`,
  `nm_titles`, `nm_conversation_metadata`, v.v. Không cộng các tổng lồng nhau.
- `app_total`: từ lúc vào middleware đến lúc bắt đầu gửi response headers, gồm
  downstream pipeline và serialization trước headers. Không gồm thời gian chờ trước
  middleware ở proxy/server, truyền qua mạng, tải hết body hoặc render FE.

Trong DevTools mở Network → chọn đúng request → Timing hoặc Response Headers →
`Server-Timing`. Nếu Network chờ lâu nhưng `app_total` ngắn, kiểm tra phần ngoài
đoạn pipeline này; nếu `nm_handler` dài, dùng các phase để khoanh query. Header
không tự xác nhận nguyên nhân mạng/DB, và cần triển khai bản BE này mới xuất hiện.

Danh sách dùng lại số chưa đọc đã tính của các hội thoại trong trang để hiển thị
nhóm nhanh Kỹ thuật/Giá. Bao gồm cả số 0 đã biết; nhóm chưa nằm trong trang vẫn
được đếm riêng theo scope hiện hành. Chỉ dùng lại trong một request, không cache
giữa người dùng/lần tải và không bỏ kiểm tra quyền nhóm. Không giả số badge về 0.

Fixture SampleRequest dùng đúng handler danh sách, metadata thật và nhóm nhanh,
kiểm tra cả GroupStorage bật/tắt trên PostgreSQL cô lập. Với 600 hội thoại và 4.800
sự kiện giả, chế độ nhóm mới mất khoảng 0,34–0,47 giây; tăng lịch sử lên 38.400 sự
kiện cho LabUser lên khoảng 1,60 giây. Đây là số đo handler trên fixture, không phải
thời gian HTTP server thật. Chi phí tập trung ở icon và badge, không phải tên khách/
Sale/category. Chưa dùng số đo này để khẳng định nguyên nhân mọi request 4 giây.

Thử nghiệm đổi phép nối chuỗi UUID thành phép nối UUID có guard trên fixture lịch sử
lớn giữ được link/count ở ca đối chiếu nhưng làm full handler timeout ở bước icon;
đã loại, không áp dụng vào SQL production. Không suy ra cách tối ưu từ kiểu cột mà
chưa kiểm chứng toàn query và dữ liệu trả về.

### Thử nghiệm icon nghiệp vụ trước khi đổi sang notification cuối cùng

Đoạn này lưu kết quả thử nghiệm cũ; handler danh sách hiện không chạy query icon riêng.
Query icon cũ chọn toàn bộ bản ghi scalar chiến thắng trong mỗi nhóm, để EF sinh
`ROW_NUMBER` thay cho lấy riêng Topic bằng scalar `First` tương quan. Vẫn chọn
CreatedDate mới nhất, hòa thời gian thì ID lớn nhất; chỉ lấy ID/thời gian/topic/
conversation ID. Nguồn dữ liệu vẫn kiểm tra quyền, company, message chưa xóa và
đúng cặp conversation/message; không thêm recipient-state hoặc bỏ lịch sử.

Phép đo xen kẽ 5 mẫu trên PostgreSQL cô lập, 38.400 sự kiện giả ở GroupStorage bật,
50 dòng: query icon scalar cũ median 748,54 ms, bản xếp hạng 335,20 ms, cùng icon
cho từng hội thoại. Đây là thời gian query fixture, không phải cam kết thời gian
HTTP server thật. Handler hiện tại không còn xuất phase `nm_icons`.

Tin chưa đọc trong DTO messages/context: chỉ `IsRead=false` khi tồn tại ReadState của employee hiện tại với IsRead=false. Không có ReadState không đồng nghĩa chưa đọc (ví dụ người xem im lặng hoặc tin lịch sử). Không tạo ReadState khi đọc query; giữ nguyên scope/quyền.

### Badge BBG theo tin hiển thị — 13/11/2026

- BBG và nhóm con của BBG chỉ tính notification unread liên kết đúng InternalMessage còn tồn tại, chưa xóa, trong đúng hội thoại và không phải tin do employee hiện tại gửi. Nhiều notification cùng tin chỉ tính một lần; đại diện summary là notification unread mới nhất theo CreatedDate/Id, bỏ reaction như trước.
- Sự kiện cấp thread như QuotationPricingApproved thiếu MessageId không cộng badge chat, tổng chuông, category/event-group và không tự làm hội thoại lọt vào Chưa đọc. Không xóa notification, đổi recipient hay tự ghi trạng thái đọc khi query. Cách phát thông báo và lịch sử vẫn giữ nguyên.
- Quyền company/membership/observer và fallback tin lịch sử giữ nguyên. Module khác và cách chọn sự kiện/icon đại diện không đổi.
- SQL badge đếm distinct message cho BBG. Query suppression dùng window theo message, trả ID của các notification không được tính; summary và khóa unread cùng sử dụng policy. Không migration hoặc sửa dữ liệu lịch sử.
- Feed bổ sung `conversationUnreadCount` nullable cho hội thoại BBG trong trang và được phép xem. FE phải nhận cả 0; khi có list/detail thì dùng `conversation.unreadCount`, không lấy Math.max với số notification ẩn. Chỉ đọc metadata trang hiện tại, không gọi API riêng từng thẻ.
- GET unread-count dùng cùng tổng với unread-summary. Tổng toàn hệ thống vẫn do BE tính, không cộng từ một trang FE.
- Test `QuotationBadgesCountOnlyDistinctVisibleMessages` dùng PostgreSQL cô lập, đủ President/Developer/LabUser/SaleUser và legacy/canonical storage. Bao gồm 1 tin với nhiều thông báo, thread-only, sai liên kết, deleted/own, module khác, feed và count/summary/category/keys trước sau đọc. SQL được đối chiếu với policy LINQ tham chiếu; notification ẩn vẫn còn nguyên.
