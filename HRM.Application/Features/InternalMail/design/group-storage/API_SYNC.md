# Đồng bộ nhóm bằng API, không dùng trigger

Đây là quy trình triển khai hiện hành. Thay thế cách nhúng SQL backfill/bridge vào Up trước đây. Task chỉ chuẩn bị code/test, không chạy migration hay API trên DB đang dùng.

## Thứ tự người vận hành tự chạy

1. Giữ `InternalMail:GroupStorage:Enabled = false`, backup và thử trên DB bản sao trước. Tự chạy Add-Migration/Update-Database ở repo sinh migration để tạo **cấu trúc bảng**: bảng nhóm, PK/alternate key, hai FK Restrict, check constraints và hai index. Không thêm nội dung 01-before-ef-schema/02-after-ef-schema hoặc full 02-expand-backfill vào Up; không chạy các script đó sau migration.
2. Triển khai binary mới cho toàn bộ API/worker ghi hội thoại. Không để phiên bản cũ còn ghi song song khi nghiệm thu/bật cờ. Code mới tự ghi mapping cùng transaction khi tạo/cập nhật cấu trúc hội thoại, kể cả khi cờ đọc mới còn false. Không cần thêm cờ bật writer. Trước khi bảng tồn tại, code vẫn lưu theo cách cũ; sau khi bảng tồn tại, write-through hoạt động. Đây chỉ là kiểm tra schema lúc có structural write, không phải trên hover/list hoặc mỗi lần gửi tin.
3. Đăng nhập **Developer** có company/employee hợp lệ, gọi `POST /api/v1/internal-mail/group-storage/synchronize` trong Swagger/Postman. Không cần body và không truyền CompanyId. Mặc định đồng bộ mapping và bù membership President; `?target=all` tương đương. `?target=mapping` giữ chế độ chỉ mapping cũ, không sửa membership. Save/download response dạng Excel; mỗi lần gọi có tên file và RunId mới. API chỉ đồng bộ công ty của token.
4. Mở Excel: `Outcome = Completed`, không có issue. Sheet Theo mã màu tổng hợp từng root/mã màu; Chi tiết nhóm chỉ rõ `Mới đồng bộ`, `Đã có trước`, `Sai mapping`, số tin chưa xóa/đã xóa và stable IDs. Sheet Bù President ghi EmployeeId, nhóm, thêm mới/đã có/đã bị gỡ/bỏ qua và lý do; summary bổ sung ba cột đếm membership. Chạy lại cho kết quả không có mapping mới nếu nguồn không đổi. Tin mới hợp lệ giữa hai lần chạy có thể làm số tin tăng; đó không phải mất/nhân đôi dữ liệu do đồng bộ.
5. So với snapshot trước chuyển đổi. Kiểm tra nhóm, quyền gửi/đọc, @Tôi và badge bằng cùng tài khoản thực tế; báo cáo dữ liệu không thay thế kiểm thử quyền/badge. Sau đó tự đặt `InternalMail:GroupStorage:Enabled = true`, restart các API/worker liên quan và kiểm tra lại. Không có cấu hình FE cần đổi.

Tài khoản DB mà BE dùng cần SELECT/INSERT/UPDATE bảng nhóm, SELECT các nguồn role/employee/nhóm và INSERT bảng InternalConversationParticipants cho chế độ gộp. Bù President không UPDATE membership cũ; API không tự GRANT quyền. Nếu tài khoản chạy migration khác tài khoản DB của BE, operator cần review/cấp quyền trước khi gọi. Không dùng script grant gắn function/trigger của phương án cũ để thay thế mà chưa đối chiếu.

Nếu đã thêm SQL backfill/trigger vào migration nhưng **chưa áp dụng**: tự bỏ các phần SQL thủ công trước khi chạy, giữ schema EF. Nếu DB đã có trigger cũ, API xuất `Blocked` + `LegacyTriggersInstalledUseSchemaOnlyDeployment` và không tự drop trigger/function. Cần đối chiếu trạng thái DB thực tế và một bước gỡ có review riêng; không chạy chồng hai cơ chế hoặc tự dùng Down.

## API và Excel

Route: `POST /api/v1/internal-mail/group-storage/synchronize`.

