# Sample Request Sample Trials

## Luồng chính

Trial là bản ghi lịch sử của một lần giao mẫu thực tế, không phải bản ghi nháp Formula.

1. Lab làm Formula `Draft`/`Approved`, Sample Request ở `New` hoặc `InProgress`: chưa có Trial.
2. Lab chuyển Formula `Approved -> SampleSent`, hoặc gửi lại Formula đang `SampleSent`, qua endpoint Formula status. Request bắt buộc có `sampleRequestId` và `deliveredSampleQuantityKg >= 0`. Mỗi lần gọi thành công, backend tạo một Trial `SampleSent` mới với `TrialNo = max + 1` và lưu khối lượng gửi. Cùng transaction này, Sample Request chuyển sang `SampleSent`, rồi mới gửi message có kèm khối lượng cho Sale trong cùng conversation.
3. Sale xác nhận đã nhận mẫu ngay trên message Lab gửi; backend cập nhật ngày/người xác nhận vào đúng Trial.
4. Sale ghi nhận phản hồi khách qua action `customer-feedback`:
   - `Approved`: Trial `Approved`, Formula của Trial `Completed`, Formula đó được chọn, Sample Request `Completed`.
   - `Failed`: Trial `Failed`, Sample Request trở về `InProgress`; Lab tạo/clone Formula mới và gửi mẫu lại để tạo Trial kế tiếp.
   - `Cancelled`: Trial `Cancelled`, Sample Request `Cancelled`.
5. Khi Sample Request đã `Completed`, Lab dùng luồng `formula-change-requests` hiện có để đề xuất Formula cải tiến; không gửi lại SampleSent cho hồ sơ đã hoàn tất.

Mọi chuyển trạng thái trên được lưu trước; message/notification chỉ được gửi sau khi lưu thành công. Message dùng lại conversation của Sample Request và vẫn no-op cho VU nội bộ/private theo `SampleRequestMessageRules`.

## Sale xác nhận đã nhận mẫu

Message được tạo khi Lab gửi mẫu có `sampleReceiptAction` trong payload, gồm `sampleRequestSampleTrialId`, `status` và dữ liệu xác nhận. FE hiển thị ô ngày/giờ mặc định là thời điểm hiện tại cùng nút **Đã nhận mẫu** khi `canConfirm = true`.

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/confirm-receipt
```

```json
{
  "messageId": "00000000-0000-0000-0000-000000000000",
  "sampleReceivedDate": null,
  "expectedUpdatedDate": "2026-08-13T12:16:00"
}
```

`sampleReceivedDate` là tùy chọn; không gửi hoặc gửi `null` thì backend dùng `IDateTimeProvider.Now`. Sale có thể chọn lại ngày/giờ trước khi bấm xác nhận. Ngày tương lai vượt quá sai số đồng hồ 5 phút bị từ chối. Action chỉ áp dụng cho Trial `SampleSent` hoặc `WaitingCustomerFeedback`.

Chỉ `ApplicationRoleSets.PLM.FormulaSelectors` (Sale/Leader và super user) được xác nhận. Backend kiểm tra company, customer visibility, Trial thuộc đúng Sample Request và `messageId` đúng message có action của Trial để tránh IDOR. Lần gọi lại trả kết quả đã xác nhận và không ghi nhận lần thứ hai.

Trial lưu ba field riêng, không tái sử dụng `RequestReceivedDate`:

```text
SampleReceivedDate
SampleReceivedByEmployeeId
SampleReceiptConfirmedAt
```

Sau khi lưu, backend cập nhật `sampleReceiptAction.status = Confirmed` trong payload message để FE khóa nút và hiển thị ngày/người xác nhận. API report Trial cũng trả ba field trên cùng `sampleReceivedByName`.

## Ghi nhận phản hồi khách

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/customer-feedback
```

FE không gửi `trialId`. Backend tự chọn Trial active mới nhất của Sample Request có trạng thái `SampleSent` hoặc `WaitingCustomerFeedback` theo `TrialNo` giảm dần. Khi Lab gửi lại mẫu, phản hồi tiếp theo tự động áp dụng cho lần gửi mới nhất.

Khi gọi `PATCH /api/v1/plm/formulas/{formulaId}/status` với `status = SampleSent`, response trả thêm `sampleRequestSampleTrialId` của Trial vừa được tạo. FE không dùng field này để hoàn tất thao tác gửi mẫu; field được trả để đồng bộ state/mở detail khi cần.

