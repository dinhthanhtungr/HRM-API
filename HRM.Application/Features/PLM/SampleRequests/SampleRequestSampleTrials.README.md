# Sample Request Sample Trials

## Luồng chính

Trial là bản ghi lịch sử của một lần giao mẫu thực tế, không phải bản ghi nháp Formula.

1. Lab làm Formula `Draft`/`Approved`, Sample Request ở `New` hoặc `InProgress`: chưa có Trial.
2. Lab chuyển Formula `Approved -> SampleSent`, hoặc gửi lại Formula đang `SampleSent`, qua endpoint Formula status. Request bắt buộc có `sampleRequestId` và `deliveredSampleQuantityKg >= 0`. Backend ưu tiên chuyển Trial `Draft` active mới nhất chưa gắn Formula hoặc đang gắn đúng Formula thành `SampleSent`; chỉ khi không có Draft phù hợp mới tạo Trial với `TrialNo = max + 1`. Trial snapshot `Formula.ExternalId` vào `BatchNo`, lưu khối lượng và đặt `CustomerReplyStatus = WAITING`. Cùng transaction này, Sample Request chuyển sang `SampleSent`, rồi mới gửi message có kèm khối lượng cho Sale trong cùng conversation.
3. Sale xác nhận đã nhận mẫu ngay trên message Lab gửi; backend cập nhật `RequestReceivedDate`, chuyển Trial sang `WaitingCustomerFeedback` và ghi audit `UpdatedBy/UpdatedDate`.
4. Sale ghi nhận phản hồi khách qua action `customer-feedback`:
   - `Approved`: Trial `Approved`, Formula của Trial `Completed`, Formula đó được chọn, Sample Request `Completed`.
   - `Failed`: Trial `Failed`, Sample Request trở về `InProgress`; Lab tạo/clone Formula mới và gửi mẫu lại để tạo Trial kế tiếp.
   - `Cancelled`: Trial `Cancelled`, Sample Request `Cancelled`.
   - Shortcut trên màn Sample Request: khi hồ sơ đang `SampleSent`, role thuộc `FormulaSelectors` có thể PATCH chọn
     `formulaId` của đúng Trial pending mới nhất. Backend hiểu thao tác đã xác nhận là khách chấp nhận mẫu, tự đặt
     `CustomerReplyStatus = APPROVED`, duyệt Trial và hoàn thành Formula/Sample Request trong cùng transaction.
     Nếu Formula không khớp Trial pending mới nhất, ngoài customer scope hoặc user không có quyền thì toàn bộ PATCH bị từ chối.
     Với Sample Request legacy không có bất kỳ Trial active nào, backend tạo Trial kế tiếp gắn Formula đã chọn và duyệt
     ngay trong transaction đó để lưu lại dấu vết đã gửi–đã nhận–khách duyệt. `SentDate` kế thừa từ `SampleRequest.SendDate`,
     fallback `RealDeliveryDate`, rồi mới dùng thời điểm chốt; `RequestReceivedDate`/`FinishedDate` là thời điểm Sale chốt,
     `CustomerReplyStatus = APPROVED` và ghi chú phản hồi nêu rõ khách đã xác nhận hoàn thành mẫu. Không suy diễn số kg giao
     thực tế hoặc người Lab gửi mẫu khi dữ liệu nguồn không có. Không tự tạo fallback nếu đang có Trial active khác trạng thái.
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

SaleUser, SaleAdmin, Developer, President và trưởng nhóm Sale (`MemberInGroups.IsAdmin`) có thể xác nhận. Trưởng nhóm chỉ được phép với Sample Request nằm trong team/customer visibility của chính họ. Backend kiểm tra company, customer visibility, Trial thuộc đúng Sample Request và `messageId` đúng message có action của Trial để tránh IDOR. Lần gọi lại trả kết quả đã xác nhận và không ghi nhận lần thứ hai.

Ngày Sale chọn được lưu trực tiếp vào `Trial.RequestReceivedDate`, là field ngày nhận mẫu đã có sẵn. Backend đồng thời chuyển `Trial.Status` sang `WaitingCustomerFeedback`; `UpdatedBy/UpdatedDate` ghi nhận người và thời điểm thực hiện action.

