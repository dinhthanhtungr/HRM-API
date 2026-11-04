# Cấu trúc nhóm riêng — chuẩn bị đợt 2

**Quy trình hiện hành:** [API_SYNC.md](API_SYNC.md), migration chỉ tạo schema, API bổ sung mapping và trả Excel; live writer ghi mapping atomic, không cần trigger. Số liệu/SQL trigger bên dưới là hồ sơ khảo sát và phương án cũ, không phải hướng dẫn chạy hiện tại.

Trạng thái: khảo sát SELECT và code đợt 2 đã làm; script đã được kiểm thử bằng dữ liệu giả lập trên cluster
PostgreSQL 17 cô lập. **Task chưa tạo bảng/index/trigger hoặc backfill trên DB đang dùng**.
Không phải migration đang được đăng ký; không tự áp dụng khi khởi động API.
Chi tiết code, thử nghiệm và runbook hiện hành: [PHASE2.md](PHASE2.md).
Code chuyển nguồn đọc đã sẵn sàng, mặc định tắt: [CUTOVER.md](CUTOVER.md). Chỉ bật sau khi người vận hành tự áp dụng và kiểm chứng schema, backfill và bridge.

## Nguồn và giới hạn khảo sát

Snapshot cuối lúc **31/10/2026 13:55:31 Asia/Bangkok** (06:55:31 UTC), PostgreSQL 17.
Dùng cấu hình local API: appsettings, Development, User Secrets khi Development, launch profile Project đầu tiên,
rồi environment của tiến trình khảo sát. Không xuất connection string/host/tài khoản/nội dung tin.
Chưa xác minh override command-line hoặc environment riêng của tiến trình API đang chạy trong Visual Studio.
Số liệu chỉ đại diện DB được resolve từ cấu hình trên; không suy rộng sang DB khác hoặc production.

Audit dùng Npgsql, `REPEATABLE READ`, `SET TRANSACTION READ ONLY`, statement timeout 15s, lock timeout 1s,
deadline 150s; kết thúc bằng ROLLBACK. Không gọi API đọc tin/mark-read, không chạy handler có side effect.
Source và kết quả sanitized cục bộ: `artifacts/group-storage-audit/`; không chứa credentials.
Kết quả snapshot để xem cùng hồ sơ: [audit-snapshot.json](audit-snapshot.json).

| Dữ liệu | Số lượng |
|---|---:|
| Hội thoại phối mẫu (type 5) | 835 |
| Hội thoại báo giá (type 12) | 662 |
| Nhóm Kỹ thuật (17) | 88 |
| Nhóm Giá (18) | 88 |
| Nhóm Custom (19) | 0 |
| Tổng hội thoại | 1.673 |
| Company có hội thoại | 1 |
| Hồ sơ phối mẫu chưa có Kỹ thuật / Giá active | 747 / 747 |
| Nhóm con rỗng Kỹ thuật / Giá | 87 / 88 |

Tất cả 176 nhóm con đều thuộc root phối mẫu, cùng company; không có parent thiếu, tự trỏ, parent là nhóm con,
active child dưới inactive parent, trùng default active hoặc trùng default lịch sử. Không có conversation type 20.
Không có thành viên lệch company, reply chéo hội thoại hoặc LastMessageId trỏ tin ở hội thoại khác.
Chưa có bảng `InternalConversationGroups`; conversation chưa có FK cho RelatedId, chưa có unique index
chống trùng nhóm mặc định; không có user trigger trên bảng conversation ở snapshot này.
Tên constraint/index thực tế khác một số tên EF config: script không dựa vào tên PK suy đoán từ EF.
Conversation không bật RLS và không có constraint chưa validate trong snapshot; tài khoản khảo sát có quyền ghi
nhưng transaction đã ép READ ONLY. Quyền của tài khoản triển khai/runtime khác vẫn phải được xác minh riêng.

Baseline **phối mẫu + các nhóm con** (1.011 hội thoại, không gồm 662 báo giá):

| Dữ liệu cần giữ | Số lượng |
|---|---:|
| Membership / active | 18.507 / 18.507 |
| Membership muted / archived | 8.809 / 0 |
| Tin nhắn / tin soft-delete | 2.718 / 3 |
| File gắn tin | 9 |
| Message read-state / chưa đọc thô | 57.538 / 33.273 |
| Notification liên kết phạm vi này | 2.641 |
| Notification user-state / unread chưa archive thô | 47.213 / 5.223 |

Toàn DB có 4.199 notification chứa conversationId, không có liên kết unresolved theo logic join company + ID
hiện hành. Không có reference forward-source type 20; **không thể kết luận luồng forward đã được kiểm thử trên dữ liệu thật**.
Các số unread trên là bản ghi thô, **không phải tổng badge UI** (badge còn rule audience/deleted/membership/mute/observer).
Custom/inactive/forward chưa có dữ liệu đại diện nên bắt buộc có fixture trên DB bản sao.

