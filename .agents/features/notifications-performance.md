# Hiệu Năng Notification Hub Và InternalMail

Đọc khi điều tra hoặc tối ưu tải inbox, đổi tab, mở cuộc trao đổi, unread summary, phân trang hoặc request FE bị trùng.
Áp dụng cho BE và việc đối chiếu với FE; khi sửa FE phải đọc AGENTS.md của repo FE. Rule này chỉ quy định cách đo
và kiểm chứng hiệu năng, không thay thế rule bảo mật, build/Git hay nghiệp vụ notification hiện hành.

## Xác Định Đúng Chỗ Chậm

- Ghi thao tác tái hiện, endpoint/query params, bộ lọc, trang/cursor, kích thước inbox, phiên bản đang chạy và chế độ dev/production.
- Xác nhận API đang nạp code vừa build. Build ra artifacts không cập nhật tiến trình API đang chạy.
- Phân biệt ba số đo: thời gian query DB qua EF; thời gian HTTP toàn endpoint; thời gian từ bấm tab đến nội dung hiển thị.
  Không dùng số đo query đơn lẻ để tuyên bố toàn màn hình mở nhanh tương đương.
- Với browser, xem waterfall/Timing để phân biệt request bị xếp hàng, TTFB, tải response và render; tìm request tuần tự,
  gọi trùng, query cũ còn chạy khi đổi tab, cache và refresh từ focus/realtime/read receipt. Chỉ dùng công cụ browser được phép.
- Với BE, tách bước load feed, chọn sự kiện đại diện, enrichment conversation và unread. Không kết luận DB thiếu index,
  full scan hoặc bị khóa chỉ từ thời gian chậm; cần execution plan hoặc bằng chứng tương ứng.

## Đo Query Trước Và Sau

1. Dùng cùng DB/dữ liệu, company, employee, quyền, bộ lọc, thứ tự, projection, page size và cursor cho hai phiên bản.
   Giữ baseline trước khi sửa. Nếu dữ liệu có thể thay đổi trong lúc so sánh, dùng một snapshot nhất quán hoặc fixture.
2. Diagnostic trên DB thật phải chỉ đọc: mở transaction, `SET TRANSACTION READ ONLY`, giới hạn timeout và số lượt chạy,
   kết thúc bằng rollback/dispose. Không gọi endpoint mark-read/read-all hoặc tự sửa dữ liệu để tạo số đo.
3. Đặt `Stopwatch` quanh phần thực thi được xác định rõ, ví dụ `await query.Select(...).ToListAsync(ct)`.
   Stopwatch EF có thể gồm dịch LINQ, network và materialization; không gọi đây là thời gian PostgreSQL thuần.
4. Ghi riêng lần đầu và lần chạy lại. Không gọi lần đầu là DB cold-cache nếu chưa xác minh, không xóa cache DB dùng chung.
   Khi cần kết luận ổn định, chạy ít nhất 5 mẫu sau warm-up, xen kẽ baseline/bản mới để giảm lệch do cache và tải hệ thống;
   báo median và khoảng dao động. Chỉ báo p95 khi có đủ mẫu, ví dụ từ 20 mẫu, kèm cỡ mẫu.
5. So sánh ID và thứ tự kết quả trong bộ nhớ, không chỉ số dòng. Kiểm tra trang đầu, cursor tiếp theo và toàn bộ kết quả
   nếu kích thước phù hợp. Sai kết quả thì bản tối ưu chưa đạt, dù nhanh hơn.
6. Nếu cần, dùng `EXPLAIN (ANALYZE, BUFFERS)` cho SELECT trong phạm vi chỉ đọc, có timeout; ANALYZE thực thi query thật.
   Không tự load-test, chạy nhiều request đồng thời hoặc chạy query không giới hạn trên hệ thống đang phục vụ người dùng.

