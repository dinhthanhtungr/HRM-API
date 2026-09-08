# Message Recipients

## SampleRequest Direct Patch

FE có thể preview người nhận trước khi gửi thông báo sau PATCH trực tiếp SampleRequest. Action này dùng cho các thay đổi `sample_request.*` và `product.*` đang đi theo luồng lưu thẳng + thông báo Lab, không tạo approval card:

```json
{
  "contextType": "SampleRequest",
  "actionType": "SampleRequestDirectPatch",
  "contextId": "00000000-0000-0000-0000-000000000000",
  "selectedRecipientEmployeeIds": []
}
```

Resolver hien tai xu ly theo `contextType = SampleRequest`; `actionType` duoc giu lai trong response de FE biet ngu canh. Response van gom `requiredRecipients`, `suggestedRecipients`, `selectedRecipients`, `canAddRecipients`, `canRemoveSuggestedRecipients`.

Feature này cung cấp contract dùng chung để FE xem trước danh sách người nhận trước khi gửi một message/nghiệp vụ có thông báo.

## Vì Sao Có POST Nhưng Nằm Trong Query

Endpoint preview dùng HTTP `POST` vì request cần body gồm context, action, id nghiệp vụ và danh sách người nhận đang chọn. Tuy nhiên handler chỉ đọc dữ liệu để trả preview, không tạo/sửa/xóa DB, nên implementation nằm trong:

```text
Queries/PreviewMessageRecipients
```

Không có `Commands` vì feature này chưa có hành động ghi dữ liệu.

## Tổ Chức Folder

```text
Features/MessageRecipients
+ Dtos
  + MessageRecipientPreviewDtos.cs
+ Queries
  + PreviewMessageRecipients
    + PreviewMessageRecipientsQuery.cs
    + PreviewMessageRecipientsQueryHandler.cs
+ Services
  + IMessageRecipientResolver.cs
+ README.md
```

`MessageRecipients` chỉ giữ phần dùng chung:

- DTO public trả cho FE.
- Query preview dùng chung.
- Interface `IMessageRecipientResolver` để các feature tự cài resolver theo nghiệp vụ.

Resolver cụ thể của từng feature không đặt ở đây. Ví dụ Sample Request đặt tại:

```text
Features/PLM/SampleRequests/Services/SampleRequestMessageRecipientResolver.cs
```

Rule người nhận bắt buộc của Sample Request đặt gần Sample Request:

```text
Features/PLM/SampleRequests/Rules/SampleRequestRecipientRules.cs
Features/PLM/SampleRequests/Services/SampleRequestRecipientResolver.cs
```

## Endpoint

```http
POST /api/v1/message-recipients/preview
```

Controller:

```text
HRM.Api/Controllers/MessageRecipientsController.cs
```

## Request

```json
{
  "contextType": "SampleRequest",
  "actionType": "Create",
  "contextId": null,
  "draftManagerBy": "00000000-0000-0000-0000-000000000000",
  "draftCategoryId": "00000000-0000-0000-0000-000000000000"
}
```

Ý nghĩa:

- `contextType`: loại nghiệp vụ. Hiện có `SampleRequest`.
- `actionType`: hành động FE đang chuẩn bị gửi, ví dụ `Create`, `GeneralMessage`, `DataChangeRequest`.
- `contextId`: id nghiệp vụ đã tồn tại, ví dụ `SampleRequestId`.
- `draftManagerBy`: người phụ trách tạm thời khi tạo mới và chưa có `contextId`.
- `draftCategoryId`: loại sản phẩm tạm thời khi tạo mới Sample Request, dùng để chọn leader Lab/R&D phù hợp trước khi record được lưu.
- `selectedRecipientEmployeeIds`: danh sách FE/user đang chọn, BE dùng để validate và merge với người nhận bắt buộc.

## Response

```json
{
  "success": true,
  "data": {
    "contextType": "SampleRequest",
    "actionType": "Create",
    "requiredRecipients": [],
    "suggestedRecipients": [],
    "selectedRecipients": [],
    "canAddRecipients": true,
    "canRemoveSuggestedRecipients": true
  }
}
```

Recipient DTO:

```json
{
  "employeeId": "00000000-0000-0000-0000-000000000000",
  "fullName": "Nguyen Van A",
  "externalId": "EMP0001",
  "source": "required",
  "reason": "Required sample request recipient",
  "locked": true
}
```