## Schema chốt để duyệt

`InternalMail.InternalConversationGroups`:

| Cột | Quy tắc |
|---|---|
| ConversationId uuid | PK, FK giữ nguyên ID hội thoại hiện có |
| CompanyId uuid | FK ghép với ConversationId, không cho gắn khác company |
| RootConversationId uuid | FK ghép đến mapping General cùng company |
| GroupType smallint | 0 General, 1 Technical, 2 Pricing, 3 Custom; độc lập enum RelatedType cũ |
| DisplayName varchar(100), nullable | General/Technical/Pricing null = nhãn i18n mặc định; Custom bắt buộc tên |
| IsActive boolean | Bản chiếu phục vụ unique default active; nguồn lifecycle vẫn là conversation.IsActive |

- General tự trỏ RootConversationId = ConversationId. Nhóm con trỏ General; không có nhóm lồng nhóm.
- ConversationId, CompanyId, RootConversationId, GroupType bất biến sau tạo. Đổi tên chỉ sửa DisplayName qua BE có quyền.
- Unique partial `(CompanyId, RootConversationId, GroupType)` cho **Technical/Pricing active**.
  Cho phép nhiều Custom, và nhiều phiên bản default inactive lịch sử; không merge/xóa nhóm cũ.
- Composite FK cần unique index mới `(CompanyId, InternalConversationId)` trên conversation.
- FK DELETE RESTRICT, không cascade vào tin/thành viên. Mapping có self-FK, nên xử lý root/child có chủ đích.
- Không thêm CreatedAt/CreatedBy/LastMessageAt/Owner vào bảng mới: lấy nguồn gốc từ bảng hiện tại, tránh hai giá trị cạnh tranh.
- IsActive không do FE gửi độc lập: trigger đồng bộ từ conversation; trigger mapping chặn cập nhật trái trạng thái.

**Phạm vi mapping:** tạo 1.673 dòng theo snapshot cuối: 1.497 General (835 phối mẫu + 662 báo giá),
88 Technical, 88 Pricing. Mapping General của báo giá chỉ mô tả hội thoại sẵn có, không tạo nhóm con, quyền hay tin mới.
Chọn mapping cả root báo giá để không tạo lỗ hổng cho use case CreateArea cũ vốn có thể dùng root ngoài phối mẫu.
Không sinh 747 cặp nhóm mặc định còn thiếu: giữ lazy initialization hiện tại.
DB vẫn đang được sử dụng: giữa snapshot đầu và cuối có thêm một hồ sơ/tin nhắn do hoạt động bên ngoài audit.
Số liệu trên dùng cùng snapshot cuối; script không hard-code số dòng và phải chạy lại preflight trước triển khai.

## Tương thích và nguồn sự thật

`02-expand-backfill.review.sql` tạo bảng/mapping và trigger cầu nối legacy trong một transaction có khóa ghi conversation
và khóa bảo vệ các bảng lịch sử. DDL đã được test trên cluster giả lập cô lập; chưa chạy trên DB nghiệp vụ hoặc clone
dữ liệu thật. Script mặc định fail-closed bằng cờ duyệt theo session; cờ này chống chạy nhầm, không thay PostgreSQL grants.
Không đưa vào thư mục migrations tự động chạy. Script từ chối nếu bảng đã tồn tại, không drop/recreate/overwrite dữ liệu;
lần chạy lỗi rollback toàn transaction, lần chạy thành công không được chạy lại mù quáng.

Trigger legacy ánh xạ INSERT conversation mới, đồng bộ IsActive và Subject của Custom; giữ nguyên root Subject là tiêu đề
hồ sơ. Không phát notification. Legacy root/type bị sửa lệch mapping thì từ chối thay vì âm thầm đổi nhóm.
BE tương thích không được vừa tự INSERT mapping vừa để trigger INSERT trùng: trong pha cầu nối, tạo conversation để trigger
đăng ký mapping, sau đó cập nhật tên nếu cần trong cùng transaction. Lock root theo pattern hiện tại; unique index chặn race.
Group read-model mới dùng bảng mới làm nguồn cấu trúc; lifecycle vẫn đọc conversation. Không chọn nguồn tùy từng endpoint.

Giữ nguyên RelatedType/RelatedId/Subject cũ trong pha tương thích: root vẫn trỏ hồ sơ nghiệp vụ, child vẫn có field legacy.
API cũ vẫn nhận/trả IDs, areaCode, areas, groupConversationId và RelatedType như trước; adapter server map loại mới ra contract cũ.
Không sửa enum persisted và notification payload cũ. Không đổi URL/deep link.
Chức năng đổi tên không nằm trong đợt này; chưa cho dùng DisplayName mới khi FE/BE cũ vẫn đang chạy.

## Trình tự triển khai sau khi được duyệt