Chỉ xuất nhãn phép đo, thời gian, số dòng và kết quả đối chiếu. Lấy connection từ cấu hình trong bộ nhớ; không in
connection string, token, payload, tên người dùng hoặc ID nghiệp vụ. SQL/plan/log có thể chứa giá trị nhạy cảm: phải
loại bỏ trước khi đưa vào báo cáo hoặc commit. Query phải parameterized.

Nếu dùng tài khoản đại diện, ghi rõ đó không phải phiên đăng nhập của user. Principal giả không có role không chứng minh
được hiệu năng hay tính đúng đắn của nhánh executive/R&D leader; kiểm tra các nhánh quyền liên quan riêng.

## Điểm Vào Code Và Công Cụ

- `HRM.Application/Features/Notifications/Services/NotificationService.cs`: feed và thứ tự áp dụng bộ lọc.
- `HRM.Application/Features/NotificationHub/Queries/GetItems/NotificationHubLatestEventQuery.cs`: sự kiện đại diện mỗi conversation.
- `HRM.Application/Features/NotificationHub/Queries/GetItems/GetNotificationHubItemsQueryHandler.cs`: endpoint items và enrichment.
- `HRM.Application/Features/InternalMail/Services/InternalMailAreaAccessService.cs`: scope xem conversation/notification và unread.
- `HRM.Infrastructure/DatabaseContext/InterfaceImplementations/ApplicationDbContext.InternalMailUnread.cs`: liên kết từ JSON và unread query.
- FE: `src/shared/ui/Layouts/AppShell/notifications/NotificationHub.tsx`, `NotificationFeedRequests.ts` và cache trong core notifications.

Workspace có thể còn utility cục bộ `artifacts/notification-latency-diagnostic/Diagnostic.csproj`. Thư mục artifacts bị
ignore, không phải công cụ được bảo đảm có trong checkout khác. Nếu có, đọc source trước, kiểm tra scope, timeout,
projection và baseline còn phù hợp rồi build lại; không chạy DLL cũ hoặc dựa vào reflection đã lỗi thời.
`profile` đo query lấy 30 ID; `profile compare` đối chiếu kết quả của query baseline với query hiện tại.
Các chế độ này không đo HTTP, enrichment đầy đủ hoặc render. Nếu utility không còn, tạo diagnostic chỉ đọc theo quy trình
trên; không sao chép credential hoặc tự tạo schema để chạy benchmark.

## Các Điều Kiện Không Được Đánh Đổi Để Lấy Tốc Độ

- Giữ company/employee inbox scope, archived state, quyền participant/area và mọi hạn chế visibility hiện hành.
- Chọn sự kiện đại diện trước category/event-group/cursor. Tab nghiệp vụ không để chat đẩy mất sự kiện nghiệp vụ;
  tab message/all dùng sự kiện mới nhất thực tế. Giữ quy tắc hòa CreatedDate bằng ID.
- Lọc unread trước phân trang; tin đại diện đã đọc vẫn có thể thuộc cuộc trao đổi còn tin cũ chưa đọc.
- FE cache phải đúng user/session và bộ lọc; hủy request cũ, chặn response cũ ghi đè, giữ refresh sau mutation/realtime.
  Không bỏ đối soát unread, đổi badge về 0 hoặc bỏ kiểm tra quyền để tạo cảm giác nhanh.
- Không tự thêm index/migration/schema hoặc chạy backfill; áp dụng quy trình phê duyệt thay đổi DB của repo.

## Kiểm Chứng Và Báo Cáo

Chạy regression liên quan latest event, unread, quyền và phân trang; nếu sửa FE thì kiểm tra thêm cancellation/cache,
đổi tab nhanh và trở lại tab cũ. Build/test theo `.agents/git-and-build.md`; báo riêng lỗi có sẵn hoặc test bị bỏ qua.
Báo thao tác đã đo, phạm vi dữ liệu, điều kiện đo, số mẫu, trước/sau, kết quả đối chiếu và giới hạn của phép đo.
Nếu chưa kiểm tra API/browser trên bản mới, nói rõ và nêu bước restart/reload cần thiết; không coi unit test là số đo tốc độ.