Chỉ `ApplicationRoleSets.PLM.FormulaSelectors` (Sale/Leader và super user) được ghi nhận phản hồi.
`status` chỉ nhận `WaitingCustomerFeedback`, `Approved`, `Failed`, hoặc `Cancelled`. Ba trạng thái cuối bắt buộc có `customerReplyStatus`.

```json
{
  "status": "Failed",
  "customerReplyStatus": "FAIL",
  "customerReplyDate": "2026-08-12T11:00:00",
  "customerReplyNote": "Khách cần điều chỉnh lại tông màu.",
  "expectedUpdatedDate": "2026-08-12T10:30:00"
}
```

`expectedUpdatedDate` là concurrency token của Trial. Nếu không gửi `customerReplyDate`, backend dùng thời điểm ghi nhận phản hồi.

## Formula snapshot trên Trial

Các API `POST` và `PATCH` Trial nhận thêm `formulaExternalId` cùng với `formulaId`:

```json
{
  "formulaId": "00000000-0000-0000-0000-000000000000",
  "formulaExternalId": "VU260800123"
}
```

`FormulaId` là quan hệ thật của Trial. `FormulaExternalId` FE gửi chỉ dùng để đối chiếu: backend lookup Formula active cùng
company/product, từ chối khi mã không khớp, rồi gán mã Formula thật vào `BatchNo` như snapshot hiển thị/báo cáo. Khi đã gửi
`formulaId`, FE không được gửi hoặc clear `batchNo` trong cùng request. Nếu Trial không có Formula thì `batchNo` vẫn là dữ liệu
legacy có thể nhập theo contract cũ.

`SampleRequestSampleTrial` là domain model cho từng lần Lab hoàn thành hoặc gửi mẫu của một `SampleRequest`.

Một yêu cầu phối mẫu có thể có nhiều trial. Nếu khách hàng từ chối hoặc fail mẫu đã gửi, Lab tạo formula/trial mới thay vì sửa đè trial cũ. Report Excel theo tháng lấy mỗi dòng từ `SampleRequestSampleTrial`, không lấy trực tiếp từ `SampleRequest`.

Các snapshot như tên khách hàng, mã yêu cầu, tên sản phẩm, mã màu và loại sản phẩm giữ lại dữ liệu tại thời điểm gửi mẫu để báo cáo lịch sử không đổi khi master data thay đổi sau này.

`DeliveredSampleQuantityKg` là khối lượng giao mẫu thực tế do user nhập tay. Không tự tính field này từ formula, VU hoặc số lượng mẫu yêu cầu trên `SampleRequest`.

## API báo cáo cho FE

```http
GET /api/v1/plm/sample-requests/sample-trials
```

Endpoint bắt đầu từ danh sách Sample Request mà current user được phép xem, sau đó `LEFT JOIN` các trial active:

- Sample Request chưa có trial vẫn trả một dòng với `hasTrial = false`, `sampleRequestSampleTrialId`, `trialNo` và `status` bằng `null`.
- Sample Request có một trial trả một dòng trial.
- Sample Request có nhiều trial trả mỗi trial thành một dòng riêng.
- Khi truyền `status` hoặc `customerReplyStatus`, các dòng chưa có trial không thỏa bộ lọc và sẽ không xuất hiện.

Response trả thêm `sampleRequestStatus` và `requestedSampleQuantity` từ hồ sơ gốc để FE vẫn có dữ liệu hữu ích khi trial chưa được tạo.

Response cũng trả quyền hành động:

- `canCreateTrial`: user hiện tại được phép tạo trial mới, kể cả khi Sample Request đã có trial trước đó.
- `canUpdateTrial`: user hiện tại được phép cập nhật trial của dòng hiện tại.

Các query parameter:

```text
pageNumber, pageSize, keyword
sampleRequestId, customerId
fromDate, toDate
status, customerReplyStatus
sortBy, sortDirection
```

`fromDate` và `toDate` lọc theo ngày báo cáo ưu tiên lần lượt `finishedDate`, `sentDate`, `requestReceivedDate`, rồi `createdDate`. `toDate` bao gồm trọn ngày được truyền vào.

Các `sortBy` được hỗ trợ:

```text
sampleRequestExternalId
customerName
trialNo
requestReceivedDate
finishedDate
sentDate
updatedDate
```

`turnaroundDays` do backend tính từ `requestReceivedDate` đến `finishedDate` và không trả số âm. Dữ liệu snapshot được ưu tiên để báo cáo lịch sử không thay đổi; nếu snapshot trống, API fallback sang dữ liệu Sample Request/Product/Customer hiện tại.

