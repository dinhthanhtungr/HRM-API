# Khu vực trao đổi theo người tham gia

## Phạm vi và lưu trữ

Ba khu vực: `general` (Chung, gồm mẫu và phản hồi), `technical` (Kỹ thuật), `pricing` (Báo giá). Conversation gốc là Chung. Mỗi khu vực riêng là một conversation con, có participant, message, read state, mute/archive riêng. FE nhóm chúng dưới một dòng hồ sơ bằng `groupConversationId`.

Không thêm entity, bảng, cột, migration, claim hoặc role mới. Bổ sung `InternalMailRelatedType.ConversationTechnical = 17` và `ConversationPricing = 18`; `RelatedId` của chúng trỏ về conversation gốc. `InternalMailAreaAccessService.AreaOf` ánh xạ sang mã khu vực. DTO trả `relatedType/relatedId` nghiệp vụ của gốc để FE vẫn nhận diện hồ sơ; chỉ entity conversation con dùng liên kết parent.

Lịch sử, file nghiệp vụ và publisher tự động hiện hành vẫn ở Chung. Không tự phân loại hoặc chuyển tin cũ sang khu vực riêng; không backfill hoặc đánh dấu đọc hàng loạt. Tin mới do người dùng gửi ở tab riêng nằm trong conversation của tab đó. Đây là chia nhóm trao đổi thủ công, không tự động che các thông báo nghiệp vụ vốn đang gửi ở Chung.

## Quyền và người nhận

- Active participant đọc toàn bộ lịch sử và gửi trong chính khu vực đó, không phụ thuộc Sale/Lab. Chủ khu vực quản lý mời/gỡ người. Người được mời không tự vào Chung hoặc khu vực khác.
- Chỉ chủ conversation gốc tạo khu vực; người tạo trở thành Owner của khu vực mới. Chủ gốc không tự đọc khu vực đã tồn tại nếu không còn là thành viên khu vực đó. Tạo lại trả khu vực hiện có nếu người gọi được vào, không ghi đè danh sách người nhận.
- Company, conversation/parent active, ownership và IDOR được kiểm tra tại BE. Role President/Developer không mở được khu vực riêng khi không có membership. Ngoại lệ executive và quản lý participant của conversation gốc vẫn theo contract cũ.
- Luật leader active nhóm `QAQC.RD` không xem hồ sơ màu PIG/CMB/PHM/PBM/PDM tiếp tục áp dụng cả conversation con; role bổ sung không vượt qua luật này.
- NotificationService chỉ nhận thành viên active cùng company của khu vực riêng, không auto-add Developer. Sender và mute được loại theo send flow; silent recipients giữ semantics đã đọc/không push hiện hành. Thu hồi membership chặn đọc/tìm kiếm/file/feed/unread ở request tiếp theo, không thu hồi bản nội dung người nhận đã tải trước đó.
- Mời vào khu vực cho xem lịch sử; chuyển tiếp chỉ sao chép body/file của tin được chọn, không thêm membership, không sao chép reply chain hay action payload. Bản sao độc lập với tin nguồn; không cấp quyền mở hồ sơ/duyệt giá tại module nghiệp vụ.

File chat qua API attachment dùng chung cũng lọc bằng `InternalMailAreaAccessService.Attachments`, kể cả URL nội dung và list collection. Không tải được file chat mồ côi/thuộc tin bị xóa hoặc khu vực không được mời. Upload/xóa file chat phải qua message endpoint; generic upload từ chối collection chat và generic delete không sửa file chat. File nghiệp vụ không gắn tin giữ contract hiện hành.

## API

Base: `/api/v1/internal-mail` (giữ `[Authorize]`).

