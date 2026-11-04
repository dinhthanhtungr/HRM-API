# Cảm xúc tin nhắn

Mỗi employee lưu tối đa một cảm xúc trên một message trong `InternalMail.InternalMessageReactions`.
Khóa chính ghép `(MessageId, EmployeeId)` ngăn trùng; `ReactionType` là 1 Heart, 2 Like, 3 Laugh,
4 Surprise, 5 Sad, 6 Angry. `CreatedAt` là lần thả đầu; `UpdatedAt` chỉ đổi khi đổi loại.
Xóa cảm xúc là xóa dòng của chính người gọi; không xóa tin nhắn hoặc lịch sử đọc.

## API và quyền

Các route đều yêu cầu đăng nhập, company scope và quyền xem chính conversation/message.
Mutation còn yêu cầu quyền gửi trong nhóm, dùng resolver hiện hành; không tự thêm membership.
President chỉ thao tác khi có quyền nhóm, Developer giữ capability hiện hành. Không được thả vào tin đã xóa.

Prefix: `/api/v1/internal-mail/conversations/{conversationId}/messages/{messageId}`.

- `PUT /reaction` body `{"reactionType":1}`: đặt/đổi cảm xúc của người gọi. Lặp cùng loại không tạo thêm dòng,
  không đổi thời gian hoặc tạo thêm outbox. Trả OperationResult với `data` là summary.
- `DELETE /reaction`: bỏ cảm xúc của chính người gọi; gọi lại là thành công không thay đổi.
- `GET /reaction`: summary `{"items":[{"reactionType":1,"count":2}],"myReaction":1}`.
- `GET /reactions?pageNumber=1&pageSize=30`: PagedResult gồm `employeeId`, `employeeName`, `reactionType`,
  `createdAt`, `updatedAt`; pageSize tối đa 100. Không có quyền/tin đã xóa trả 404 ở GET; mutation trả lỗi nghiệp vụ.

Thread và message context có `reactionsEnabled`, `canReact` và `reactions` cùng cấu trúc summary.
`myReaction:null` nghĩa là chưa thả; `items:[]` nghĩa là không có cảm xúc được phép hiển thị.
Counts và danh sách đều loại Developer với người không có capability observer, kể cả Developer inactive
còn active role, theo quy tắc ẩn Dev của danh sách thành viên. Tên tác giả tin nhắn không thay đổi.
Không trả email, tài khoản hay dữ liệu nhân viên khác. Summary tải theo lô của trang, không gọi API từng tin.

## Realtime và bất biến

Mutation khóa conversation cùng cơ chế hiện có. Xóa tin cũng khóa trước khi kiểm tra lại để không ghi reaction
sau khi tin bị xóa. Reaction và outbox riêng `InternalMailReactionChanged` lưu trong cùng transaction.
Worker dispatch SignalR `internalMailReactionChanged` chỉ với `{conversationId,messageId}`;
kiểm tra lại active membership, Developer/company scope, RD/Pha màu và root trước khi gửi tín hiệu.
FE chỉ tải lại summary của tin bị thay đổi, không xử lý như notification mới.

Reaction của người khác tạo một notification cá nhân gộp theo tin và tác giả để hiện trong @Tôi.
Các topic append-only: 58 `plm.sample_request.message.reacted`, 59 `crm.quotation.message.reacted`,
60 `internal_mail.message.reacted`, cùng event group `reaction` và category của hồ sơ nguồn.
NotificationService đồng bộ trong transaction mutation: người mới thả thêm làm nhóm chưa đọc và cập nhật
thời gian hoạt động cá nhân; đổi emoji chỉ cập nhật tổng hợp, không phát notification/push mới.
Lịch sử actor đã báo được lưu riêng trong payload nội bộ `seenReactionActorIds`, bị loại khỏi mọi DTO public.
Người đã thả bỏ rồi thả lại chỉ khôi phục nhóm, không reset đã đọc, thời gian cá nhân hoặc phát push lại.
Bỏ cảm xúc cập nhật số người/cảm xúc; hết người hợp lệ thì archive state. Tự thả không báo chính mình.
Dev ẩn không tạo notification hay làm đổi số đếm cho tác giả không phải Dev.
Payload chỉ có metadata cá nhân cần thiết: `kind=messageReaction`, `personalActivityAt`, `reactionActorCount`,
`reactionCounts`, preview 100 ký tự và context/thread IDs chuẩn. Link mở `/notifications?mentionedOnly=true&conversationId=...&messageId=...`.
Chỉ tác giả có quyền đọc hiện tại nhận state; không tự thêm Developer monitor hoặc thành viên khác.
Tên/count được dựng lại khi đọc theo quyền và role hiện tại, không tin snapshot actor cũ sau khi đổi sang Developer.
Mute giữ inbox cá nhân nhưng dispatch chặn push theo cơ chế hiện hành.
Notification reaction không tham gia Tất cả, latest hoạt động hồ sơ hoặc badge tin nhắn; @Tôi có hoạt động
và số chưa đọc cá nhân riêng. Không sửa message read-state, `SentAt`, `LastMessageAt` hoặc thứ tự hồ sơ chung.
Cảm xúc không được copy tự động khi forward tin.

## Triển khai schema do người vận hành thực hiện

Code chỉ thêm entity, configuration và DbSet. Không có migration hoặc tự update database.
`InternalMail:Reactions:Enabled` mặc định `false`; lúc tắt thread không query bảng mới, UI ẩn thao tác,
summary rỗng và mutation bị từ chối. Deploy code trước migration vì thế không làm thread lỗi thiếu bảng.

1. Tự sinh migration từ model tại project/context quản lý DB; kiểm tra chỉ tạo bảng, PK/FK/index/check constraint dự kiến.
2. Áp dụng migration vào đúng database API/worker sử dụng.
3. Bật cấu hình `"InternalMail":{"Reactions":{"Enabled":true}}` (gộp với cấu hình hiện có), restart API/worker.
4. Kiểm tra thả/đổi/bỏ bằng hai tài khoản; badge tin nhắn/thứ tự hồ sơ chung giữ nguyên, @Tôi cập nhật cá nhân.

Không cần đồng bộ/backfill cảm xúc cũ. Có thể tắt cờ để ngừng feature mà không xóa dữ liệu.