- `[Authorize]` và capability `internal-mail.group-storage.synchronize`, nhóm quản trị kỹ thuật mặc định Developer. President/Admin/Sale/Lab/PLPU không được action này chỉ vì có quyền chat; claim riêng lẻ không vượt role gate. Không thay đổi role/capability đọc/gửi chat; chỉ bổ sung membership Member vào nhóm mặc định đúng phạm vi bên dưới. Resolver tương thích token Developer explicit cũ. Script seed-group-storage-sync-permission.sql chỉ đồng bộ metadata tùy chọn, không cần chạy để token Developer hiện tại sử dụng API. Khi thay/thu hồi role, login/refresh để token phản ánh quyền mới.
- Không nhận CompanyId, root IDs hoặc tên nhóm từ caller. Không có quyền/thiếu context: 401/403, không query dữ liệu hoặc xuất file.
- Request có quyền trả file `.xlsx` qua Content-Disposition, MIME application/vnd.openxmlformats-officedocument.spreadsheetml.sheet. Header `X-Group-Sync-Outcome` và `X-Group-Sync-Run-Id`; Cache-Control no-store. Không trả EF entity, SQL, connection string, nội dung tin hoặc tên nhân viên; sheet membership dùng EmployeeId ổn định. File tải về phía client, không tự lưu báo cáo chứa dữ liệu vào file share của server.
- `Completed`: transaction đã commit trước khi trả file. `Blocked`: thiếu schema/trigger cũ/cấu trúc hoặc mapping không hợp lệ, không bổ sung gì. `Failed`: lỗi DB/validation trước commit, transaction rollback. `Unknown`: mất xác nhận ở lúc commit, không thể kết luận đã commit hay chưa; chạy lại an toàn để kiểm tra. Chỉ bật cờ sau một lần Completed được đối chiếu.
- Mỗi lần request hợp lệ có report mới, kể cả Blocked/Failed khi có thể tạo file. Nếu client hủy/mất mạng hoặc bản thân renderer/file transport lỗi, không thể bảo đảm client nhận được file; chạy lại không tạo trùng. Report được render trước commit để lỗi xuất file không làm commit một đợt không có report. Không công bố Completed khi chưa commit thành công.
- Tối đa 20.000 hội thoại mỗi công ty mỗi lần, vượt giới hạn thì Blocked, không backfill một phần rồi giả báo đủ. Số tin là toàn bộ message lưu trong nhóm, tách chưa xóa/đã xóa, không phải badge của một người. Bao gồm inactive ở phần mapping; membership chỉ bổ sung vào nhóm/hồ sơ active. Tối đa 20.000 cặp nhóm/President cho sheet membership; vượt giới hạn chặn và rollback cả đợt. Mã màu lấy từ Product của SampleRequest cùng company; hồ sơ Internal/Quotation có thể trống mã màu.

## Ghi dữ liệu sau đồng bộ

`ApplicationDbContext.GroupWrites.cs` là write-through dùng chung cho mọi EF writer của InternalConversation: root từ phối mẫu/báo giá/chat, tạo default/custom, sửa Subject và IsActive. Không rải thêm từng handler để tránh sót worker hoặc module khác. Nhóm mới và thay đổi hội thoại lưu/rollback cùng transaction; outer transaction có savepoint để caller không commit nửa dữ liệu khi group write lỗi. Cả SaveChanges và SaveChangesAsync đều đi qua đường này.

Company advisory lock thống nhất giữa API, live writer và LockInternalConversationAsync; company lock trước root row lock. Insert root mapping trước child; ON CONFLICT không tạo trùng; API chỉ bổ sung missing, không ghi đè mapping mâu thuẫn. API không sửa conversation/message/notification/read-state/outbox. Mặc định có bổ sung membership President còn thiếu theo phạm vi bên dưới; target=mapping không sửa membership. Live writer cập nhật lifecycle/custom Subject mirror như hành vi cũ, không tự chạm mốc thời gian/tin hoặc badge. Message-only update bình thường không kích hoạt backfill.

Mapping có sẵn sai type/root/company/active, orphan/nested root, custom name lỗi hoặc trùng default active: API chặn cả đợt và xuất lý do. Không tự gộp, xóa hoặc chuyển quyền để sửa lỗi dữ liệu. Write-through từ chối đổi cấu trúc type/root mâu thuẫn; unique/FK/check của DB vẫn bảo vệ dữ liệu. Không dùng raw SQL/ExecuteUpdate để bypass EF cho thay đổi cấu trúc hội thoại; nếu thêm writer kiểu đó phải tích hợp cùng writer/transaction lock.

Không có trigger mới nào do code/API tạo. Trường hợp DB cũ đã có legacy trigger, live writer tương thích với nó trong lúc chờ operator xử lý; API vẫn chặn việc backfill ở trạng thái đó để tránh deployment hiểu nhầm. Schema-only là đường được khuyến nghị cho triển khai mới.

## Đánh dấu và dọn về sau

API/controller/command, permission kỹ thuật, report/renderer và DI có marker `GROUP-STORAGE-TRANSITION: synchronization-api`; có thể gỡ sau nghiệm thu theo [TRANSITION_CLEANUP.md](TRANSITION_CLEANUP.md).

