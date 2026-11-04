# Chuyển đổi bảng nhóm InternalMail

Đọc khi sửa cấu trúc nhóm, API đồng bộ/Excel, backfill/mapping, GroupStorage flag, write-through, tương thích trigger cũ hoặc dọn code chuyển đổi. Không áp dụng cho mọi thay đổi Notification thông thường.

## Quy trình hiện hành

- Đọc `HRM.Application/Features/InternalMail/design/group-storage/API_SYNC.md`. Người dùng chọn schema-only migration + API đồng bộ trả Excel + write-through bằng code, không dùng trigger mới. Không nhúng/chạy các fragment SQL bridge lịch sử hoặc tự sửa migration/DB nếu chưa được cho phép.
- API tạm `POST /api/v1/internal-mail/group-storage/synchronize` cần `[Authorize]`, capability kỹ thuật và company/employee từ principal. Không nhận tenant/IDs từ caller. Không có quyền thì từ chối trước query/export. Không cấp rộng quyền chat qua capability chuyển đổi.
- API mặc định bổ sung mapping và bù membership President Kỹ thuật/Giá còn thiếu theo API_SYNC.md; target=mapping giữ chế độ chỉ mapping. Không tự tạo nhóm mặc định, không thêm Chung/Custom, không kích hoạt lại inactive; giữ eligibility RD/màu và quyền nguồn forward. Không sửa conversation/message/notification/read-state/outbox hoặc preferences membership đã có. Chạy lại không tạo trùng; mapping mâu thuẫn chặn cả đợt. Company lock + transaction dùng chung với live writer. Render Excel trước commit; không báo Completed khi chưa commit. Không xác nhận rollback nếu mất phản hồi tại commit; trả trạng thái Unknown và cho chạy lại an toàn.
- Mỗi request hợp lệ tạo báo cáo Excel mới, gồm RunId, kết quả, nhóm mới/đã có/lỗi, mã hồ sơ/màu, số tin chưa xóa/đã xóa và sheet Bù President với EmployeeId/trạng thái/lý do. Không xuất secret, SQL, body tin hoặc tên nhân viên. Không có quyền thì không có file; lỗi export/network/cancellation không thể bảo đảm client nhận file, phải cho retry an toàn.
- `ApplicationDbContext.GroupWrites.cs` và `InternalConversationGroupStore` là writer thường trực: tạo/cập nhật nhóm atomic cùng hội thoại, hỗ trợ outer transaction/savepoint. Không bypass qua raw SQL/ExecuteUpdate cho structural writes mà không tích hợp cùng cơ chế. Giữ lock order company trước conversation row. Message-only update không kích hoạt backfill.

## Marker và dọn code

- Dùng `GROUP-STORAGE-TRANSITION: <vai trò>` tại đúng phần tạm: `synchronization-api`, `switch`, `legacy-adapter`, `mirror-validation`, `legacy-write-shape`, `legacy-enum-values`, `legacy-membership-defaults`, `comparison-reader`, `audit-only`, `backfill-bridge`, `schema-readiness`, `legacy-bridge-detection`.
- Mỗi phần mới phục vụ chuyển đổi có marker, lý do/điều kiện dọn tại `HRM.Application/Features/InternalMail/design/group-storage/TRANSITION_CLEANUP.md`.
- Khi gỡ API phải rà controller/command/report/interface/renderer/DI, capability/catalog/fallback/seed và test của API. **Không xóa shared store/live writer, entity/DbSet, mapped reader, quyền/capability chat, membership, read-state hoặc badge.**
- Chỉ bỏ schema-readiness/legacy-trigger compatibility khi mọi DB/phiên bản đang triển khai đã qua giai đoạn đó. Không tự drop trigger cũ; operator cần review trạng thái DB. Không gỡ writer khi chỉ mới bật cờ đọc true.
- Enum 17/18/19 và mirror trong entity/config có marker riêng; không gắn obsolete cho toàn entity hoặc đổi số enum. Resolver default/preview còn chạy có marker legacy-membership-defaults, chỉ thay khi Policy đã được nối và kiểm chứng, không xóa theo đợt bù President. Xem inventory TRANSITION_CLEANUP.md.
- Chỉ bỏ legacy shape/mirror RelatedType/RelatedId sau khi mọi consumer/writer đã dùng cấu trúc canonical. RelatedType/RelatedId của hồ sơ gốc vẫn là business reference; không renumber/xóa enum lịch sử hoặc forward-source 20.
- Có snapshot trước–sau theo CompanyId + ConversationId, tin từng nhóm và regression quyền/badge. Dọn code không đồng nghĩa dọn DB: không tự chạy migration/Down, drop bảng/cột, gộp nhóm, xóa tin hoặc reset badge.

## Công cụ audit chỉ đọc

- CLI audit/Excel ngoài API vẫn phải dùng transaction chỉ đọc, snapshot nhất quán, timeout và rollback; không gọi endpoint mark-read để lấy báo cáo.
- Xuất số liệu thực tế, tách chưa xóa/đã xóa; thiếu bảng thì chưa chuyển, không tự tạo schema. Không suy ra ngày/đợt chuyển từ cột không có: snapshot chỉ chứng minh trạng thái hiện tại; API report xác định các ID mới được bổ sung trong chính lần chạy đó.
- Mã màu không phải khóa duy nhất. Giữ snapshot/file Excel của user khi dọn công cụ, không xóa báo cáo cùng cleanup code.

Runbook và inventory nằm ở `design/group-storage/API_SYNC.md`, `TRANSITION_CLEANUP.md` của InternalMail; CLI tham khảo `tools/internal-mail-group-storage/README.md`.

## Cấu hình thành viên thường trực

- Đọc `HRM.Application/Features/InternalMail/DefaultMembership/README.md` khi sửa policy/rule/revision hoặc API cấu hình. Ba model DefaultMembershipPolicy/Revision/Rule, capability và config service là code thường trực, không đánh dấu là backfill để xóa.
- Giai đoạn hiện tại chỉ chuẩn bị model/API; không tự seed/apply schema, chạy sync hoặc thay resolver hard-code. Configured không đồng nghĩa đã áp dụng membership. API group-storage mặc định hoặc target=all chỉ làm mapping + bù President theo resolver cũ, không áp dụng policy. target=mapping chỉ mapping; default-members/preview vẫn phải từ chối, không báo Completed giả cho đồng bộ policy.
- Publication có expectedVersion, company scope, capability riêng, transaction/lock và lịch sử append-only. Rollback tạo revision mới, không sửa lịch sử. Không dùng publication để thêm/xóa participant hoặc cập nhật badge.
- Model HRM.api ExcludeFromMigrations; F:\Backend chỉ đọc, không tự port/build/sinh migration tại đó. Operator cần schema trước khi gọi API cấu hình.


