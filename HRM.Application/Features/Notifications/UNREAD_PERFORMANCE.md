# Đối chiếu hai API Sample Request chưa đọc

## Notification Hub/InternalMail trên DB test — 20/11/2026

Kết thúc ở phạm vi tối ưu đường đọc và điều phối FE theo lựa chọn của người dùng.
**Chưa đạt tiêu chí mọi lần mở tin dưới 100ms và Chưa đọc/@Tôi dưới 300ms, bao gồm
tải trang mới và request đầu sau khởi động API.** Không chuyển tiêu chí sang median
hoặc p95 để nghiệm thu. Không triển khai compiled model/query, ReadyToRun, SSR, index
hay thay đổi schema. Không restart dịch vụ chính bởi agent.

Phép đo sử dụng PostgreSQL 17.6, DB test `dev_enviroment_v1` tại localhost:5432;
GroupStorage/Reactions bật. Probe handler chạy transaction repeatable-read chỉ đọc,
timeout 10s, rollback; so toàn bộ JSON DTO trong mỗi cặp, không ghi nội dung hay ID
vào kết quả. Bản tham chiếu giữ reader cũ nhưng dùng chung một số helper quyền hiện
hành, nên không phải binary nguyên trạng trước mọi thay đổi.

| Phạm vi đo | Trước → sau | Điều kiện và giới hạn |
| --- | --- | --- |
| Handler @Tôi có dữ liệu | median 4.313,49 → 263,27ms; p95 4.476,74 → 285,42ms | 20 cặp sau 5 warm-up, 22 sự kiện; reader commands 18 → 35; DTO khớp. Tăng số query nhưng giảm chi phí từng tập quyền. Bản v27. |
| Handler Chưa đọc | median 1.689,18 → 252,01ms | 5 cặp warm, 15 dòng, min–max mới 233,55–302,35ms; DTO khớp. Bản v29; không có p95. |
| UI @Tôi có dữ liệu | 3 mẫu mới 215,2 / 204 / 234ms | Element Timing của chữ hiển thị, foreground; 18 dòng nhóm, response 66.015 bytes. FE `-muTcn-xhVJkTg5LVAhzA`; không đủ mẫu để kết luận mọi lần hoặc p95. |
| HTTP `/messages` đầu trên tiến trình riêng | 4.439,19ms và 6.107,51ms ở hai lần khởi động | 12 reader commands, response 10.596 bytes; JWT/quyền đầy đủ, cùng hội thoại, DTO khớp bản nóng. Năm lượt sau mỗi lần khoảng 27,79–92,62ms. Đây chưa gồm render FE. |

API đo các số mới có MVID `b68c543a-1cf8-40e1-97c6-4cc7238dc2ff`, Application
`bdfe6e4e-0847-4406-af9f-cd155b969237`. Bản v27 trong hàng đầu có API
`9b2889da-d251-43f4-b231-ae0b51053e32`, Application
`af7a5697-16fe-45c8-bf7e-cbc07c96f7b5`. Không gán số giữa các build cho nhau.

Ở lần lạnh 6.107,51ms, timing pipeline server ghi 5.692,21ms trước khi hoàn tất xác
thực; handler messages 239ms, reader execution cộng 102,93ms. Đây là bằng chứng
khởi tạo trên đường xác thực chậm, chưa phân bổ hết cho EF/JIT/kết nối. Prototype
khởi tạo model 287 entity trước khi lắng nghe tốn 2.824,22ms startup; request đầu vẫn
1.692,79ms, nên không giữ thay đổi này trong mã sản phẩm. Không cộng các phase lồng
nhau để suy tổng. Ngưỡng lạnh chưa đạt dù query chạy nóng đã nhanh hơn.

Các thay đổi giữ lại: TanStack Query chia sẻ GET đang chạy theo user/company/session
và đầy đủ tham số, đọc mới vẫn kiểm tra API; tải messages ngay khi bấm, trước dữ liệu
phụ; cache danh sách không tự mở hội thoại trước khi đối soát; summary khi đổi bộ lọc
chờ danh sách kết thúc. BE giới hạn tập ứng viên trước các truy vấn quyền/lịch sử,
tái dùng tập sự kiện đã được cấp quyền trong cùng request và theo đủ user/company/
employee; hơn 4.096 ứng viên quay về query đầy đủ. Không cắt dữ liệu, quyền hay badge.