Sau khi lưu, backend cập nhật `sampleReceiptAction.status = Confirmed` trong payload message để FE khóa nút và hiển thị ngày/người xác nhận. API report Trial tiếp tục trả `requestReceivedDate` theo contract hiện có; không bổ sung cột database mới.

## Ghi nhận phản hồi khách

### Composer Trial + CRM interaction dành cho Sale

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials/{trialId}/customer-feedback
```

Endpoint này phục vụ dialog **Phản hồi khách hàng/Ghi chú phản hồi**. FE chỉ gửi dữ liệu người dùng nhập; backend tự resolve customer từ Trial và không nhận `customerId`, `sampleRequestId` hoặc `trialId` trong body.

```json
{
  "idempotencyKey": "00000000-0000-0000-0000-000000000001",
  "interactionType": "Call",
  "interactionAt": "2026-08-13T09:30:00",
  "content": "Khách đã nhận mẫu và đang kiểm tra.",
  "customerReplyStatus": "RECEIVED",
  "customerReplyNote": "Hẹn phản hồi sau ba ngày.",
  "outcome": "Đã nhận mẫu",
  "nextAction": "Gọi lại sau ba ngày",
  "nextFollowUpDate": "2026-08-16T09:30:00",
  "expectedTrialUpdatedDate": "2026-08-13T09:00:00"
}
```

`idempotencyKey` là UUID bắt buộc và phải được FE giữ nguyên khi retry cùng một lần lưu. Backend tạo interaction ID ổn định theo company/key; retry trả lại interaction đã tạo, còn tái sử dụng key cho Trial khác bị từ chối.

Trong đúng một `SaveChangesAsync`/transaction, backend:

- cập nhật `CustomerReplyStatus`, `CustomerReplyDate`, `CustomerReplyByEmployeeId`, `CustomerReplyNote`, `OrderDate` và audit của Trial;
- nếu `customerReplyStatus = APPROVED`: Trial `Approved`, Formula `Completed`/được chọn và Sample Request `Completed`;
- nếu `customerReplyStatus = FAIL`: Trial `Failed`, Formula `Rejected` (nghĩa là mẫu không đạt) và Sample Request trở về `InProgress`;
- nếu `customerReplyStatus = CANCEL`: Trial, Formula và Sample Request chuyển `Cancelled`;
- nếu `customerReplyStatus = WAITING` hoặc `BÁO GIÁ`: Trial lần lượt chuyển `WaitingCustomerFeedback` hoặc `PriceQuote`, không đổi Sample Request/Formula. Cả hai vẫn là trạng thái đang mở, nên Sale có thể ghi tiếp kết quả cuối `APPROVED`/`FAIL`/`CANCEL`;
- tạo `CustomerInteraction` với `InteractionType` do Sale chọn;
- tạo reference primary có `ReferenceType = SampleTrial`, `ReferenceId = trialId`;
- cập nhật `Customer.LastContactDate/CurrentSaleId`;
- tạo follow-up `WorkTask` nếu có `nextFollowUpDate`.

Sau khi transaction lưu thành công, backend tạo một message tiếng Việt trong **chính conversation của Sample Request** và publish topic `SampleRequestCustomerFeedbackRecorded` (`plm.sample_request.customer_feedback.recorded`). Nội dung có mã yêu cầu, lần thử, công thức, trạng thái phản hồi và ghi chú. Người nhận dùng resolver Sample Request hiện có: các role bắt buộc (President/LabAdmin), manager, participant đang có và người gửi. Leader Lab theo nhóm sản phẩm chỉ được thêm khi đã là participant hoặc được chọn ở luồng tạo message; không tự động được thêm bởi notification này. VU `private` hoặc khách `KH_VIETAUS` vẫn no-op theo `SampleRequestMessageRules`, nên không tạo conversation/notification. Retry idempotency trả interaction đã có trước khi vào bước gửi, vì vậy không tạo message trùng.

Chỉ role thuộc `ApplicationRoleSets.Modules.Sales` được gọi route PLM này. Backend tiếp tục kiểm tra company, customer visibility, contact/employee scope và Trial thuộc đúng Sample Request/customer. `customerReplyStatus` và `content` bắt buộc; `expectedTrialUpdatedDate` là concurrency token tùy chọn.
Các DateTime FE gửi ở route này có thể là ISO UTC (`Z`); backend chuẩn hóa về timestamp không timezone trước khi lưu để tương thích schema PostgreSQL hiện tại.

Endpoint cũ `POST /api/v1/plm/sample-requests/{sampleRequestId}/customer-feedback` vẫn là action lifecycle (`Approved/Failed/Cancelled`) và không tạo CRM interaction; FE dialog tương tác mới phải gọi route có `trialId` ở trên.

### Phản hồi lifecycle cũ

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

Endpoint bắt đầu từ danh sách Sample Request mà current user được phép xem. Danh sách chính luôn trả đúng một dòng cho một Sample Request và chỉ gắn Trial active mới nhất theo `trialNo` lớn nhất:

- Sample Request chưa có Trial vẫn trả một dòng với `hasTrial = false`, `sampleRequestSampleTrialId`, `trialNo` và `status` bằng `null`.
- Sample Request có một hoặc nhiều Trial vẫn chỉ trả một dòng; dữ liệu Trial trên dòng là Trial mới nhất.
- `trialCount` là tổng Trial active của yêu cầu; `hasPreviousTrials = true` khi có nhiều hơn một Trial để FE hiển thị nút mở lịch sử.
- Khi truyền `status` hoặc `customerReplyStatus`, backend lọc trên Trial mới nhất; Sample Request chưa có Trial không thỏa các filter này.

Danh sách chính luôn sắp xếp `SampleRequest.CreatedDate` giảm dần, không đổi vị trí khi tạo Trial mới. Response trả rõ `sampleRequestCreatedDate` cho mốc này; `createdDate` cũ vẫn là ngày tạo Trial mới nhất (hoặc fallback Sample Request khi chưa có Trial). `sortBy` và `sortDirection` cũ được bỏ qua cho endpoint này. `fromDate`/`toDate` của danh sách chính lọc theo `SampleRequest.CreatedDate`.

FE tải Trial cũ khi người dùng expand bằng:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials
```