1. Xác minh target thực tế và override của tiến trình phục vụ, backup có diễn tập restore; chạy lại preflight vì dữ liệu đang sống.
2. Trên DB clone: chạy draft được duyệt, đối chiếu hash tất cả bảng được bảo vệ; chạy fixture Custom, inactive,
   duplicate defaults, cross-company, orphan, sai root, tạo đồng thời, soft-delete rồi tạo lại và rollback.
3. Chuẩn bị resolver cấu trúc nhóm chung, chuyển access/metadata/default groups/forward/participants/notification feed sang resolver.
   Giữ capability/membership/audience, visibility Developer và mapping Lab/Sale nguyên trạng.
4. Trong maintenance đã chặn writer: rerun preflight; chạy expand/backfill. Trigger bridge hỗ trợ writer cũ sau khi mở lại.
5. Triển khai app đối chiếu old/new nhưng chưa đổi nguồn đọc; không gọi ensure/mark-read chỉ để đối chiếu.
6. Chuyển nguồn đọc bằng cấu hình khi ID/order/quyền/badge khớp; theo dõi lỗi, unique violation và latency.
7. Chỉ bỏ bridge/field legacy bằng đợt duyệt riêng sau khi mọi writer/reader đã chuyển. Không DROP field cũ ở đợt đầu.

## Điều kiện nghiệm thu và rollback

- Cùng snapshot, kiểm tra **tập ID và thứ tự**, không chỉ count: list/detail/messages/attachments/search/context,
  areas, quick groups, @Tôi/reply/tag, filter unread/urgent/archive, cursor và metadata báo giá/phối mẫu.
- Ma trận President/Developer/Lab/Sale/PLPU và cross-company; Dev ẩn trong members/tag/count nhưng tác giả tin vẫn hiện.
- Mỗi nhóm có read-state riêng; mở Technical không đọc Pricing; không hồi sinh member đã bị loại.
- Người mời ngoài nguồn chỉ đọc nhóm mới; forward cũ vẫn kiểm tra toàn audience của mọi source, không copy action capability.
- Nhóm rỗng/đọc/mute/rename không đổi activity hoặc đẩy hồ sơ lên đầu. Chỉ tin mới thật được đổi thứ tự.
- Hash/count bảo toàn conversation, participant, message, file-link, reply/read-state, reference, notification/user-state.
  Badge cuối cùng cần đối chiếu handler thực tế với principal/capability thật; hash không chứng minh rule quyền đã đúng.
- Đo DB/HTTP/render riêng, cùng snapshot và quyền; ít nhất 5 mẫu sau warm-up; không load-test hệ thống đang phục vụ.

Rollback ưu tiên **đổi nguồn đọc về legacy**, không xóa bảng mới. Mọi nhóm mới vẫn phải có legacy field đến khi hết cửa sổ rollback.
Nếu đã sửa DisplayName mà không dual-write tương thích, hoặc writer mới đã bỏ legacy, rollback app cũ không còn an toàn:
phải xử lý trước khi bật tính năng đó. DDL rollback chưa cung cấp vì chỉ có rollback ứng dụng là cần thiết ở pha đầu;
drop bảng có thể làm mất tên nhóm mới. Không dùng CASCADE hoặc sửa các bảng tin/thành viên để rollback.

## Hồ sơ bàn giao

- `01-preflight.readonly.sql`: audit SELECT, bao READ ONLY + ROLLBACK; không đổi schema/dữ liệu.
- `02-expand-backfill.review.sql`: DDL/backfill dự kiến, mặc định bị khóa, **chưa được thực thi**.
- `03-verify.readonly.sql`: kiểm tra mapping/count sau chuyển; chỉ chạy khi bảng đã tồn tại trên clone/đích được duyệt.
- `04-runtime-grants.review.sql`: bước tùy chọn nếu deployer và runtime dùng PostgreSQL role khác nhau;
  cấp SELECT/INSERT/UPDATE trên bảng mới cho role tường minh, không cấp PUBLIC hoặc đổi role nghiệp vụ.
- Đợt 2 bổ sung Domain/EF/reader và test. API contract, luồng quyền/badge và FE chưa chuyển nguồn dữ liệu.

Đã có test integration migration/rollback/race trên cluster giả lập; chưa diễn tập trên clone dữ liệu thật, chưa
đo HTTP/render hoặc benchmark trên hệ thống đang phục vụ. Không xem fixture thay cho bước xác minh target/backup.

Khảo sát ban đầu chỉ có parser PostgreSQL; đợt 2 đã bổ sung kiểm thử constraint/trigger/backfill thực trên PostgreSQL
cô lập và API build. Kết quả và giới hạn được ghi trong PHASE2.md. Không có GRANT PUBLIC hoặc SECURITY DEFINER;
cần chốt tài khoản triển khai/runtime và quyền tối thiểu trên clone. Nếu conversation đang có RLS, draft dừng để thiết kế
policy tương ứng, không tự tạo bảng không-policy rồi mở rộng dữ liệu.