Đã đối chiếu ma trận DTO Chưa đọc/@Tôi với filter/cursor và quick-group reference trên
v29; regression sau dọn telemetry: 1.613 test BE, 641 test FE. Telemetry FE và timing
messages tạm đã gỡ khỏi source sản phẩm; bản đo giữ trong artifacts, không phải bản
đang được triển khai. Chưa chứng nhận luồng deep-link thông báo và navigation-to-text
của trang mới trên binary cuối. Thao tác test đã đọc một số thông báo theo quyền được
cho phép; không seed hay khôi phục giả trạng thái chưa đọc.

Artifact thô và build hash ở `artifacts/notification-baseline-20261120` (hai repo);
phép đo lạnh ở `artifacts/notification-cold-20261120`. Các thư mục này không commit.
Hạ tầng đo riêng loại HRM hosted workers để tránh side effect, nên không đại diện
tải nền đầy đủ của production. Giới hạn query-count là số lượt thực thi reader,
không phải tổng mọi lệnh SQL hoặc số request FE.

### Kiểm tra bản source cuối

API Release build sau dọn telemetry: 0 lỗi, 12 cảnh báo package có sẵn; API MVID
`298bbf97-72ee-4111-ae88-010abe2d9692`, Application
`36be0951-6ea2-47c9-a606-d80c6fe3e08f`. Đây là build kiểm chứng, chưa được phục vụ
hay đo lại; không gán các số v29 cho binary này. Hash ở `backend-build-final-clean.json`.
FE: 71 file/641 test pass; UI/theme pass; lint phạm vi 0 lỗi, 6 cảnh báo hiện có.
Build bản sao toàn bộ source FE hiện tại compile thành công nhưng **không qua
TypeScript** tại `SampleRequestGalleryPage.test.tsx:118`: prop `expandedEntries`
không còn trong kiểu component. File thuộc PLM ngoài phạm vi tối ưu và được giữ
nguyên. Không bỏ kiểm tra type, sửa test hoặc làm mới baseline UI để ép build pass.
Log ở FE `artifacts/notification-final-20261120/build-isolated.log`; hash các file
Notification chính khớp canonical trong `source-manifest.json`. Không có build ID
production cuối đã pass để triển khai.

Một lượt build kiểm chứng chạy sớm trước khi copy snapshot hoàn tất đã khiến npm
chọn repo cha và ghi output `.next` của FE chính. Lượt đó đã dừng, không dùng làm
bằng chứng. Agent không restart FE3000; sau đó kiểm tra login và script đầu trả 200,
tiến trình vẫn cùng PID. Lượt build tường minh vào thư mục artifacts riêng mới là
lượt được báo kết quả ở trên. Không hoàn tác output hoặc thay đổi code của user.

## Nhánh cảm xúc của unread-summary — 14/11/2026

Ảnh user: `nm_summary_reactions` 943 ms, `_pre_dispatch` 319 ms. Đây là đầu mối runtime, không chứng minh full scan hoặc thiếu index. Thay đổi lấy trước ID unread reaction đúng employee/company; chỉ chạy scope quyền trên các lô 256 ID, đọc lại trạng thái ở query cuối. Khi tính năng tắt trả 0 không query; không cache, không tăng timeout hoặc sửa DB.

Đo trên PostgreSQL cô lập, cùng dữ liệu/principal, warm-up rồi 5 cặp xen kẽ. Nhánh tắt trên fixture ~38.400 sự kiện: trung vị query cũ President canonical 7,26 ms; Developer 4,90; LabUser 17,56; SaleUser 9,08; President legacy 4,63. Bản mới không gọi DB ở bước này. JSON toàn summary khớp 5 cấu hình.

Nhánh bật, fixture nhỏ một thông báo cảm xúc hợp lệ, quyền hội thoại/tác giả/actor thực:

| Role | Trung vị cũ → mới (ms) | Min–max cũ | Min–max mới |
| --- | --- | --- | --- |
| President | 8,50 → 8,72 | 8,30–8,81 | 8,56–10,43 |
| Developer | 2,94 → 3,06 | 2,80–3,03 | 2,98–3,35 |
| LabUser | 7,93 → 8,24 | 7,80–8,10 | 8,04–8,27 |
| SaleUser | 8,31 → 8,18 | 7,90–10,41 | 7,96–8,40 |