| API | Contract |
| --- | --- |
| POST `/conversations/{id}/areas` | `{ "areaCode": "technical", "employeeIds": ["..."] }`; pricing tương tự. Danh sách trống tạo khu vực chỉ có chủ để mời sau. |
| GET `/conversations/{id}` | Thêm `groupConversationId`, `areaCode`, `canManageParticipants`, `areas: [{code, conversationId, canSend, unreadCount, canCreate}]`. Chỉ trả khu vực được phép; khu vực chưa tạo có thể có ID null và canCreate=true cho chủ gốc. |
| POST `/messages/{messageId}/forward` | `{ "targetConversationId": "..." }`; phải đọc được nguồn, gửi được đích, cùng hồ sơ và khác conversation. Body lấy từ DB, file sao chép qua storage; giữ giới hạn upload hiện hành. |
| GET messages/search/context/attachments | Dùng **conversationId thực của khu vực**, `areaCode` tùy chọn; không dùng ID gốc cộng area để truy cập con. |
| POST messages/read | Dùng ID khu vực thực; areaCode nếu gửi phải khớp. Read giữ boundary message và snapshot ID, chỉ cập nhật khu vực đang đọc. |
| participants/preferences | Endpoint hiện hành với ID khu vực; chủ khu vực mời/gỡ thành viên, mỗi người tự mute/archive. |

List và Hub conversation info thêm `groupConversationId`, `areaCode`. Tổng của dòng FE cộng unread từng conversation truy cập được; mỗi conversation đối soát message/notification bằng logic cũ, tránh đếm đôi. Area code suy ra từ loại conversation, không thêm cột trên message/notification. Không thay đổi numeric TopicNotifications, topicCode/category hoặc NotificationTopicCatalog. Generic send/forward vẫn qua `SendInternalMessageCommandHandler` → `INotificationService.PublishAsync`; business publisher cũ giữ luồng Chung.

Payload chat giữ conversationId/messageId/business reference/urgent/attachmentCount; forward thêm `isForwarded: true`, không mang link nguồn hoặc action payload. Body/file có thể nhạy cảm và chỉ được tải qua API kiểm tra membership. SignalR chỉ gửi notificationId; Web Push dùng nội dung chung. Outbox dùng employee đã resolve, giao với audience hiện tại trước dispatch; không bổ sung silent watcher bằng role broadcast.

## Triển khai và kiểm chứng

Triển khai/restart BE rồi cập nhật/reload FE. Không chạy SQL, migration hoặc refresh claim. FE cũ vẫn đọc/gửi Chung khi bỏ areaCode; để dùng ba khu vực cần FE mới. Không triển khai lại BE cũ sau khi tạo khu vực con vì code cũ không hiểu loại 17/18 và các ràng buộc khu vực.

Regression: người Sale được mời kỹ thuật và người Lab được mời giá đều đọc được; President/Developer không được mời thì không đọc; parent owner không thừa kế quyền con; thu hồi membership, sai company/parent inactive đều bị chặn. Provider tests kiểm tra SQL PostgreSQL dịch được và model không thêm cột; không thay thế kiểm thử DB/runtime. FE tests kiểm tra payload area/create/forward, nhóm conversation, badge và read boundary.

Kiểm thử thủ công sau restart: chủ gốc tạo Kỹ thuật, mời A; tạo Báo giá, mời B; A không đọc/tải file Báo giá bằng ID trực tiếp; forward một tin từ Kỹ thuật sang Báo giá chỉ hiện bản sao; gỡ B chặn truy cập và unread của B. Đọc hết một khu vực không xóa unread khu vực còn lại. Kiểm tra lại push queue sau revoke và mẫu màu với leader RD.

Kiểm chứng ngày 2026-10-04: build API qua, 0 warning/error; 162 test notification FE qua và Next production build/TypeScript qua. 57 test BE tập trung qua. Test project khi biên dịch lại bị chặn bởi hai file ngoài phạm vi: `QuotationSnapshotRegressionTests.cs` (argument quantity/fieldPath không khớp factory) và `ManufacturingLossProfileApplicationResolverTests.cs` (property Code không còn tồn tại). Lượt test tập trung dùng MSBuild target tạm trong `artifacts/verify-tests` để loại đúng hai file, không sửa project hoặc test ngoài phạm vi. Không coi lượt test incremental trước đó là full-project compile pass. Lint FE không có error, còn 6 warning unused của code có sẵn. Chưa thực hiện thao tác tạo khu vực/gửi tin trên DB thật trong phiên sửa code.
