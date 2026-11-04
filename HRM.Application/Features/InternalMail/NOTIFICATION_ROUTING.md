# Notification audience và @mention

Membership quyết định quyền đọc lịch sử khu vực. Audience quyết định Notification/feed, badge và push.
Default membership nhóm Kỹ thuật gồm LabUser, Leader Lab theo sản phẩm và President active cùng company;
nhóm Giá vẫn có President. Dùng API `areas/ensure-defaults` theo hồ sơ để thêm membership còn thiếu vào nhóm
đã tồn tại; không tự bật lại người đã bị loại, không hồi tố thông báo của các tin trước khi đồng bộ.
Tin do người dùng gửi qua API messages (kể cả forward) trong Kỹ thuật/Giá/nhóm Custom tạo notification chưa đọc
cho mọi thành viên active, đúng company/eligibility, trừ sender, không cần @tag. Mute không loại khỏi inbox/badge;
dispatch vẫn chặn SignalR/Web Push cho người mute trừ @tag hợp lệ theo rule hiện hành. Người mute tải lại inbox để
thấy badge mới. Không tự thêm observer vào membership. Developer active cùng công ty được NotificationService bổ sung vào audience mọi event mới; President chỉ theo audience nghiệp vụ. Tin hệ thống/workflow,
tin ở Chung và các audience nghiệp vụ bên dưới giữ nguyên; không áp dụng broadcast cho chúng.
Developer có capability riêng để đọc/gửi toàn công ty mà không cần membership (xem README/AREAS).
Khi Dev gửi bằng ngoại lệ không có membership, publish dùng exact audience đã resolve theo khu vực/nghiệp vụ,
vẫn loại sender và tôn trọng mute/@tag khi dispatch; NotificationService bổ sung Developer monitoring vào audience, không tạo membership.
Rule dưới đây áp dụng cho sự kiện Sample Request mới; initial message khi tạo hồ sơ giữ luồng tạo hồ sơ.
Không xóa notification cũ, không migration/backfill. Hồ sơ nội bộ giữ `SampleRequestMessageRules`.

| Sự kiện | Audience mặc định |
| --- | --- |
| Tin nhắn thường, có hoặc không tag | Leader Lab theo mapping sản phẩm hiện hành |
| Lab bắt đầu xử lý | Sale + SaleAdmin |
| Lab xác nhận công thức / đổi NVL cần duyệt giá, yêu cầu báo giá | President + Sale + SaleAdmin |
| Lab gửi mẫu, đề nghị đổi công thức, có giá tham khảo | Sale + SaleAdmin |
| Chấp nhận / từ chối / hủy đề nghị đổi công thức; hủy hồ sơ; phản hồi khách hàng | Leader Lab + LabAdmin |
| Sale sửa trực tiếp thông tin, hoàn tất công thức, đề nghị / duyệt / từ chối sửa dữ liệu | Không có audience mặc định |

Sale lấy mọi CustomerAssignment active đúng customer/company, customer/employee active. SaleAdmin phải có role
và membership active trong GroupId của assignment. Không fallback sender/ManagerBy hoặc broadcast khi thiếu
assignment. Leader Lab dùng mapping `SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes` hiện có và
MemberInGroup.IsAdmin. LabAdmin/President là role toàn company, user-role active. Role sets ở
ApplicationRoleSets.Notifications. Audience lọc eligibility sản phẩm/khu vực, company, active, sender và mute.
Audience nghiệp vụ không tự mở rộng; NotificationService bổ sung Developer active cùng công ty cho mọi notification mới, kể cả exact audience.

## API tag tên

POST `/api/v1/internal-mail/conversations/{id}/messages`:

```json
{
  "body": "@Nguyễn Nam kiểm tra giúp",
  "mentionedEmployeeIds": ["11111111-1111-4111-8111-111111111111"],
  "isUrgent": false
}
```

Multipart cùng route dùng trường lặp mentionedEmployeeIds cùng body/files/replyToMessageId/isUrgent.
BE không suy ID từ tên; tối đa 50 ID khác nhau, không GUID rỗng. ID phải là participant active có quyền trong
đúng khu vực và employee active cùng company. Tag không mời/cấp quyền. Forward không mang tag nguồn.
Sender không nhận self-notification. Command gửi tin SampleRequest/direct-patch/data-change/formula-change
nhận cùng field. Audience cuối = audience nghiệp vụ cộng tag hợp lệ, loại trùng. Sự kiện không phát mặc định
vẫn báo tag. Người nhận thường tôn trọng mute; tag vượt mute ở publish/dispatch, không vượt revoke, archive
notification, company hoặc giới hạn sản phẩm/khu vực.

PATCH `/messages/{id}`: thiếu/null mentionedEmployeeIds giữ nguyên; [] bỏ tag. Chỉ sender được sửa theo contract
hiện hành. Chỉ employee chưa từng được báo bằng tag của tin đó nhận thông báo mới; remove/add lại không phát
lặp. Edit khóa conversation, cập nhật payload/publish cùng transaction và giữ PayloadJson concurrency token.
Workflow payload được giữ, không thực thi lại action.

Response message tiếp tục trả payloadJson, bổ sung:

```json
{
  "notificationAudienceVersion": 1,
  "mentions": [{"employeeId":"11111111-1111-4111-8111-111111111111","displayName":"Nguyễn Nam"}],
  "notifiedMentionEmployeeIds": ["11111111-1111-4111-8111-111111111111"]
}
```

displayName là snapshot tên từ BE. mentions=[] là không còn tag. notifiedMentionEmployeeIds là lịch sử chống
phát lặp, không phải audience hiện tại. Notification thêm deliveryMentionEmployeeIds do server ghi để bypass
mute tại dispatch của đúng notification; client không đặt delivery policy. Không có giá/cost/margin mới.

## Badge, đọc và rollout

Message mới có notificationAudienceVersion=1 không đóng góp trực tiếp vào unreadCount list/detail/areas/Hub
hoặc unreadOnly; badge lấy notification unread của employee. Message read-state độc lập, cập nhật khi thực sự
mở/đọc. Người không được báo vẫn xem message. Message lịch sử thiếu marker giữ max(message unread, notification
unread). Không đánh dấu giả đã đọc hoặc sửa dữ liệu lịch sử hàng loạt.
SignalR chỉ gửi notificationId qua outbox; Web Push giữ payload tối thiểu. Edit có tag mới dùng cùng luồng.
Người không nhận tín hiệu tải lại/mở hội thoại để thấy tin mới.

Triển khai BE trước, FE hrm-mobile sau. Client cũ thiếu tag vẫn gửi được nhưng dùng audience mới. Rollback BE
cũ có thể khôi phục broadcast và badge cũ, không thu hồi notification đã gửi. Không chạy SQL/backfill hoặc đổi
topic numeric/catalog khi rollout.