Kết quả count khớp cả 4 role và về 0 sau read. Fixture bật nhỏ chưa chứng minh tăng tốc trên dữ liệu lớn; hai lượt query có overhead nhỏ. Không suy các số này thành thời gian HTTP server thật hoặc cam kết 943 ms đã hết. Regression 1.575 test đạt (quyền, cảm xúc, unread, read scope, timing); 4 ca PostgreSQL bật đạt; 5 ca summary PostgreSQL tắt đạt. Build API 0 lỗi, 12 cảnh báo NuGet ImageSharp có sẵn.

Development tách `_pre_dispatch` thành `request_cors`, `request_authentication`, `request_acting_user`, `request_authorization` (xem README). Chưa đo pipeline HTTP của server đang chạy; chưa có bằng chứng để thay cơ chế xác thực/quyền. Không restart/deploy hoặc truy cập DB thật trong task này. Artifact kết quả nằm ở `artifacts/reaction-summary-comparison` và `artifacts/unread-summary-comparison` (không commit).

## Phạm vi

### Lọc hội thoại có nhiều tin chưa đọc — 17/11/2026

`GetInternalConversationsQueryHandler` dùng `UnreadConversationIdsAsync` trong canonical storage. Query trước ghép notification state → JSON link → quyền thông báo → quyền hội thoại lặp → legacy fallback trong cùng biểu thức UNION. Khi nhiều unread, đồ thị quyền bị lặp theo delivery. Bản mới giữ điều kiện own employee/company, unread/unarchived, loại reaction và policy BBG, gom DISTINCT conversation trong SQL, rồi áp dụng nguyên `Conversations()` và membership/company-observer lên tập nhỏ. Không đưa toàn bộ ID notification về Application. Hợp với nhánh fallback dùng chung `LegacyUnreadCountsByConversationAsync`; vẫn xét mọi state của employee khi chống trùng message/notification, kể cả read/archived. Count, sort, bộ lọc search/category và phân trang ngoài cùng không đổi. Không tăng timeout, tạo cache, giới hạn giả số unread, thay schema/index hoặc truy cập DB thật.

Baseline là `UnreadConversationIds` trước sửa còn giữ cho legacy storage, cùng dữ liệu/principal và predicate type. Fixture PostgreSQL cô lập chạy warm-up rồi 5 cặp xen kẽ, đối chiếu toàn bộ tập ID, thứ tự/DTO/badge các trang (kể cả trang rỗng), BBG nhiều event cùng message, quyền theo role và JSON summary. Phase đo là `nm_unread_keys`, không phải HTTP toàn API hay thời gian FE. FE hiện chờ song song feed và conversations; task này không thay feed, layout hoặc bỏ nguồn dữ liệu. Server thật cần cập nhật BE rồi xem lại timing hai request để xác minh runtime, không suy từ benchmark fixture.

| Role / storage | Notification / state unread | Hội thoại mẫu unread | Trung vị cũ → mới (ms) | Min–max cũ | Min–max mới |
| --- | --- | --- | --- | --- | --- |
| President / canonical | 38.402 / 7.541 | 82 | 556,14 → 175,67 | 538,65–566,67 | 173,52–183,05 |
| Developer / canonical | 38.402 / 7.541 | 87 | 597,46 → 178,54 | 546,59–615,12 | 173,20–187,40 |
| LabUser / canonical | 38.402 / 7.541 | 82 | 532,06 → 181,30 | 491,37–553,76 | 178,64–195,88 |
| SaleUser / canonical | 38.402 / 7.541 | 82 | 557,01 → 196,37 | 451,84–660,16 | 80,63–203,07 |
| LabUser / legacy (query giữ nguyên) | 38.402 / 7.541 | 82 | 276,11 → 287,78 | 143,24–303,57 | 266,08–305,36 |
| LabUser / canonical, stress | 153.602 / 153.012 | 395 | 4.016,00 → 1.305,19 | 4.010,97–4.041,60 | 1.299,85–1.329,64 |

Các số là dữ liệu giả, không phải inbox của người dùng thật. Cả 6 cấu hình khớp baseline. Nhánh legacy không thay query nên không diễn giải dao động là tăng/giảm tốc thực. 1.575 test hồi quy (gồm stress, quyền, phân trang, badge BBG, read scope) đạt; 5 ca JSON summary đạt. Build API 0 lỗi, 12 cảnh báo NuGet ImageSharp có sẵn. Artifact nằm ở `artifacts/groups-unread-keys-comparison`, log `artifacts/unread-keys-reduced-final.log` và `artifacts/unread-keys-summary-regression.log`; không commit artifacts. Không restart/deploy, thay config hoặc đụng DB thật.

