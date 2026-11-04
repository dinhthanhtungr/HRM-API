# Nhóm trao đổi trong hồ sơ

## Lưu trữ và phạm vi

Một yêu cầu phối mẫu giữ một dòng inbox theo `groupConversationId`. Chung là conversation gốc;
Kỹ thuật, Giá và nhóm tùy chỉnh là các conversation con, mỗi nhóm có thành viên, tin, file,
read-state, mute và archive riêng. Lịch sử và publisher nghiệp vụ cũ giữ ở Chung.

Luồng runtime hiện tại vẫn tái sử dụng conversation/participant/message và RelatedType lưu int:
17 = ConversationTechnical, 18 = ConversationPricing, 19 = ConversationCustom;
RelatedId của nhóm con trỏ về gốc. Loại 20 = ConversationForwardSource chỉ dùng trên
InternalMessageReference, trỏ về conversation nguồn của bản chuyển tiếp.

Đợt 2 bổ sung model bảng nhóm và bộ đọc để đối chiếu, chưa chuyển endpoint sang nguồn mới và chưa áp dụng SQL
vào DB đang dùng. Bảng mới chỉ được tạo bằng script thủ công có guard và backfill đã được duyệt;
xem [group storage phase 2](design/group-storage/PHASE2.md). Không tạo lại thành viên, tin nhắn hoặc badge.

## Quyền

### Lựa chọn nhanh trong inbox

`GET /api/v1/internal-mail/conversations` trả `quickGroups` trên từng item; `GET /api/v1/notification-hub/items`
trả cùng field trong `conversationInfo`. Ví dụ: `"quickGroups": [{"code":"technical","conversationId":null}]`.
Danh sách chỉ gồm Kỹ thuật/Giá được current user phép mở hoặc đủ điều kiện default khi nhóm chưa tồn tại.
ID khác null là nhóm đã lưu và hiện được phép đọc; null là preview chưa ghi DB, bấm mới gọi ensure-defaults
rồi đọc lại detail để lấy ID được phép truy cập. `[]` nghĩa không có lựa chọn nhanh; không suy ra quyền từ role ở FE.
Nhóm đã tồn tại nhưng không có quyền không được thay bằng preview; membership bị gỡ không được tái kích hoạt.
Đây chỉ là metadata điều hướng, không chứa nội dung, participant, giá hoặc unread giả. Detail giữ unread canonical.
Query chạy theo lô root ID của trang: hai lượt lấy root/child, tối đa hai lượt bổ sung role/group hiện tại nếu
còn nhóm cần preview; không query theo từng dòng, không load tin nhắn hay ghi dữ liệu trong GET/hover.
Scope company/active và quyền đọc dùng `InternalMailAreaAccessService.Conversations`; default preview dùng
role sets và phân loại leader hiện hành. Endpoint mở/gửi vẫn kiểm tra lại quyền khi dữ liệu danh sách đã cũ.
Triển khai/restart BE trước FE; refresh inbox để nhận metadata mới.

- Chung giữ contract hiện có.
- Developer có quyền hệ thống `internal-mail.company-conversations.observe`, đọc mọi conversation active cùng company
  (Internal/Quotation/Sample Request và nhóm con có parent active), kể cả không có membership. Quyền gắn với role Developer,
  hỗ trợ token explicit cũ; claim đơn lẻ không cấp cho role khác. Không tạo participant hay cấp quyền mời từ quyền đọc.
  Chỉ Developer có capability riêng `internal-mail.company-messages.send`: gửi vào mọi nhóm active được phép đọc cùng
  company, không cần membership; API gửi và `areas[].canSend` dùng chung rule. Không tự thêm thành viên hoặc cấp quyền quản lý.
  Với người không phải Developer, BE loại role Developer active khỏi participants/candidates/readerGroup và số thành viên
  ở mọi khu vực; Developer thấy danh sách đầy đủ. Tác giả và nội dung tin Dev gửi vẫn hiện bình thường.
- Chỉ Developer được bypass membership trong Chung/Kỹ thuật/Giá/custom của công ty. President phải có participant
  active trong nhóm, không dùng claim observe/send cũ để vượt membership. Default members President vẫn theo rule hiện tại.
- Giá mặc định gồm SaleUser/SaleAdmin active trong Chung và President active cùng company.
- Kỹ thuật gồm LabUser, President active cùng company và leader active của group theo
  SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes: RD/màu theo danh mục sản phẩm.
  Luật loại leader RD khỏi hồ sơ màu hiện hành vẫn áp dụng.
- POST ensure-defaults tạo nhóm còn thiếu và thêm default member chưa có participant.
  Không kích hoạt lại người đã bị gỡ hoặc sửa invitation/mute/archive đã lưu.
  Đổi role về sau không tự xóa membership đã cấp.
- Developer có capability `internal-mail.pricing-group.observe` được đọc Giá của Sample Request
  cùng company, không cần participant và không tự được mời. Developer gửi bằng capability công ty riêng ở trên; capability quan sát này giữ cho token cũ;
  quyền xem mọi nhóm ở trên là capability riêng.
  Không thêm Dev vào participant qua defaults hoặc lời mời Giá, kể cả Dev có thêm role Sale/President.
  Tên/ID Dev trong participant, ứng viên mời, gợi ý
  mention và metadata người tạo ở mọi khu vực được che với người không phải Developer; CreatedBy = Guid.Empty,
  CreatedByName = null khi người tạo là Dev. Tin Dev chủ động gửi bằng membership thật vẫn hiện tác giả.