**Giữ lại write-through và shared store** khi gỡ API: nhóm tạo về sau vẫn cần được lưu. Chỉ dọn schema-readiness/legacy-bridge-detection sau khi không còn DB/phiên bản cũ. Không tự xóa bảng/cột/ID, tin, membership, badge hoặc các file Excel đã tải. Rollback nguồn đọc: false + restart, giữ mapping và writer. Không dùng Down để xóa dữ liệu.

## Kết quả kiểm chứng code

Đợt mapping ban đầu có regression/baseline riêng trong artifacts/group-storage-api-results. Đợt gộp President đã chạy
1.634 test chat/notification đạt, 2 test ngoài cấu hình được bỏ qua; 77 test cô lập PostgreSQL/Excel/quyền sync đạt.
Kiểm tra bổ sung 5 test cho cả flag đọc nhóm cũ/mới, truyền mode và sheet Excel đều đạt. Test dùng PostgreSQL loopback
riêng, không đọc appsettings hoặc dùng DB nghiệp vụ. Workbook synthetic được mở lại để đối chiếu ba sheet, số tin,
Membership Added/Existing/Removed, kiểu số và chống công thức từ text. Báo cáo TRX nằm trong artifacts/president-sync-results.

Chưa chạy migration, chưa gọi API trên DB thật hoặc kiểm thử UI nhiều tài khoản sau đồng bộ. Operator thực hiện
sau khi triển khai schema/binary. Không suy ra dữ liệu thật đã chuyển từ kết quả kiểm thử synthetic.

## Bù membership President trong cùng lần gọi

Không phụ thuộc Policy/Revision/Rule, không seed policy hoặc kích hoạt resolver policy DB. Dùng role set/eligibility hiện hành.
Company và employee từ principal Developer; group scope/read flag không cấp thêm quyền cho người gọi.

- Chọn employee active có user-role President active trong công ty; bỏ tài khoản đồng thời có role Developer active.
- Chỉ bổ sung Member vào Kỹ thuật/Giá **đã tồn tại**, active, có root Sample Request active và record Sample Request active cùng công ty. Không tự tạo nhóm mặc định còn thiếu, không thêm vào Chung, Custom hoặc nhóm thuộc báo giá/module khác.
- Chưa từng có membership: thêm mới với JoinedAt của lần chạy. Đã có active: giữ nguyên Role/mute/archive/LastReadAt. Đã có inactive: giữ nguyên, không tái kích hoạt. Root locks theo thứ tự ID thống nhất với invite/remove, trong transaction và company lock dùng chung.
- Giữ eligibility RD/màu hiện hành; nhóm có forward giữ kiểm tra nguồn. Thiếu quyền nguồn thì bỏ qua và ghi lý do, không tự thêm người vào source để vượt kiểm tra.
- Không đổi LastMessageAt/CreatedAt, message, file, read-state, inbox notification, người nhận cũ hay outbox. Không phát tin "đã thêm" hoặc push từ đợt bù. Dữ liệu unread đã có được giữ nguyên; sau khi được cấp quyền, UI có thể hiển thị badge vốn tồn tại ở nhóm trước đó chưa được phép xem.
- Chỉ commit sau khi mapping, membership và render Excel đều thành công. Lỗi/Blocked rollback cả hai phần. Unknown khi mất xác nhận commit: báo chưa xác nhận ở các dòng Added, không khẳng định rollback; chạy lại an toàn. Chạy lại không tạo trùng và không đổi preferences cũ.

Excel có ba sheet: **Theo mã màu**, **Chi tiết nhóm**, **Bù President**. Sheet thứ ba dùng EmployeeId, không xuất họ tên; summary đếm Added/Existing/Skipped theo root. Lý do là mã ổn định: MissingDefaultMembership, ExistingMembershipPreserved, RemovedMembershipNotReactivated, CustomRequiresExplicitInvitation, InactiveGroup, InactiveOrNonSampleRequestRoot, OutsideProductOrForwardSourceScope.
Không có President hợp lệ hoặc không có nhóm cần bù thì sheet không có dòng dữ liệu; không đồng nghĩa API lỗi. target=mapping cũng để sheet này rỗng và ghi rõ chế độ ở dòng 4.

`target=all` hiện nghĩa mapping + bù President, **không phải** đồng bộ mọi rule từ Policy. `target=default-members`, giá trị target khác và mọi tham số preview chưa được hỗ trợ: HTTP 400 UnsupportedGroupSynchronizationTarget, chưa query/ghi dữ liệu. Default request không có query param thực hiện cả hai phần.

Các bước migration/cutover vẫn do operator thực hiện. Không có schema mới trong phần bù President này; vẫn cần bảng mapping hiện có trước khi API gộp chạy Completed. Chưa gọi API trên DB nghiệp vụ để kiểm chứng dữ liệu thật.