Khi đã có trial, `status` được serialize thành code chuỗi ổn định (`Draft`, `SampleSent`, `WaitingCustomerFeedback`, `Approved`, `Failed`, `Cancelled`, `PriceQuote`, `ReworkRequested`) để FE tự map label, màu và icon. Khi chưa có trial, `status` là `null` và FE có thể dùng `sampleRequestStatus` để hiển thị trạng thái hồ sơ.

Endpoint có `[Authorize]`, khóa dữ liệu theo company và phạm vi khách hàng bằng `ICustomerVisibilityService.ApplySampleRequestVisibility`. Báo cáo luôn loại khách nội bộ `KH_VIETAUS` thông qua `PLMCustomerRules.InternalCustomerExternalId`, kể cả khi current user có thể xem Sample Request nội bộ ở màn hình nghiệp vụ khác. `additiveRate` và `labNote` chỉ được trả cho role thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors`; user khác nhận `null`.

## Tạo trial

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials
```

Ví dụ request:

```json
{
  "formulaId": "00000000-0000-0000-0000-000000000000",
  "batchNo": "VU260400324",
  "deliveredSampleQuantityKg": 1.1,
  "additiveRate": 0.02,
  "requestReceivedDate": "2026-06-15T00:00:00",
  "finishedDate": "2026-06-25T00:00:00",
  "sentDate": "2026-06-26T00:00:00",
  "deliveryMethod": "Gửi xe",
  "labNote": "Gửi mẫu để khách kiểm tra",
  "sentByEmployeeId": "00000000-0000-0000-0000-000000000000",
  "status": "SampleSent"
}
```

Backend tự tính `trialNo = max(trialNo) + 1`, tạo snapshot khách hàng/mã yêu cầu/sản phẩm/mã màu/loại sản phẩm và gán audit fields. Formula phải active, thuộc đúng product; người gửi phải active và cùng company. Nếu có `sentDate` nhưng không gửi `sentByEmployeeId`, backend dùng employee hiện tại.

Chỉ user thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors` được tạo trial. API vẫn kiểm tra company và customer visibility ở backend.

## Cập nhật trial

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}
```

PATCH sử dụng semantics:

- Field không gửi: giữ nguyên.
- Field gửi value: cập nhật value.
- Field nằm trong `clearFields`: chuyển thành `null`.
- Không được vừa gửi value vừa đưa cùng field vào `clearFields`.
- `status` không nullable và không nằm trong `clearFields`.
- `expectedUpdatedDate` là concurrency token tùy chọn; nếu record đã đổi, backend yêu cầu FE reload.

Ví dụ cập nhật value:

```json
{
  "expectedUpdatedDate": "2026-06-26T09:00:00",
  "finishedDate": "2026-06-25T00:00:00",
  "status": "SampleSent"
}
```

Ví dụ chuyển nhiều field về `null`:

```json
{
  "expectedUpdatedDate": "2026-06-26T09:00:00",
  "clearFields": [
    "formulaId",
    "batchNo",
    "deliveredSampleQuantityKg",
    "additiveRate",
    "requestReceivedDate",
    "finishedDate",
    "sentDate",
    "deliveryMethod",
    "labNote",
    "sentByEmployeeId"
  ]
}
```

Whitelist `clearFields` chính xác:

```text
formulaId
batchNo
deliveredSampleQuantityKg
additiveRate
requestReceivedDate
finishedDate
sentDate
deliveryMethod
labNote
sentByEmployeeId
```

Backend từ chối số lượng/tỷ lệ âm, ngày hoàn thành trước ngày nhận, ngày gửi trước ngày hoàn thành, string trắng và string vượt độ dài cấu hình.

## Ghi chú về API tạo Trial trực tiếp

`POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials` được giữ lại cho dữ liệu lịch sử/administration.
FE không dùng API này cho thao tác “Gửi mẫu”; luồng chính phải gọi `PATCH /api/v1/plm/formulas/{formulaId}/status`
với `status = SampleSent`, `sampleRequestId` và `deliveredSampleQuantityKg >= 0` để Formula, Sample Request và Trial được lưu cùng một transaction.

## Database deployment

Repo hiện không duy trì EF Core migrations history. Script PostgreSQL tạo bảng và index theo đúng EF configuration nằm tại:

```text
HRM.Infrastructure/DatabaseContext/Migrations/20260805_CreateSampleRequestSampleTrials.sql
```

Script chỉ tạo schema/table/index khi chưa tồn tại và không tự import dữ liệu Excel lịch sử.
