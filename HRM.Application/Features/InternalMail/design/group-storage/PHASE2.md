# Đợt 2 — code và script nhóm riêng

**Hồ sơ phương án cũ dùng trigger.** Triển khai hiện hành dùng [API_SYNC.md](API_SYNC.md): migration tạo schema, API đồng bộ trả Excel và write-through bằng code. Không dùng SQL backfill/bridge ở tài liệu này cho deployment mới.

## Phạm vi đã làm

- Domain: `InternalConversationGroup`, enum `InternalConversationGroupType : short` (0 General, 1 Technical, 2 Pricing, 3 Custom).
- EF: `InternalConversationGroupConfiguration`, DbSet trên ApplicationDbContext và IInternalMailDbContext.
  Sáu field, compound FK cùng company, self-FK tới General root, delete Restrict, unique partial cho default active.
  Bảng được `ExcludeFromMigrations`; schema/trigger/backfill do script thủ công quản lý, không có EF migration class mới.
- Application: `InternalConversationGroupReader` scoped nội bộ và `InternalConversationGroupInfo`, chỉ phục vụ đối chiếu sau expansion.
  Không có endpoint/worker/handler hiện tại gọi reader mới; DI registration không tạo query.
- SQL: preflight chỉ đọc, expand/backfill có guard, verify chỉ đọc và grant tùy chọn cho PostgreSQL runtime role.

Endpoint list/detail/messages/send/forward/participants/mentions/feed/summary vẫn dùng resolver cũ, capability và
membership cũ. Không đổi DTO, topic, payload, recipients hoặc outbox publisher. Không có feature flag tự bật bảng mới
khi phát hiện tồn tại; không có fallback lặng lẽ che mất mapping thiếu. Không thêm query schema vào mỗi request.
FE không đổi trong đợt này. Tính năng đổi tên/chuyển Owner/đóng nhóm và cutover thuộc đợt sau.

## Contract của reader nội bộ

`ReadAsync(conversationIds, ct)` chỉ nhận tối đa 200 ID, bỏ ID rỗng và trùng, trả kết quả theo ConversationId.
Anonymous/thiếu company/employee hoặc input rỗng trả `[]` trước khi truy cập DbSet mới.
Mọi row phải thuộc cùng company, conversation còn active và **được phép đọc bởi InternalMailAreaAccessService.Conversations()**.
Mapping không cấp thêm quyền; observer vẫn theo capability cũ, không tạo membership/read-state.
Root mapping phải General, tự trỏ, cùng company và có root conversation active. Mapping sai cấu trúc bị loại.
Root ID có thể được trả cho thành viên nhóm con không có quyền đọc nguồn; đây không phải grant hay nội dung nguồn.

Output chỉ gồm ConversationId, RootConversationId, GroupType, DisplayName. Null DisplayName là nhãn i18n mặc định;
không phải tiêu đề hồ sơ bị mất. Không trả EF entity hoặc member/name của người bị ẩn.
Reader được gọi trước khi schema được triển khai sẽ báo lỗi DB như một thao tác đối chiếu sai thời điểm;
không bắt lỗi rồi giả vờ kết quả mới khớp legacy. Các endpoint hiện tại không gọi nên DB cũ vẫn dùng được.

## Quy tắc writer trong pha cầu nối

1. Code cũ INSERT conversation với ID/legacy type/root hiện hành trong transaction hiện tại.
2. `TR_SyncLegacyConversationGroup` INSERT đúng một mapping trong cùng transaction.
3. Code cũ thêm participant/tin nếu có, rồi commit như hiện tại. Lỗi mapping làm rollback thao tác gốc.

**Không gọi `InternalConversationGroups.Add(...)` từ command/handler ở pha này**; trigger là writer đăng ký cấu trúc.
Không thêm navigation vào InternalConversation để tránh vô tình kéo mapping vào các projection/query cũ.
Khi dùng DbSet mới để đối chiếu, dùng AsNoTracking; không attach nó rồi SaveChanges để đồng bộ trạng thái.
IsActive vẫn thuộc conversation; mapping chỉ phản chiếu để enforce unique default active. Đổi root/type/company bị từ chối.
Subject của Custom được bridge đồng bộ thành tên nhóm; Subject General vẫn là tiêu đề hồ sơ, không thay nhãn tab.

## Script và thứ tự áp dụng khi được duyệt

**Chưa áp dụng bất kỳ bước ghi nào dưới đây lên DB đang dùng.**