- `/api/v1/notifications/feed?unreadOnly=true&categoryCode=sample_request&take=30`
- `/api/v1/internal-mail/conversations?relatedType=SampleRequest&archived=false&unreadOnly=true&pageNumber=1&pageSize=50`

Feed tái dùng tập khóa chưa đọc để chỉ chọn winner của các hội thoại có thể xuất hiện.
Không giới hạn topic bên trong hội thoại, không chuyển category/cursor lên trước winner.
Thông báo standalone vẫn xét nhánh riêng. Danh sách hội thoại chỉ giới hạn nguồn tìm
khóa theo đúng relatedType/root-child của request, không giảm quyền kiểm tra hoặc đếm.
Trong canonical group storage, icon unread nhóm các sự kiện ứng viên theo conversation,
lọc khóa nhóm bằng query Conversations được phép xem, rồi chọn winner theo CreatedDate/Id. Vì quyền ở bước này
chỉ phụ thuộc conversation, chuyển predicate lên khóa nhóm giữ cùng tập kết quả. Mọi
lọc quyền nằm trong SQL trước materialization, không hydrate dữ liệu trái quyền rồi
mới che ở client. Luồng danh sách mọi tin giữ query icon hiện có.
Không đổi public contract, payload, recipient, unread state, thứ tự hay schema.

Legacy group storage giữ query icon và khóa unread cũ vì các shape mới không đem lại
lợi ích trên phép đo legacy. Tối ưu feed áp dụng cả hai chế độ. Không đổi cờ triển khai
group storage trong task này.

## Cách kiểm chứng

Test `SampleUnreadApisPreserveFullResponsesAndMeasurePairedBaseline` dùng
`IsolatedGroupStoragePostgres`: tự khởi tạo cluster mới, loopback, kiểm tra data_directory,
database/user trước thao tác dữ liệu. Không đọc connection string API hay DB thật.
Fixture gồm 160 root mẫu, 160 nhóm Kỹ thuật, 80 nhóm Giá, 200 hội thoại khác và 64 sự
kiện mỗi hội thoại tổng hợp (khoảng 38.400 sự kiện), cộng các dữ liệu biên ban đầu.

Phép đo dùng cùng một DB không thay đổi, cùng principal/options. Baseline giữ biểu
thức LINQ trước sửa cho truy vấn khóa, icon và trang feed. Interceptor chỉ thay đúng command
đó (khóa unread, icon hoặc trang feed) trong đường handler/service đầy đủ; enrichment, DTO và field masking vẫn chạy bình
thường. SQL/parameter baseline được dựng và cache trong warm-up, tránh tính thêm chi phí
biên dịch LINQ tham chiếu vào lượt đo. Warm-up cả hai shape rồi chạy 5 cặp xen kẽ trước/sau, ghi median và min/max;
không báo p95 với 5 mẫu. Không in SQL, payload hoặc ID hồ sơ trong kết quả đo.

Mỗi cặp so toàn bộ DTO bằng JSON trong bộ nhớ, không chỉ số dòng hoặc ID. So thêm
trang hội thoại 1–4 và cursor feed đến hết dữ liệu (tối đa 6 trang). Có trường hợp latest
đã đọc nhưng older unread, ties CreatedDate, latest khác category, state archived,
standalone, membership bị gỡ và các role Developer/LabUser/SaleUser. Test legacy và
canonical group storage dùng cùng contract; tests unit latest/unread kiểm tra thêm
event-group và phân trang.

Chạy kiểm thử:

```powershell
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~SampleUnreadApisPreserveFullResponses"
```

Kết quả chi tiết chỉ có số đo tổng hợp tại `artifacts/sample-unread-api-comparison`.
Artifact bị ignore; test tái tạo dữ liệu nên không phụ thuộc một DB benchmark cũ.

## Số đo ngày 07/11/2026

Đơn vị ms, cùng fixture và principal trong từng cặp. Đây là thời gian handler/service,
không phải số đo HTTP trên server đang sử dụng. Min–max thể hiện độ dao động của 5 lượt.