Route lịch sử trả các Trial active của đúng Sample Request, sắp `trialNo` giảm dần và dùng cùng DTO với danh sách chính.

Response trả thêm `sampleRequestStatus` và `requestedSampleQuantity` từ hồ sơ gốc để FE vẫn có dữ liệu hữu ích khi trial chưa được tạo.

Mỗi dòng còn trả `formulaId` và `formulaExternalId` để hiển thị ngay mã VU Formula mà không cần gọi
Formula detail/lookup cho từng dòng. Khi có Trial, `formulaExternalId` ưu tiên `Trial.Formula.ExternalId`,
fallback sang `Trial.BatchNo` snapshot cho dữ liệu lịch sử. Khi chưa có Trial, API fallback sang Formula hiện
đang gắn trên Sample Request. `null` nghĩa là dòng chưa có Formula liên quan; FE chỉ gọi Formula lookup khi
người dùng mở thao tác chọn/đổi Formula.

Response cũng trả quyền hành động:

- `canCreateTrial`: user hiện tại được phép tạo trial mới, kể cả khi Sample Request đã có trial trước đó.
- `canUpdateTrial`: user hiện tại được phép cập nhật trial của dòng hiện tại.
- `canUpdateCustomerFeedback`: user hiện tại được phép cập nhật riêng `customerReplyStatus` và
  `customerReplyNote`; cờ chỉ true khi dòng đã có Trial. Sale dùng cờ này thay vì `canUpdateTrial`.

Các query parameter:

```text
pageNumber, pageSize, keyword
sampleRequestId, customerId
fromDate, toDate
sampleRequestCreatedToDate (`yyyy-MM-dd`, inclusive)
includePreviousUnfinished (`true` mặc định)
reportType (`All`, `CompletedSamples`, `WaitingCustomerFeedback`)
status, customerReplyStatus
```

