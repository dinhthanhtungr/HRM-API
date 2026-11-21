# Thông báo và hội thoại

## Message Recipients

Preview/membership bên dưới mô tả người tham gia và luồng tạo hồ sơ. Notification cho sự kiện mới dùng
[notification routing](../../../../InternalMail/NOTIFICATION_ROUTING.md). Sale nhận thông báo lấy CustomerAssignments,
không dùng ManagerBy/sender. Command gửi tin/direct-patch/data-change/formula-change nhận mentionedEmployeeIds;
tag không tự thêm participant.

All Sample Request thread message types resolve default recipients through `SampleRequestRecipientResolver`. Static role/group rules live in `SampleRequestRecipientRules`. Locked required recipients are active employees in roles `President` and `LabAdmin`, plus the active account with username `qaqcad01` in the current company. Khi sender có role `SaleUser`, mọi leader active (`MemberInGroup.IsAdmin = true`) và employee có role `SaleAdmin` cùng nằm trong chính group active của sender cũng là required recipient (`source = sales_group_leader` hoặc `sales_group_admin`); rule group-scoped này không dùng role `Leader`/`SaleAdmin` toàn công ty, loại sender và tự bỏ trùng. Default leader recipients cho Lab là active leaders trong current company và được resolved theo stable product category `ExternalId`:

- `CMP` (Compound), `PMA` (Masterbatch), `PPG` (Phụ gia), `AMB`, `VRG`, and `ADD` -> `QAQC.RD`.
- `PHM` (Hạt màu), `PBM` (Bột màu), `PDM` (Dry mix), `CMB`, and `PIG` -> `QAQC.MAU`.
- `GCO` (Gia công) -> `QAQC.RD` and `QAQC.MAU`.
- `PKH`, `PTP`, `PMS`, `PPB`, or unknown/unmapped category -> fallback `QAQC.RD` and `QAQC.MAU`.

Regular `LabUser` employees are not required recipients unless FE/user adds them or they already belong to the existing conversation. For an existing conversation, preview returns every active participant and the sample request manager, except the current sender, as locked selected recipients because real send always keeps them in the thread participant set. This keeps `selectedRecipients` aligned with the people who will actually receive the message.

Internal sample requests (`RequestType` internal or customer `KH_VIETAUS`) skip this recipient resolution during real send. Preview with an existing `contextId` also returns empty `requiredRecipients`, `suggestedRecipients`, `selectedRecipients` and `canAddRecipients = false`.

Both preview and real send use the same resolver:

```text
SampleRequestMessageRecipientResolver -> SampleRequestRecipientResolver
SendSampleRequestMessageCommandHandler -> SampleRequestRecipientResolver
```

FE can preview default recipients before submit through the generic recipient preview API:

```http
POST /api/v1/message-recipients/preview
```

Create form payload example:

```json
{
  "contextType": "SampleRequest",
  "actionType": "Create",
  "draftManagerBy": "00000000-0000-0000-0000-000000000000",
  "draftCategoryId": "00000000-0000-0000-0000-000000000000"
}
```

Existing sample request message/data-change payload example:

```json
{
  "contextType": "SampleRequest",
  "actionType": "DataChangeRequest",
  "contextId": "00000000-0000-0000-0000-000000000000",
  "selectedRecipientEmployeeIds": []
}
```

Direct patch notification preview uses the same resolver:

```json
{
  "contextType": "SampleRequest",
  "actionType": "SampleRequestDirectPatch",
  "contextId": "00000000-0000-0000-0000-000000000000",
  "selectedRecipientEmployeeIds": []
}
```

The response contains `requiredRecipients`, `suggestedRecipients`, and `selectedRecipients`. `President`/`LabAdmin`, account `qaqcad01`, active Sales-group leaders, and active Sales-group members with role `SaleAdmin` of a `SaleUser` sender are locked required recipients. For an existing thread, active participants and the sample request manager are also locked, except the current sender. Category-matched QAQC leaders are returned as default, unlocked recipients and are selected by default on the first preview. FE may remove or add these optional recipients, then submit the selected optional ids back through:

```text
CreateSampleRequestCommand.InitialLabRecipientEmployeeIds
CreateSampleRequestDataChangeRequestCommand.RecipientEmployeeIds
CreateSampleRequestDirectPatchNotificationCommand.RecipientEmployeeIds
SendSampleRequestMessageCommand.ExtraRecipientEmployeeIds
```

BE still validates all submitted recipient employee ids against the current company and active employees, and always resolves locked `President`/`LabAdmin` plus the current Sale sender's same-group leaders when FE does not send them. Existing active conversation participants remain participants so the history is not hidden from them.

For the initial preview, FE omits `selectedRecipientEmployeeIds` so category-matched leaders are selected by default. After the user edits the recipient list, FE sends the current optional ids; sending an empty array explicitly removes every optional default recipient.

### Silent watchers when creating a Sample Request

Preview request accepts `selectedSilentWatcherEmployeeIds`; response returns the validated people in
`selectedSilentWatchers` and `canAddSilentWatchers`. FE submits that same selection in
`CreateSampleRequestCommand.initialLabSilentWatcherEmployeeIds` when creating the Sample Request.