1. Xác minh connection/runtime overrides, quyền operator, backup và restore; chặn mọi writer/workers.
2. Chạy `01-preflight.readonly.sql`, xử lý riêng mọi duplicate/orphan/company/name bất thường.
3. Dùng client dừng ngay khi lỗi; trong session riêng, người triển khai đặt cờ `hrm.group_storage_review_approved` thành `yes`
   sau khi được duyệt, rồi chạy `02-expand-backfill.review.sql`. Không gắn cờ vào cấu hình/connection pool của API.
4. Script tạo unique index, bảng, validators, backfill root trước child, bridge. Không sinh nhóm còn thiếu.
   Fingerprint/count **10 bảng** trước/sau phải khớp: conversation, participant, message, read-state, reference,
   attachment-link, notification, user-state, recipient và outbox. Có sai lệch hoặc lỗi DDL thì rollback cả transaction.
5. Nếu deployer và runtime khác PostgreSQL role: chỉ định role runtime có sẵn qua `hrm.group_storage_runtime_role`,
   chạy `04-runtime-grants.review.sql` trong session đã duyệt. Role này là tài khoản kết nối PostgreSQL, **không phải**
   role President/Developer của ứng dụng. Không cấp PUBLIC/DELETE hoặc thay quyền trên bảng cũ. Bridge SECURITY INVOKER
   cần runtime có SELECT/INSERT/UPDATE trên bảng mới, ngoài quyền cũ sẵn có trên schema/conversation.
6. Chạy `03-verify.readonly.sql` bằng deployer **và tài khoản runtime thực tế**; check mapping mismatch/root/name và grants.
7. Khôi phục writer; API vẫn đọc cấu trúc cũ. Lập bước đối chiếu/cutover riêng, không bật từ script.

Cờ session chỉ là chống chạy nhầm; không phải cơ chế phân quyền. Script one-time từ chối nếu mapping table đã tồn tại;
không drop/recreate/ghi đè để chạy lại. Nếu thất bại, transaction rollback rồi mới điều tra và chạy lại khi được duyệt.
Nếu triển khai thành công nhưng app mới có lỗi, rollback app vẫn đọc legacy; giữ bảng/mapping để không mất dữ liệu.
Không có DDL down phá hủy tên nhóm hoặc cascade lịch sử. Trước khi bỏ bridge/legacy cần đợt duyệt mới.

## Kiểm thử an toàn

Test fixture tự `initdb` và `pg_ctl` thành cluster mới, chỉ bind `127.0.0.1` ở cổng trống, database test cố định và
data directory mới dưới `artifacts/group-storage-phase2-tests/<run-id>/`. Không đọc appsettings/User Secrets,
không dùng bất kỳ connection-string environment của API. Trước mọi kết nối ghi, test so sánh data_directory,
database và current_user với cluster vừa tạo; không khớp thì dừng trước DDL/reset.
Kết thúc fixture dừng đúng cluster đó, giữ log/dữ liệu giả lập trong artifacts, không xóa thư mục đệ quy.

Windows tự tìm PostgreSQL 17 trong Program Files; máy khác chỉ định thư mục binaries bằng `HRM_TEST_POSTGRES_BIN`.
Không có binaries thì các integration test được báo skipped rõ ràng, không chuyển sang DB API làm fallback.
Test uses citext giống mapping legacy. Không chạy API hoặc background worker để kiểm thử migration.

```powershell
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --filter "FullyQualifiedName~GroupStorage|FullyQualifiedName~MappingReader|FullyQualifiedName~GroupEfModel"
```

Coverage gồm: bảo toàn full rows/mute/archive/removed member/timestamps/mention/reply/file/forward-source 20;
giữ query unread cũ; DB chưa có bảng vẫn đọc legacy; grant guard; rollback giữa transaction và giữa DDL;
multiple Custom và tên Unicode; duplicate active/inactive defaults; orphan/nested/cross-company; immutable structure;
soft-close/recreate/reactivate collision; cạnh tranh hai transaction với commit/rollback; runtime role khác deployer.
Reader có ma trận President/Developer/Lab/Sale/PLPU, member/observer/nonmember, anonymous, company/root/lifecycle sai.
EF test kiểm tra schema, provider type, FK/index, không shadow columns, query cũ không join bảng mới và manual exclusion.

Kết quả chạy thực tế và lỗi ngoài phạm vi: [PHASE2_VERIFICATION.md](PHASE2_VERIFICATION.md).
Chưa có clone dữ liệu thật, chưa chạy kiểm thử UI nhiều tài khoản hoặc đo tốc độ HTTP/render; không xem cluster giả lập
là bằng chứng đã sẵn sàng cutover production. Không thay đổi capability/role claims trong đợt này.
