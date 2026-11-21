# Phần code chuyển đổi có thể dọn về sau

Quy trình hiện hành: [API_SYNC.md](API_SYNC.md). API/controller/command/permission, report renderer, PresidentMembershipBackfill và DI có marker `synchronization-api` để gỡ sau nghiệm thu. `ApplicationDbContext.GroupWrites.cs` và `InternalConversationGroupStore` là writer thường trực, **không xóa khi gỡ API**. Chỉ nhánh `schema-readiness`/`legacy-bridge-detection` trong writer là tương thích tạm.

Marker thống nhất: `GROUP-STORAGE-TRANSITION`. Marker là chỉ dẫn tìm phạm vi và điều kiện dọn, không phải cho phép xóa cả file hoặc dữ liệu DB. Phân biệt API/công cụ dùng trong đợt chuyển đổi với writer/mapped reader cần tiếp tục vận hành.

`InternalMailAreaAccessService.UnreadKeys.cs`: marker `switch` giữ query khóa unread legacy khi `GroupStorage.Enabled=false`; chỉ bỏ guard sau khi mọi môi trường đã chuyển canonical và đối chiếu quyền/tập khóa/badge. Hàm lấy khóa async, kiểm tra quyền và bước fallback tin legacy không phải code backfill để xóa toàn bộ.

```powershell
rg -n "GROUP-STORAGE-TRANSITION" HRM.Application HRM.Infrastructure HRM.Api tools/internal-mail-group-storage
```

| Nhãn | Vị trí | Khi dọn cần làm gì |
| --- | --- | --- |
| switch / comparison-reader (unread plans) | InternalMailAreaAccessService.UnreadConversationIds và LatestVisibleMessageBusinessEvents | Sau cutover toàn bộ DB, giữ kế hoạch canonical đã đo và bỏ nhánh query legacy; không xóa predicate quyền ở khóa nhóm. Rà parity role/DTO/unread và đo lại trước khi bỏ guard UsesGroupStorage. Không đổi schema/flag hay dữ liệu như một phần tối ưu query. |
| switch | InternalMailGroupStorageOptions; constructor/access predicates trong InternalMailAreaAccessService; hai DI registrations ở Application và API | Bỏ cờ cùng nhánh legacy sau nghiệm thu, rồi bắt buộc kiểm tra mapping thay vì xóa kiểm tra quyền/mapping. Cập nhật test cấu hình và runbook. |
| legacy-adapter | InternalMailAreaAccessService.Storage.cs, false branch của GroupScopes | Xóa nhánh chuyển RelatedType/RelatedId cũ thành cấu trúc nhóm. Giữ query đọc bảng mới và các helper root/children dùng trong runtime. |
| mirror-validation | Mapped branch trong InternalMailAreaAccessService.Storage.cs | Chỉ bỏ các điều kiện đối chiếu RelatedType/RelatedId khi mọi writer đã dùng bảng mới làm nguồn chính. Giữ company, active, root hợp lệ, loại nhóm và authorization. |
| comparison-reader | InternalConversationGroupReader và DI đăng ký nó | Xóa helper chỉ dùng so sánh, cùng test dành riêng cho helper đó. Không xóa InternalConversationGroupInfo/Scope đang được runtime sử dụng. |
| audit-only | tools/internal-mail-group-storage; preflight và verification SQL | Sau nghiệm thu có thể bỏ công cụ xuất Excel/SELECT, giữ snapshot trước–sau ở nơi lưu hồ sơ. Không nhầm file báo cáo với dữ liệu DB. |
| synchronization-api | Controller/command/GroupStorage report models, GroupStorageSynchronization, PresidentMembershipBackfill, sheet membership/DTO, Excel renderer, DI, capability/catalog/role resolver/seed | Có thể gỡ API và report sau nghiệm thu; giữ shared store, validator và live writer. |
| schema-readiness / legacy-bridge-detection | ApplicationDbContext.GroupWrites.cs | Chỉ dọn tương thích DB/phiên bản cũ sau rollout; không xóa writer thường trực. |
| legacy-write-shape | Structural fields trong CreateArea, EnsureDefaultAreas, ForwardToGroup | Các field hiện làm đầu vào cho atomic writer; thay toàn bộ đường ghi/đọc canonical trước khi bỏ, không xóa mapping writes. |
| legacy-enum-values | InternalMailRelatedType: ConversationTechnical=17, ConversationPricing=18, ConversationCustom=19 | Giá trị đã persist: không đổi số, tái sử dụng hoặc cast sang GroupType mới. Chỉ ngừng dùng khi đã thay toàn bộ consumer/writer và rà dữ liệu/payload lịch sử; không tự xóa enum hay sửa DB. ForwardSource=20 không thuộc phần legacy này. |
| legacy-membership-defaults | DefaultMembersAsync và role preview trong QuickGroups | Resolver đang chạy, giữ nguyên đến khi có đợt chuyển Policy riêng và đối chiếu quyền/membership/badge. Thay hai đường cùng lúc; không xóa do API bù President đã chạy hoặc vì entity Policy đã tồn tại. |
| backfill-bridge | SQL triển khai phương án trigger cũ, chỉ lưu lịch sử | Không chạy cho schema-only + API. Xóa file không gỡ trigger trong DB; thao tác DB cần review/authorization riêng. |