Each silent watcher is added to the new Sample Request conversation with `Role = Watcher` and `IsMuted = true`.
They can read the thread and may unmute it later through their own conversation preference. While muted, the initial
message appears in their Notification Hub as an already-read item, but does not create an
unread badge, SignalR event or Web Push alert. A silent watcher must
be active in the current company, cannot be the current creator, and cannot also be in the normal recipient list.
The initial Sample Request message uses the selected watcher ids directly, so it also creates their Hub item before
the new conversation participants have been persisted. The same rule applies to initial normal recipients; subsequent
messages use the event audience described in notification routing above. Non-target watchers keep conversation
access but do not get a silent inbox item for each later event. Ordinary notifications respect mute; explicit valid
mentions bypass mute without granting membership. Unmuting does not subscribe an employee to events outside their
business audience.
When `selectedSilentWatcherEmployeeIds` is omitted on the first preview, all active `LabUser` and `LabAdmin`
employees absent from `requiredRecipients`, `suggestedRecipients` and `selectedRecipients` are selected as default
silent watchers. Sending an explicit empty array lets FE remove those optional defaults.
The create command applies the same fallback when `initialLabSilentWatcherEmployeeIds` is omitted, so an older FE
that has not yet sent the field still adds the default Lab watchers. An explicit `[]` remains an intentional choice
to add none.

## Conversation Subject Sync

Thread InternalMail của Sample Request dùng `InternalConversation.Subject` làm tên cuộc trao đổi hiện tại.

Subject được build theo format:

```text
{SampleRequest.ExternalId}
{SampleRequest.ExternalId} - {Product.ColourCode}
```

Conversation lịch sử có thể vẫn lưu prefix `Trao đổi yêu cầu phối mẫu`. API danh sách conversation không
backfill hoặc sửa dữ liệu đó; response bổ sung `displayTitle` và loại prefix legacy tại tầng presentation.
FE dùng `displayTitle` ở cột inbox, còn màn hình chi tiết vẫn có thể dùng `subject` đầy đủ.

Khi Lab lưu `ColourCode` hoặc khi PATCH SampleRequest làm đổi product/colour code, BE gọi `SampleRequestConversationSubjectService.SyncSubjectAsync` để cập nhật đúng một conversation đang active của sample request đó. Luồng này chỉ cập nhật metadata thread hiện tại, không sửa body message cũ, notification cũ hoặc payload lịch sử.

Nếu `ColourCode` thực sự đổi sang một giá trị mới, BE đồng thời cập nhật mã snapshot và subject conversation của
các báo giá `Draft` cùng company đang dùng `ProductId` đó. Báo giá đã gửi hoặc không còn là `Draft` vẫn giữ nguyên
snapshot; message, notification và payload lịch sử không bị viết lại.

Các điểm đang sync:

```text
SendSampleRequestMessageCommandHandler: tạo conversation mới với subject có colour code nếu đã có.
PatchSampleRequestCommandHandler: Lab lưu product/colour code bằng PATCH SampleRequest, hoặc approve data-change có defer save.
```

## Lab Start Processing Notification

Khi Lab lưu thông tin làm Sample Request chuyển trạng thái từ `New` sang `InProgress`, BE tự gửi một message công khai vào thread Sample Request.

Khi **tạo mới** Sample Request, backend cũng dùng cùng rule: nếu Product đã có đủ `Name` và `ColourCode` sau khi resolve/tự sinh mã, Sample Request được tạo thẳng ở `InProgress`; nếu thiếu một trong hai thì vẫn là `New`. Rule này áp dụng cho cả Product có sẵn và Product được tạo cùng request.

## Color Chip Records

API lưu read-model phục vụ hiển thị/in phiếu Color Chip được tách tại
`Features/PLM/ColorChipRecords`. Contract GET/POST/PATCH, company scope, quyền chỉnh sửa, attachment collection và
semantics `clearFields` được mô tả trong [Color Chip Records README](../../../ColorChipRecords/README.md).


### Thread notification cho Developer

SendSampleRequestMessageCommandHandler vẫn no-op theo SampleRequestMessageRules với hồ sơ nội bộ. Với message hợp lệ,
luôn gọi PublishAsync dù audience nghiệp vụ rỗng để Developer active cùng công ty nhận event. Người nhận thông thường
vẫn theo audience/mute/tag hiện hành; NotificationService bổ sung Dev ngoài exact audience, không thêm membership Dev.
President chỉ đọc/gửi thread khi có participant active; rule cấp default members không đổi. Không phát lại tin cũ.

Endpoint chat công khai `POST /api/v1/plm/sample-requests/{id}/messages` bật kiểm tra quyền gửi conversation hiện có
trước khi resolve/thêm participant; FE không thể tắt bằng JSON. Developer có quyền company-send; President và role
khác cần participant active. Publisher nghiệp vụ nội bộ vẫn theo gate nghiệp vụ riêng, không bị cờ endpoint này tác động.
GET messages dùng scope đọc tập trung, không ép membership lần hai đối với Developer.