`All` và không truyền `reportType` trả toàn bộ Sample Request active mà current user được phép xem, không loại theo
trạng thái workflow. Mỗi Sample Request vẫn chỉ gắn Trial active mới nhất nếu có. Hai loại báo cáo chuyên biệt có điều kiện:

- `CompletedSamples`: Sample Request `Completed` và Trial mới nhất có trạng thái `Approved`; không phụ thuộc
  `FinishedDate`. Danh sách sắp theo `CustomerReplyDate` giảm dần, fallback `UpdatedDate` rồi `CreatedDate` cho dữ liệu cũ.
- `WaitingCustomerFeedback`: là hàng đợi cần Sale theo dõi trước khi có kết quả cuối. Danh sách gồm hai nhóm:
  1. Trial `SampleSent` chưa có `RequestReceivedDate` (**Chờ Sale nhận mẫu**), luôn xếp trên;
  2. Trial `WaitingCustomerFeedback` đã có `RequestReceivedDate` và `CustomerReplyStatus` đang rỗng hoặc `WAITING`
     (**Chờ phản hồi khách hàng**).
  Mỗi nhóm sắp giảm dần theo ngày gửi mẫu hoặc ngày Sale nhận mẫu tương ứng. FE phải dựa vào `status` để map đúng
  nhãn của từng dòng, không gộp cả hai thành một trạng thái workflow.

`fromDate` và `toDate` dùng mốc ngày theo loại báo cáo: `CompletedSamples` lọc `CustomerReplyDate`,
`WaitingCustomerFeedback` lọc `SentDate` với nhóm chờ Sale nhận mẫu và `RequestReceivedDate` với nhóm chờ phản hồi
khách hàng, còn `All`/không truyền `reportType` lọc
`SampleRequest.CreatedDate`. `toDate` bao gồm trọn ngày.

`sampleRequestCreatedToDate` luôn lọc độc lập theo `SampleRequest.CreatedDate`, bất kể loại báo cáo.
`sampleRequestCreatedToDate` có semantics "tạo đến hết ngày": ví dụ
`sampleRequestCreatedToDate=2026-07-31` dùng điều kiện `SampleRequest.CreatedDate < 2026-08-01 00:00:00`, nên các hồ sơ
tạo từ tháng trước nhưng hiện vẫn chờ phản hồi tiếp tục xuất hiện. Không truyền tham số thì không giới hạn ngày tạo.
Đây là trạng thái tồn đọng hiện tại theo ngày tạo hồ sơ, không phải snapshot trạng thái tại cuối ngày/tháng đã chọn.

`includePreviousUnfinished` mặc định là `true` để giữ tương thích. Khi có `sampleRequestCreatedToDate`:

- `true`: không có cận dưới ngày tạo, nên giữ các mẫu tồn từ tháng trước.
- `false`: backend thêm cận dưới là ngày đầu tháng của `sampleRequestCreatedToDate`, nên chỉ trả Sample Request tạo trong
  chính tháng được chọn. Ví dụ cutoff `2026-07-31` dùng khoảng `2026-07-01 <= CreatedDate < 2026-08-01`.

Nếu không truyền `sampleRequestCreatedToDate`, `includePreviousUnfinished` không áp dụng bộ lọc ngày vì không có tháng làm mốc.