Entity/config/DbSet, GroupType, quyền/capability, membership, message, read-state, notification/outbox và logic badge là **phần vận hành cần giữ**, không phải code tạm để xóa.
`InternalConversation` không phải entity obsolete. Marker trên Subject/RelatedType/RelatedId và EF configuration chỉ nói về mirror của nhóm con; title và tham chiếu nghiệp vụ ở hồ sơ gốc vẫn cần giữ. Store/validator là code dùng chung thường trực dù phần đọc input legacy bên trong có marker.
RelatedType/RelatedId ở hội thoại gốc vẫn là liên kết nghiệp vụ SampleRequest/Quotation, không phải toàn bộ đều là cột tạm. Không renumber/xóa enum lịch sử hoặc tham chiếu forward nguồn `20` khi dọn enum nhóm con `17–19`; cần rà các consumer lịch sử trước.

Write-through mới đã bao phủ mọi EF writer qua SaveChanges/SaveChangesAsync, không cần trigger ở schema-only deployment. Vì thế chỉ giữ bridge ở DB cũ trong lúc chờ operator xử lý. Khi dọn phải kiểm tra mọi API/worker/phiên bản đang chạy đã nạp binary mới và không có writer raw SQL/ExecuteUpdate bypass hook; không được gỡ bridge chỉ vì đã bật cờ đọc true. API không tự gỡ trigger hoặc chạy DDL.

Giữ phạm vi không sửa DB/migration trừ khi người dùng cho phép riêng. Không drop cột/bảng, chạy Down, gộp nhóm, xóa tin hay reset badge như một phần của việc dọn comment/code.

Excel CLI audit cho biết mapping tồn tại/khớp **tại thời điểm chụp**. Excel của API đồng bộ còn ghi các ID mới được bổ sung trong chính lần gọi, RunId và kết quả commit, nhưng không tự chứng minh quyền đọc/gửi. Bảng hiện tại không có MigratedAt/BatchId; file report giữ lịch sử lần chạy phía client. Đối chiếu bằng CompanyId + ConversationId/RootConversationId, không dùng riêng mã màu vì có thể lặp giữa hồ sơ.

Runbook vận hành và rollback: [CUTOVER.md](CUTOVER.md). Công cụ xuất lại: [tools/internal-mail-group-storage/README.md](../../../../../tools/internal-mail-group-storage/README.md).

Khi gỡ PresidentMembershipBackfill sau nghiệm thu, giữ nguyên membership đã bổ sung, live default-member resolver và các quyền Developer/President vận hành. Không tự xóa participant hoặc hoàn tác dữ liệu theo RunId; workbook là audit phía client, không phải lệnh rollback.