## Luồng Xử Lý

```text
FE mở form
-> POST /api/v1/message-recipients/preview
-> PreviewMessageRecipientsQueryHandler
-> chọn IMessageRecipientResolver theo contextType
-> resolver của feature trả required/suggested/selected recipients
```

Với Sample Request:

```text
SampleRequestMessageRecipientResolver
-> SampleRequestRecipientResolver
-> SampleRequestRecipientRules
```

Với form tạo mới Sample Request, FE nên gửi `draftCategoryId` đang chọn. Resolver sẽ đọc `Categories.ExternalId` của cùng company để quyết định leader mặc định:

- `CMP`, `PMA`, `PPG`, `AMB`, `VRG`, `ADD` -> leader `QAQC.RD`.
- `PHM`, `PBM`, `PDM`, `CMB`, `PIG` -> leader `QAQC.MAU`.
- `PKH`, `PTP`, `PMS`, `PPB`, hoặc loại không map được -> fallback leader `QAQC.RD` và `QAQC.MAU`.

`President`, `LabAdmin` và account `qaqcad01` nằm trong `requiredRecipients` với `locked = true`. Khi current sender có `SaleUser`, leader active của chính group active chứa sender (`source = sales_group_leader`) và SaleAdmin active có membership trong chính group đó (`source = sales_group_admin`) cũng nằm trong `requiredRecipients` với `locked = true`; sender bị loại trừ, không dùng role `Leader`/`SaleAdmin` toàn công ty và recipient trùng được gộp. Leader QAQC theo category là người nhận mặc định, xuất hiện trong `suggestedRecipients` và `selectedRecipients` ở lần preview đầu tiên nhưng `locked = false`, nên user có thể bỏ hoặc thay đổi.

`selectedRecipientEmployeeIds` có hai semantics: omit/null cho lần preview đầu tiên để BE tự chọn leader mặc định; gửi `[]` khi user đã bỏ toàn bộ người nhận tùy chọn; gửi mảng id khi user đang chọn các người nhận tùy chọn đó. Khi submit, FE gửi đúng danh sách tùy chọn đã chọn qua field recipient tương ứng; BE vẫn tự thêm `President`/`LabAdmin`.

Nếu `contextId` là Sample Request nội bộ (`RequestType` nội bộ) hoặc customer là `KH_VIETAUS`, resolver trả danh sách rỗng và `canAddRecipients = false` vì nghiệp vụ này không tạo InternalMail message/notification. Với form tạo mới chưa có `contextId`, FE tự ẩn bước chọn người nhận khi người dùng chọn request type nội bộ hoặc customer `KH_VIETAUS`.

Khi gửi thật, BE không tin hoàn toàn vào danh sách FE gửi. Handler nghiệp vụ vẫn tự resolve lại người nhận bắt buộc:

```text
SendSampleRequestMessageCommandHandler
-> SampleRequestRecipientResolver
```

Nhờ vậy danh sách FE preview và danh sách BE gửi thật không bị lệch.

Với Sample Request, preview cũng nhận `selectedSilentWatcherEmployeeIds` và trả `selectedSilentWatchers`. Khi FE
không gửi field này ở lần preview đầu tiên, các `LabUser`/`LabAdmin` active không xuất hiện trong
`requiredRecipients`, `suggestedRecipients` hoặc `selectedRecipients` được chọn sẵn làm Watcher im lặng; FE có thể
gửi `[]` để bỏ các lựa chọn mặc định đó.
Đây là người được thêm vào thread với role `Watcher`, mute mặc định. Họ có quyền đọc và vẫn thấy notification trong
Notification Hub ở trạng thái đã đọc, nhưng không nhận SignalR/Web Push hay làm tăng badge. Form tạo mới gửi các id
này qua `initialLabSilentWatcherEmployeeIds`; một employee không được vừa là silent watcher vừa là recipient thông thường.

## Cách Thêm Context Mới

Khi một feature khác cần preview recipients:

1. Tạo resolver trong folder feature đó.
2. Implement `IMessageRecipientResolver`.
3. `CanResolve(contextType)` trả true cho context mới.
4. Đăng ký DI:

```csharp
services.AddScoped<IMessageRecipientResolver, YourFeatureMessageRecipientResolver>();
```

Không đặt resolver nghiệp vụ cụ thể vào `Features/MessageRecipients`, để feature dùng chung không phụ thuộc ngược vào từng module.