- Chỉ trả tab trong areas nếu BE cho phép đọc. Company, conversation/parent active và membership
  áp dụng cho detail/messages/search/context/file/feed/unread, không dựa vào ẩn tab ở FE.
- Thành viên active có thể mời người từ Chung, không gán Owner. Nhóm chứa tin chuyển tiếp còn
  yêu cầu người mời và người mới có quyền nguồn. Người mới đọc được lịch sử nhóm.
  Gỡ người ở nhóm riêng vẫn dành cho Owner; quyền mời và quyền gỡ là hai cờ riêng.
- Ngoài Sample Request, create-area cũ vẫn yêu cầu chủ gốc; không áp defaults phối mẫu.

## API

Base `/api/v1/internal-mail`, giữ Authorize; endpoint cũ dùng ID thật từng nhóm.

| Method/route | Contract |
| --- | --- |
| POST conversations/{id}/areas/ensure-defaults | Tạo/đồng bộ hai nhóm mặc định Sample Request; không nhận danh sách quyền từ FE. |
| GET conversations/{id} | groupConversationId, areaCode, canInviteParticipants, canManageParticipants và areas. |
| GET conversations/{id}/participants/candidates | Người được mời vào nhóm riêng, lọc membership gốc, nguồn chuyển tiếp, active/company và Dev ẩn. Không có quyền trả 404. |
| POST conversations/{id}/participants | Thêm Member/Watcher; system message @Họ tên đã được thêm vào nhóm và notification. Thêm trùng không phát lại. |
| POST conversations/{id}/groups/forward | {name, employeeIds, messageIds}: tên 1–100 ký tự, 1–20 tin cùng nguồn, 2–50 người gồm người tạo. Trả area mới. |
| POST messages/{id}/forward | {targetConversationId}: cùng hồ sơ, khác nhóm, mọi người ở đích phải có quyền nguồn. |

Ví dụ area:

```json
{"code":"custom","conversationId":"uuid","name":"Thử nghiệm nguyên liệu","canSend":true,"unreadCount":2,"canCreate":false}
```

Name là Subject lưu của nhóm; FE dùng i18n cho ba mã mặc định, name cho custom.
ConversationId là đích gửi/đọc; groupConversationId là khóa gộp inbox. Detail nhóm con trả
relatedType/relatedId nghiệp vụ của gốc để header hồ sơ và action giữ nguyên.
Người không có quyền không nhận area hoặc unread của nhóm đó. Unread mỗi nhóm đối soát
message/notification bằng max, không cộng hai bản biểu diễn cùng sự kiện.

## Chuyển tiếp và thông báo

Sao chép body/file từ DB, giữ tên người gửi trong forwardedSenderName, đặt isForwarded.
Không sao chép action payload/reply chain hoặc nhận body/quyền nguồn từ client.
References giữ mọi conversation nguồn qua nhiều lần forward; mời thêm người phải thỏa các nguồn.
Đồng bộ default members cũng lọc theo nguồn đã forward để không tự mở rộng người xem bản sao.
Tin từ nhóm riêng chỉ chuyển sang Kỹ thuật/custom; mọi đích vẫn kiểm tra membership nguồn.
Chỉ Developer được quan sát mọi nhóm trong công ty, gồm nhóm riêng;
quyền quan sát không dùng để cấp membership hay vượt kiểm tra forward cho người khác.
Bản sao đã nhận không bị thu hồi khi nguồn về sau bị gỡ thành viên hoặc xóa tin.
Forward và thay đổi membership khóa conversation gốc để kiểm tra audience trong transaction.

Tin nhóm con báo thành viên active theo mute/sender/@tag hiện hành; Developer active cùng công ty luôn được bổ sung vào inbox/outbox, không cần membership và không chịu mute/sender exclusion. Không tạo membership Dev.
Chung giữ audience nghiệp vụ riêng. Thêm người dùng topic InternalMailMessageCreated,
contentType InternalMailMembersAdded, addedEmployeeIds và mentions; người mới nhận thông báo
kể cả mute. Nội dung/membership commit cùng transaction qua PublishAsync/outbox.
SignalR chỉ báo ID, Web Push giữ nội dung chung. Notification/@Tôi mở conversationId/messageId thật.

## Triển khai và kiểm chứng

Build/restart BE trước FE. Không rollback BE về bản không hiểu loại 19/20 sau khi tạo nhóm mới.
Observer có fallback tập trung ApplicationPermissionRoleSets. Với token explicit permission,
chạy seed-pricing-observer-permission.sql trên Identity hiện có rồi login/refresh;
baseline seed/rollback đã đồng bộ. Script không tự chạy và không đổi schema.
Quyền xem mọi nhóm dùng `seed-sample-group-observer-permission.sql` cho Developer. Script cũng dọn claim bypass cũ của President; runtime role gate chặn claim cũ ngay sau restart API.
Quyền quan sát toàn công ty của Developer gắn trực tiếp với role tại resolver capability tập trung, không phụ thuộc claim mới
trong token explicit cũ. `seed-company-observer-permission.sql` và baseline seed/rollback chỉ đồng bộ metadata claim/catalog;
không tự chạy script. Thay đổi role vẫn cần refresh/login để cập nhật JWT.

Regression: defaults Sale/President/RD/màu, company/parent active, custom membership/revoke,
Dev ẩn, forward không mở rộng nguồn, mời trùng, unread độc lập và cancellation/deep link FE.
Build/unit/provider tests không thay thế kiểm tra nhiều tài khoản trên API đã restart.