| Role / storage | API | Median trước → sau | Min–max trước | Min–max sau |
| --- | --- | --- | --- | --- |
| Developer / canonical | conversations | 775,0 → 689,3 | 760,0–840,2 | 667,7–723,0 |
| Developer / canonical | feed | 4.209,3 → 1.077,9 | 3.948,2–4.318,4 | 1.021,2–1.121,7 |
| SaleUser / canonical | conversations | 939,2 → 851,5 | 919,0–1.041,3 | 788,6–857,5 |
| SaleUser / canonical | feed | 4.123,4 → 1.082,3 | 3.916,6–4.298,4 | 1.038,9–1.100,7 |
| LabUser / canonical | conversations | 939,6 → 841,1 | 931,1–972,3 | 741,5–997,1 |
| LabUser / canonical | feed | 3.951,2 → 1.061,6 | 3.746,0–4.086,0 | 962,1–1.080,8 |
| LabUser / legacy | conversations | 250,0 → 231,8 | 227,9–257,7 | 225,0–249,6 |
| LabUser / legacy | feed | 3.509,3 → 942,0 | 3.481,9–3.543,5 | 912,0–1.058,3 |

Canonical feed giảm khoảng 73–74%; conversations giảm khoảng 9–11%. Không suy rộng
tỷ lệ này thành cam kết tốc độ cho mọi DB hoặc tải thực tế. Build API đạt; bộ hồi quy
1.467 test về quyền, unread, latest-event và API Báo giá đạt. Cả 4 cấu hình đối chiếu
hoàn tất, toàn bộ DTO/order/phân trang khớp trước/sau. Legacy conversations giữ query
cũ; chênh lệch nhỏ trong bảng là dao động đo, không phải cải tiến kế hoạch truy vấn.

## Đợt tiếp theo: nhóm nhanh và khóa chưa đọc

`GroupsAndUnreadKeysPreserveResponsesAndMeasurePairedBaseline` dùng cùng fixture cô lập
và các trường hợp biên ở trên. Mỗi phần warm-up trước rồi đo 5 cặp xen kẽ. Bản nhóm
nhanh trước sửa được đóng băng trong test, gồm scope quyền và nguồn đếm unread; không
thay bằng mock hoặc số giả. Đối chiếu khóa unread sau Distinct/sắp xếp, toàn bộ nhóm
nhanh và DTO của trang hội thoại 1–4. Các query phần khác của handler không đổi trong
đợt này. Artifact tổng hợp: `artifacts/groups-unread-keys-comparison`.

Nhóm nhanh đọc tập ID đã qua đầy đủ scope từ `VisibleGroupScopes` một lần, chỉ trong
phạm vi root của lời gọi. Query root/nhóm con dùng lại tập này thay vì lồng scope nhiều
lần. Thêm một lượt đọc ID nhẹ để giảm query lồng phức tạp, không phải giảm số command
bằng mọi giá. Không cache qua request. Truy vấn nhóm con vẫn giữ nhóm không hợp quyền
ở nội bộ để không đề nghị tạo nhóm thay thế khi membership đã bị gỡ. Việc đếm unread,
preview theo role/loại sản phẩm, field masking và contract công khai giữ nguyên.

| Role / storage | Nhóm nhanh median trước → sau (ms) | Min–max trước | Min–max sau |
| --- | --- | --- | --- |
| Developer / canonical | 37,40 → 30,10 | 37,04–38,41 | 29,43–30,82 |
| LabUser / canonical | 66,78 → 46,52 | 65,23–77,49 | 46,35–55,43 |
| SaleUser / canonical | 66,82 → 46,11 | 66,26–67,67 | 44,85–46,42 |
| LabUser / legacy | 29,72 → 25,12 | 26,38–33,34 | 21,46–26,06 |

Đây là thời gian riêng `quick-groups`, không phải thời gian toàn API và không nên so
với bảng handler ở đợt trước: tải máy/điều kiện giữa các đợt có thể khác nhau. Lợi ích
đo được khoảng 15–31% ở phần này; không khẳng định toàn API giảm tương đương.

Khóa chưa đọc đã thử gom ID trước scope và chuyển lọc relatedType vào scope đọc.
Cả hai giữ kết quả nhưng không cải thiện ổn định trên cùng fixture; đã loại bỏ khỏi
code production. Bản cuối giữ nguyên query khóa của đợt trước. Các median đối chứng
32,0/32,3 ms (Developer), 46,2/46,8 ms (LabUser), 46,0/45,1 ms (SaleUser), 25,4/24,3 ms
(legacy) chỉ là dao động của query không đổi, không báo là tối ưu mới.

Bản cuối: 4 test đối chiếu paired đạt; 1.473 test hồi quy đạt, gồm quyền/unread/latest,
danh sách mọi tin có metadata/nhóm nhanh của 3 role ở cả hai chế độ và API Báo giá.
Build API đạt (0 lỗi; warning dependency/nullable hiện có). Không chạy DB thật,
không tạo migration/index, không đổi cấu hình hay restart/publish dịch vụ.