Màn hình tồn đọng nên chủ động gửi `reportType=WaitingCustomerFeedback`. Nếu truyền đồng thời
`sampleRequestCreatedToDate` và `fromDate`/`toDate`, các điều kiện được kết hợp bằng `AND`.

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
  "requestDeliveryDate": "2026-06-13T00:00:00",
  "expectedDeliveryDate": "2026-06-14T00:00:00",
  "requestReceivedDate": "2026-06-15T00:00:00",
  "finishedDate": "2026-06-25T00:00:00",
  "sentDate": "2026-06-26T00:00:00",
  "deliveryMethod": "Gửi xe",
  "labNote": "Gửi mẫu để khách kiểm tra",
  "sentByEmployeeId": "00000000-0000-0000-0000-000000000000",
  "status": "SampleSent"
}
```

Backend tự tính `trialNo = max(trialNo) + 1`, tạo snapshot khách hàng/mã yêu cầu/sản phẩm/mã màu/loại sản phẩm và gán audit fields. Formula phải active, thuộc đúng product; người gửi phải active và cùng company. Nếu có `sentDate` nhưng không gửi `sentByEmployeeId`, backend dùng employee hiện tại. Khi gửi `requestDeliveryDate` hoặc `expectedDeliveryDate`, backend đồng thời cập nhật ngày tương ứng trên Sample Request cha trong cùng lần lưu; không gửi thì giữ nguyên dữ liệu Sample Request.

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

`requestDeliveryDate` và `expectedDeliveryDate` của payload Trial là dữ liệu của Sample Request cha, không phải snapshot trên Trial. Khi gửi value, backend cập nhật Sample Request trong cùng transaction; khi không gửi, giữ nguyên. PATCH có thể clear bằng chính field code `requestDeliveryDate` hoặc `expectedDeliveryDate` trong `clearFields`.

Technical editor giữ quyền PATCH các field hiện có. Sale thuộc `ApplicationRoleSets.Modules.Sales` chỉ được gửi
`customerReplyStatus`, `customerReplyNote`, hoặc clear đúng hai field này qua `clearFields`. Nếu payload Sale có bất kỳ
field nghiệp vụ nào khác, backend từ chối toàn bộ request bằng business error và không cập nhật một phần. Company/customer
visibility và `expectedUpdatedDate` vẫn được kiểm tra như cũ.

Ví dụ Sale cập nhật phản hồi:

```json
{
  "expectedUpdatedDate": "2026-06-26T09:00:00",
  "customerReplyStatus": "APPROVED",
  "customerReplyNote": "Khách đã duyệt mẫu."
}
```

Ví dụ Sale clear phản hồi:

```json
{
  "expectedUpdatedDate": "2026-06-26T09:00:00",
  "clearFields": ["customerReplyStatus", "customerReplyNote"]
}
```

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
    "requestDeliveryDate",
    "expectedDeliveryDate",
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
customerReplyStatus
customerReplyNote
```

Backend từ chối số lượng/tỷ lệ âm, ngày hoàn thành trước ngày nhận, ngày gửi trước ngày hoàn thành, string trắng và string vượt độ dài cấu hình.

## Giá bán tiêu chuẩn trên báo cáo gửi mẫu

Hai route danh sách và lịch sử nhận query `currency` tối đa 10 ký tự, mặc định `VND`:

```http
GET /api/v1/plm/sample-requests/sample-trials?pageNumber=1&pageSize=15&currency=VND
GET /api/v1/plm/sample-requests/{sampleRequestId}/sample-trials?currency=VND
```

Mỗi dòng trả thêm:

```json
{
  "approvedStandardSellingPrice": 134322,
  "standardSellingPriceApprovedAt": "2026-08-29T15:19:00",
  "systemCalculatedStandardSellingPrice": 145000
}
```

- `approvedStandardSellingPrice`: `StandardSellingPrice` của `ProductPricingVersion` active, `Approved`, đúng
  company/product/currency và có version lớn nhất.
- `standardSellingPriceApprovedAt`: `ApprovedAt` của chính version trên; không dùng `UpdatedDate` thay thế.
- `systemCalculatedStandardSellingPrice`: giá realtime từ `ProductPricingSourceQueryService` và pricing policy
  hiện hành. Backend ưu tiên Formula đang gắn với Trial/SampleRequest; nếu Formula đó không còn là nguồn hợp lệ thì
  dùng nguồn Product hợp lệ được resolver ưu tiên.

Ba field chỉ được map cho role có quyền mở Product Pricing Workbench (`SaleUser`, `President`, `Developer`). User
khác nhận `null`. API không trả thêm material cost, manufacturing cost, margin hoặc tier, và không dùng snapshot
Formula làm fallback cho giá realtime.

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
