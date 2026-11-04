# Chat / Notification regression tests

## Phạm vi và kết quả

Bổ sung **882 ca tự động theo ma trận**, không phải 882 luồng E2E độc lập. Mỗi dòng dữ liệu được xUnit/Vitest chạy và báo kết quả riêng. Không dùng hoặc lưu thông tin đăng nhập thật; không gửi tin vào hồ sơ nghiệp vụ.

| Bộ mới | Số ca | Điều kiện được kiểm tra |
|---|---:|---|
| BE `InternalMailAreaAccessTests.ChatMatrix.cs` — quyền đọc | 480 | 5 vai trò × 3 nhóm riêng × thành viên/không × đúng/khác công ty × hồ sơ hoạt động/khóa × nhóm hoạt động/khóa × đã/chưa xác thực. Đọc nhóm và tin phải cùng phạm vi; observer không tự trở thành thành viên. |
| BE — nhận thông báo | 64 | Tag, mute, membership bị thu hồi, inbox archive, nhân viên ngừng hoạt động, khác công ty. Tag chỉ vượt mute. |
| BE — trả lời trong `@Tôi` | 32 | Membership, tin bị xóa, tự gửi, trả lời tin của mình/người khác, cùng/khác hội thoại. |
| BE — giới hạn tag | 12 | 0/1/2/50/51/100 người, có/không ID trùng. Đếm theo người phân biệt, tối đa 50. |
| BE `InternalMailChatAttachmentMatrixTests.cs` | 60 | 15 đuôi tệp × nguồn Chat/SampleRequest × chữ hoa/thường. MIME, loại, URL được bảo vệ, chỉ ảnh có thumbnail. |
| FE `NotificationChatMatrix.test.ts` — gửi | 144 | 3 khu vực × 4 kiểu tệp × 0/1/2 tag × có/không trả lời × gấp/thường. JSON hoặc multipart giữ đủ dữ liệu và nội dung tệp. |
| FE — lỗi gửi | 24 | 3 khu vực × 4 kiểu tệp × lỗi mạng/hủy; không tự gửi lại. |
| FE — sửa | 6 | Bỏ field mention/clear/add × gấp/thường; PATCH giữ đúng ý nghĩa. |
| FE — đánh dấu đọc | 6 | 3 khu vực × có/không mốc tin; chỉ gửi yêu cầu cho đúng nhóm. |
| FE — tùy chọn | 4 | Bật/tắt mute và archive; không đổi field khác. |
| FE `notificationMultiActorReply.test.ts` | 50 | 5 người gửi × 5 người trả lời × còn/không còn trong nhóm. Không tự tag, không tag người ngoài nhóm, hủy tag tự thêm thì bỏ reply theo contract. |
| **Tổng mới** | **882** | **648 BE + 234 FE** |

Vai trò giả lập: President, Developer, LabUser, PLPUUser, SaleUser. Quyền quan sát của President/Developer ở bộ 480 chỉ áp dụng con của SampleRequest; không suy ra quyền gửi, quyền xóa hay quyền xem mọi nhóm nội bộ. Tên vai trò ở bộ FE chỉ phân biệt các actor; việc phân quyền được kiểm tra ở BE.

Lần chạy ngày 2026-10-30:

- BE phạm vi InternalMail + NotificationHub + SampleRequestNotification: **840 đạt, 2 bỏ qua, 0 lỗi**.
- FE toàn bộ `src/core/notifications`: **349 đạt, 0 lỗi**, 18 file.
- Tổng hồi quy: **1.189 đạt**, gồm 882 ca mới và 307 ca có sẵn; 2 ca PostgreSQL bỏ qua vì thiếu `HRM_NOTIFICATION_TEST_CONNECTION`.
- FE TypeScript và production build đạt. BE test command đã biên dịch Domain/Application/Infrastructure/test project.
- Dependency ImageSharp đang có cảnh báo NU1902/NU1903 có sẵn; task này không đổi dependency.

## Các hành vi đã có bộ hồi quy đi kèm

| Chức năng | Bộ kiểm tra có sẵn được chạy |
|---|---|
| Tag Unicode, ranh giới tên, không suy đoán ID từ chữ gõ; metadata và lịch sử người đã nhận | `InternalMailMentionsTests`, `InternalMailMentionResolverTests`, FE `notificationMentions.test.ts` |
| Xóa/thu hồi theo quyền hiện hành, không tự coi tác giả là có quyền xóa | `InternalMessageDeletionAccessTests` |
| Forward không mở rộng người xem, default membership, mapping RD/màu, observer | `InternalMailAreaAccessTests.Groups.cs` |
| Hover không gọi API, chuẩn bị nhóm khi click | BE `InternalMailAreaAccessTests.QuickGroups.cs`, FE `useNotificationQuickGroups.test.ts`, `PrepareInternalMailQuickGroupsUseCase.test.ts` |
| Tạo/forward nhóm và tải người có thể mời — transport contract | FE `InternalMailGroupsApiRepository.test.ts` |
| Chưa đọc, receipt, mốc tin, company scope, không đánh dấu nhóm khác | `InternalConversationReadScopeTests`, `InternalConversationNotificationUnreadTests`, `InternalConversationReadPersistenceTests`, NotificationHub tests |
| Nhảy đúng tin từ notification, điều phối toast/browser alert | FE `notificationAlertNavigation.test.ts`, `notificationAlerts.test.ts`, `notificationAlertWorker.test.ts` |
| Người nhận sự kiện phối mẫu | `SampleRequestNotificationRecipientRulesTests`, `SampleRequestNotificationAudienceTests`, `SampleRequestNotificationAudienceResolverTests` |