## Giới hạn vận hành

Thời gian đo bao gồm thực thi handler/service qua EF, materialization và enrichment;
không phải HTTP qua mạng hoặc thời gian render FE. Đây không phải benchmark dữ liệu
server thật. Khi publish cần xác nhận đúng DLL/process được nạp rồi xem lại Server-Timing:
feed `nm_unread_keys`, `nm_feed_page`; hội thoại `nm_unread_keys`, `nm_icons`,
`nm_quick_groups`. Không suy ra thiếu index hoặc lock từ TTFB đơn lẻ; cần plan đúng
DB, trong phạm vi đọc được cho phép, nếu sau triển khai vẫn chậm.

Tối ưu này không xử lý icon/quick-group bằng cách bỏ quyền hoặc trả số tạm. Nhánh thử
lọc sớm JSON link theo trang gây regression trên fixture canonical đã được loại bỏ.

## Tổng chưa đọc — 08/11/2026

Nhánh `LegacyUnreadCountsByTypeAsync` trong canonical group storage tách thành: khóa tin chưa đọc của employee/công ty → tập hội thoại qua đầy đủ `InboxConversations(false)`/membership/mute/quyền → khóa notification có cùng cặp conversation/message để chống trùng → cộng theo loại root hợp lệ. Chỉ giữ ID trong bộ nhớ, không tải nội dung; không cache qua request. Không tạo hoặc cập nhật read-state. Không loại trừ notification đã đọc/archived khỏi bước chống trùng vì baseline cũng xét mọi state của employee. Legacy storage giữ nguyên query cũ vì kế hoạch đơn giản cũ nhanh hơn. Không đổi cờ storage, schema hay timeout.

`UnreadSummaryPreservesRoleCountsAndMeasuresBaseline` chạy trên PostgreSQL cô lập, 160 root mẫu, 240 nhóm con và 200 hội thoại khác, 64 sự kiện mỗi hội thoại tổng hợp (~38.400 sự kiện). Fixture có lịch sử đã đọc, notification liên kết và tin cũ không có link, nhóm muted. Warm-up rồi 5 cặp xen kẽ trên cùng dữ liệu/principal. Đối chiếu dictionary từng loại root và tổng của nhánh bổ sung, không chỉ số dòng; nhánh notification và mention của summary giữ nguyên. Đây là phép đo nhánh legacy fallback, không phải toàn bộ thời gian HTTP API summary.

| Role / canonical | Trung vị cũ → mới (ms) | Min–max cũ | Min–max mới |
| --- | --- | --- | --- |
| President | 2516,33 → 286,35 | 2482,50–2914,82 | 279,72–614,28 |
| Developer | 4133,28 → 278,50 | 2806,94–6750,57 | 276,21–602,93 |
| LabUser | 3558,64 → 299,13 | 2614,40–4226,57 | 282,79–513,44 |
| SaleUser | 2501,86 → 279,57 | 2477,57–3841,47 | 278,01–393,85 |

Cả 4 role khớp baseline: 60 SampleRequest + 29 Internal = 89 tin fallback trên fixture. Không suy rộng số này thành số chưa đọc thực tế. Artifact tái tạo tại `artifacts/unread-summary-comparison`. Test còn kiểm tra nhánh không còn read-state chưa đọc. Server-Timing mới `nm_summary_legacy` giúp phân biệt bước này với tổng `nm_handler` khi xác minh server sau cập nhật.

Lượt kiểm tra bổ sung gọi toàn service `GetUnreadSummaryAsync` thật và đối chiếu JSON đầy đủ với bản composition baseline trước tối ưu (total/categories/eventGroups/unreadMentionCount). Cả 5 cấu hình role/storage khớp, gồm President legacy; nhánh không còn unread cũng khớp rỗng. Một lượt service sau warm-up trong lượt kiểm tra này: President canonical 2554,57 → 357,31ms; Developer 2485,25 → 355,08ms; LabUser 4769,62 → 721,22ms; SaleUser 4553,39 → 635,78ms. Đây là số mẫu đơn minh họa, không gọi là median/p95 hay thời gian HTTP server thật. Các lần chạy lại có dao động do tải máy. Nhánh legacy giữ nguyên SQL: median 202,91 → 204,70ms (dao động đo). Hồi quy quyền/đọc/timing: 1.538 test đạt; kiểm tra JSON đầy đủ bổ sung: 5 case đạt.