## Cách chạy lại

Tại repo BE:

```powershell
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --filter "FullyQualifiedName~Features.InternalMail|FullyQualifiedName~Features.NotificationHub|FullyQualifiedName~SampleRequestNotification" -p:OutDir=..\artifacts\verify-chat-matrix\ --no-restore --logger "trx;LogFileName=chat-regression.trx" --results-directory artifacts\chat-test-results
```

Tại `F:\UserInterface\hrm-mobile`:

```powershell
npx.cmd vitest run src/core/notifications --reporter=default --reporter=json --outputFile=artifacts/chat-test-results/notification-vitest.json
npx.cmd tsc --noEmit --incremental false
npm.cmd run build
```

TRX/JSON chứa từng ca và trạng thái; báo cáo CSV tổng hợp 882 ca mới ở `artifacts/chat-test-results/new-chat-cases.csv` trong repo BE. Báo cáo là artifact của lần chạy, không phải dữ liệu nghiệp vụ.

## Chưa xác minh trên hệ thống thật

Các test trên dùng query fixture trong bộ nhớ, kiểm tra SQL translation có sẵn và mock HTTP/browser. Chúng **không chứng minh** mật khẩu, JWT thực tế, dữ liệu gán role của các tài khoản, PostgreSQL execution, SignalR end-to-end, upload thật, quyền SMB hay hiệu năng production hoạt động đúng. Tệp ảnh/PDF ở FE chỉ là fixture kiểm tra transport, không phải kiểm tra giải mã ảnh/PDF. Không tuyên bố đã test mọi thao tác UI.

Checklist UAT dưới đây **chưa chạy**; dùng hồ sơ và nhóm kiểm thử tách biệt, không dùng hội thoại nghiệp vụ:

| Ca UAT | Bước / kết quả cần xác nhận |
|---|---|
| Đăng nhập 5 vai trò | Phiên riêng, đúng company/employee/role; logout không dùng lại cache người trước. |
| Chung / Kỹ thuật / Giá | Chỉ hiện nhóm được quyền; President/Developer quan sát theo contract; đọc không tự cấp quyền gửi. |
| Mời / gỡ thành viên | Người được mời thấy nhóm, có tin hệ thống thêm người; người bị gỡ mất quyền ở phiên đang mở. |
| Tag một / nhiều người | Dropdown, bàn phím, tên Unicode, chip inline; chỉ đúng ID được gửi. |
| Tag khi mute / ngoài nhóm | Tag vượt mute nhưng không vượt quyền hoặc archive; self-tag không tự thông báo. |
| Reply / `@Tôi` | Trích dẫn đúng tin; nhận reply của người khác xuất hiện `@Tôi`; mở đúng nhóm và đúng tin. |
| Sửa tag | Thêm mới chỉ báo người mới; bỏ tag không gửi lại; giữ tag cũ không phát trùng. |
| Gửi ảnh / tệp | Ảnh thật, nhiều ảnh, PDF, ảnh+tệp, chỉ tệp không chữ; xem thumbnail, tải xuống; kiểm tra giới hạn 10 tệp/50 MB và loại bị cấm. |
| Storage lỗi / mất quyền | Báo lỗi phù hợp khi tệp không còn, storage mất kết nối hoặc người dùng mất quyền; không lộ đường dẫn. |
| Forward | Cùng/khác nhóm, tạo nhóm riêng, nhiều tin và tệp; không mở rộng đối tượng vượt quyền nguồn. |
| Xóa / thu hồi | Theo capability hiện hành, tombstone và trích dẫn; không còn nội dung qua attachment route. |
| Đọc / chưa đọc / gấp | Số riêng từng nhóm; tin đến trong lúc đọc không bị đánh dấu nhầm; lọc đúng. |
| Tìm kiếm / nhảy tin | Tin cũ, tin bị xóa, từ có dấu, context pagination; không lấy tin nhóm khác. |
| Realtime nhiều phiên | Hai trình duyệt, reconnect, background/foreground, nhiều tab; không trùng thông báo hay mất tin. |
| Soạn thảo | Enter/Shift+Enter, dán văn bản, emoji, IME tiếng Việt, bản nháp, đổi nhóm khi đang soạn/gửi. |
| Lỗi mạng | Chậm, hủy, timeout, hết phiên, reconnect; không báo thành công giả hoặc tạo tin trùng khi retry. |
| Responsive / motion | Mobile/tablet/desktop, bàn phím mở, cuộn, sticky ngày, reduced-motion; hover không phát request. |
| Hiệu năng | Đo feed, mở nhóm, tìm kiếm với dữ liệu lớn; các unit test không thay thế số đo latency. |

Không nhập thông tin đăng nhập thật vào fixture, snapshot, command line hoặc báo cáo. Khi bật PostgreSQL integration, dùng biến môi trường của database kiểm thử riêng và không in giá trị kết nối.
