# Giá công thức

## Xuất Excel danh sách NVL

```http
GET /api/v1/plm/formulas/{formulaId}/materials/excel
```

Endpoint tải tệp `.xlsx` gồm các NVL active trong Formula, với các cột `STT`, `Mã NVL`, `Tên NVL`,
`STD`, `Giá gần nhất`. `STD` lấy từ `FormulaMaterial.Quantity`; mã/tên là dữ liệu snapshot của dòng Formula để phản ánh đúng dữ liệu trong công thức;
giá gần nhất dùng nguồn mua hàng/nhà cung cấp hiện hành. NVL chưa có giá hợp lệ để trống cột giá.

Endpoint yêu cầu đồng thời `PLM.FormulaMaterials.View` và `PLM.FormulaPrices.View`; backend cũng kiểm tra
nhóm `FormulaMaterialViewers` và `FormulaPriceViewers`, nên không đủ một trong hai quyền không thể tải file.

## API ghi công thức

Các API ghi công thức nằm dưới:

```http
POST   /api/v1/plm/formulas
POST   /api/v1/plm/formulas/{sourceFormulaId}/clone
POST   /api/v1/plm/formulas/{formulaId}/requote-requests
PUT    /api/v1/plm/formulas/{formulaId}
PATCH  /api/v1/plm/formulas/{formulaId}/status
DELETE /api/v1/plm/formulas/{formulaId}
```

`POST /api/v1/plm/formulas/{sourceFormulaId}/clone` tạo một Formula `Draft` mới trong cùng company. Backend tự sinh
`externalId` và tên Formula mới, đặt `isSelect = false`, copy note, giá header và toàn bộ active material từ Formula nguồn.
Line number của bản copy luôn được tạo lại `1..N`; response trả `formulaId` mới để FE điều hướng thẳng sang màn hình sửa.
FE không cần gọi `item-lookup` cho từng dòng khi copy; lookup chỉ dùng nếu người dùng thêm/chọn item mới sau đó.

`POST /api/v1/plm/formulas/{formulaId}/requote-requests` gửi yêu cầu báo giá lại vào đúng conversation của
Sample Request đã chọn. Request body gồm `sampleRequestId`, `message` bắt buộc (tối đa 2000 ký tự) và `isUrgent`.
Formula và Sample Request phải cùng Product/cùng company. Message nằm trong thread Sample Request với type
`PriceQuoteRequest`; notification topic hiện có là `SampleRequestPriceQuoteRequested`
(`plm.sample_request.price_quote.requested`) và chỉ gửi President active cùng company, không gửi người tạo.
Link notification mở Product Pricing Options theo mã màu khi có. Payload notification chỉ có metadata message/thread;
không chứa cost, margin hoặc giá. Sample Request private/KH_VIETAUS tuân theo rule chung và không phát message/notification.

## Danh sách công thức khi lên đơn hàng

```http
GET /api/v1/plm/formulas?productId={productId}&customerId={customerId}&orderType={orderType}&isMerchadiseOrder=true
```

Khi `isMerchadiseOrder=true`, backend chỉ trả `formulaDevs`; `formulaSelects` và `formulaStandard` trả danh sách
rỗng vì đây là Manufacturing Formula, không phải loại Formula được lưu vào dòng Merchandise Order. FE phải gửi
`customerId` của đơn để backend áp đúng rule, không tự suy nội bộ ở client.

- Khi `isMerchadiseOrder=true`, Formula phải active, thuộc Product/công ty hiện tại và có `Formula.Status` là `SampleSent`
  hoặc `Completed`. Rule này áp dụng giống nhau cho cả bốn `orderType`; API không kiểm tra `SampleRequest.FormulaId`, Trial
  hay `BatchNo` để quyết định Formula có được chọn hay không.

Không có `customerId` hoặc `orderType`, endpoint vẫn giữ điều kiện tương thích cũ: chỉ `KH_VIETAUS` nhận Formula `SampleSent`;
client tạo đơn mới phải luôn gửi cả hai field để có kết quả chính xác.

Khi cờ không gửi hoặc bằng `false`, API giữ nguyên hành vi cũ và trả cả ba nhóm Formula. Tên query hiện tại
giữ nguyên `isMerchadiseOrder` để tương thích client đang dùng.

Mỗi phần tử trong `formulaDevs`, `formulaSelects` và `formulaStandard` có thêm `createdByName`: họ tên nhân viên
tạo Formula tương ứng. Field này là `null` khi bản ghi cũ không còn liên kết được với nhân viên tạo, không phải lỗi
và FE có thể hiển thị dấu `—`.

## Gửi mẫu và tạo Trial

Message `SampleRequestSampleSent` kèm `sampleReceiptAction` chứa Trial id để Sale xác nhận đã nhận mẫu ngay trong Notification Hub. Action xác nhận thuộc API Sample Request/Trial, chỉ cập nhật dữ liệu nhận mẫu và audit trên Trial; không thay đổi trạng thái Formula và không dùng ngày phản hồi khách hàng.

Lab gửi mẫu bằng endpoint trạng thái của Formula, không tạo `SampleRequestSampleTrial` độc lập từ FE:

```http
PATCH /api/v1/plm/formulas/{formulaId}/status
```

Khi `status = SampleSent`, FE gửi `sampleRequestId` để xác định đúng hồ sơ đang giao mẫu và `deliveredSampleQuantityKg` là khối lượng thực gửi, bắt buộc lớn hơn hoặc bằng 0.
`sampleRequestId` không thể tự suy ra từ Formula vì một Formula có thể xuất hiện trong nhiều ngữ cảnh Sample Request.

```json
{
  "status": "SampleSent",
  "sampleRequestId": "00000000-0000-0000-0000-000000000000",
  "deliveredSampleQuantityKg": 2.5,
  "expectedUpdatedDate": "2026-08-12T10:30:00"
}
```

Backend cho phép gửi Formula đang `Approved` hoặc gửi lại Formula đang `SampleSent`. Nếu Sample Request có Trial `Draft` active mới nhất chưa gắn Formula hoặc đang gắn đúng Formula được gửi, backend chuyển chính Draft đó thành lần gửi; nếu không có Draft phù hợp mới tạo Trial với `TrialNo = max + 1`. Backend snapshot `Formula.ExternalId` vào `BatchNo`, lưu `DeliveredSampleQuantityKg`, đặt `CustomerReplyStatus = WAITING`, tự gán `SentBy` là employee hiện tại, `SentDate` và `UpdatedDate` là thời điểm xử lý. Notification cho các participant liên quan có kèm khối lượng mẫu.
Endpoint này chỉ nhận trạng thái `Approved` hoặc `SampleSent`; không nhận `Completed`.
Formula chỉ được hoàn thành khi Sale ghi nhận Trial `Approved` qua action phản hồi khách.
Trong một lần lưu, backend đổi `Formula.Status = SampleSent`, đổi `SampleRequest.Status = SampleSent`, hoàn tất Draft phù hợp hoặc tạo Trial có
`TrialNo = max + 1`, đồng thời snapshot Formula/khách hàng/sản phẩm/mã màu. `SampleRequest.FormulaId` chưa được gán ở bước này;
nó chỉ được gán khi Sale ghi nhận khách đã chấp nhận một Trial. Sau khi lưu thành công, backend gửi message/notification
trong conversation hiện có của Sample Request.

## File liên quan của NVL trong công thức

```http
GET /api/v1/plm/formulas/{formulaId}/related-attachments
```

Endpoint này dùng cho vùng file bên dưới danh sách NVL của công thức đang chọn. Backend chỉ trả metadata của attachment thuộc các dòng công thức có `itemType = Material`; dòng `Product`, `MaterialFailure` và `ProductFailure` không được dùng để lấy file NVL.

Quyền truy cập dùng policy `PLM.FormulaMaterials.View`. Query luôn check `Formula.CompanyId`, `Formula.Product.CompanyId`, `Material.CompanyId`, `IsActive` của công thức/product/NVL/attachment. Nội dung file không trả trong response; FE dùng `contentUrl` hoặc `downloadUrl` do backend trả về để xem/tải khi người dùng bấm.

Response:

```json
{
  "formulaId": "00000000-0000-0000-0000-000000000000",
  "totalCount": 3,
  "groups": [
    {
      "sourceType": "Material",
      "sourceId": "00000000-0000-0000-0000-000000000000",
      "sourceExternalId": "NVL_NH_924",
      "sourceName": "Hạt nhựa HDPE 2400732",
      "attachments": [
        {
          "attachmentId": "00000000-0000-0000-0000-000000000000",
          "fileName": "TDS-HDPE.pdf",
          "sizeBytes": 125000,
          "contentType": "application/pdf",
          "isImage": false,
          "isPdf": true,
          "contentUrl": "/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content",
          "downloadUrl": "/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content?mode=download",
          "createdDate": "2026-08-07T10:30:00"
        }
      ]
    }
  ]
}
```

Nếu một NVL xuất hiện nhiều dòng trong cùng công thức, backend chỉ trả một group cho NVL đó. Nếu attachment bị lặp theo `attachmentId`, backend chỉ trả một lần. `totalCount` là số attachment unique trong toàn response. NVL không có attachment active sẽ không tạo group.

Tất cả endpoint ghi dùng policy `PLM.FormulaPricing.Update`, cùng nhóm quyền đang được phép chỉnh giá công thức.
Backend luôn khóa dữ liệu theo `CurrentUser.CompanyId`, yêu cầu current user có `EmployeeId`, và chỉ thao tác với
`Product`, `Formula`, `Material` đang active trong cùng công ty.

`POST` tạo công thức mới. Nếu FE không gửi `externalId`, backend sinh mã tháng theo prefix `VU`.
`PUT` là cập nhật full thông tin công thức và danh sách vật tư/sản phẩm trong công thức. Khi FE gửi `materials`,
backend soft-delete toàn bộ dòng material active cũ của công thức rồi tạo lại danh sách mới theo payload. Nếu FE không gửi
`materials`, backend giữ nguyên danh sách material hiện tại.

Payload tạo/cập nhật:

```json
{
  "externalId": null,
  "name": "Formula VU",
  "productId": "00000000-0000-0000-0000-000000000000",
  "note": "Ghi chú",
  "stepOfProduct": 7,
  "effectiveDate": "2026-07-28T00:00:00",
  "isSelect": false,
  "expectedUpdatedDate": "2026-07-28T10:30:00",
  "materials": [
    {
      "lineNo": 1,
      "itemId": "00000000-0000-0000-0000-000000000000",
      "itemType": "Material",
      "categoryId": null,
      "quantity": 1.25,
      "unitPrice": 50000,
      "unit": "kg"
    }
  ]
}
```

`stepOfProduct` là luồng công đoạn sản xuất của Formula, dùng enum số thống nhất với database và FE:
`0` Sang bao, `1` Đùn, `2` Trộn, `3` Trộn Recolor, `4` Nghiền, `5` Trộn → Đùn,
`6` Trộn → Đùn → Trộn Recolor, `7` Trộn → Nghiền → Đùn, `8` Trộn → Nghiền → Đùn → Trộn Recolor.
Giá trị `null` được chấp nhận khi Formula chưa xác định công đoạn. `POST` và `PUT` lưu trực tiếp
giá trị này; `GET /api/v1/plm/formulas/{formulaId}` cũng trả `stepOfProduct` dưới dạng số hoặc `null`.

`itemType` nhận các giá trị enum `Material`, `Product`, `MaterialFailure`, `ProductFailure`.
Với item material, `itemId` là `MaterialId`; với item product, `itemId` là `ProductId`.
Nếu không gửi `categoryId`, backend lấy category từ material/product được chọn. `unitPrice` không gửi thì mặc định `0`.
Khi có danh sách `materials`, `Formula.TotalPrice` được tính lại bằng tổng `quantity * unitPrice`.
`lineNo` trong response là thứ tự canonical do backend trả về. Khi ghi, FE chỉ gửi `materials[]`
theo thứ tự UI; backend không dùng `materials[].lineNo` từ request và đánh lại `1..N` theo vị trí mảng.

`PATCH /status` cho chuyển sang:

```text
Approved   -> Formula.Status = Approved, Formula.CheckBy/CheckDate = current employee/time.
SampleSent -> Formula.Status = SampleSent, Formula.SentBy/SentDate = current employee/time.
Completed  -> Formula.Status = Completed, Formula.IsSelect = true, SampleRequest.Status = Completed.
```

`PATCH /api/v1/plm/formulas/{formulaId}/status` cũng nhận `stepOfProduct` tùy chọn. Khi gửi
code hợp lệ, backend cập nhật công đoạn cùng lúc với status; khi không gửi field này, giá trị công đoạn
đang lưu không đổi. PATCH không hỗ trợ xóa công đoạn bằng `null`; dùng `PUT` với
`"stepOfProduct": null` nếu cần xóa.

Với `POST /status-transition`, FE có thể đặt `stepOfProduct` ở root hoặc trong `formulaUpdate`.
Nếu gửi ở cả hai vị trí, hai giá trị phải giống nhau.

Khi người dùng bấm đổi trạng thái trong lúc có thay đổi Formula chưa lưu, FE dùng:

```http
POST /api/v1/plm/formulas/{formulaId}/status-transition
```

Payload nhận toàn bộ field status như `PATCH /status`, thêm `formulaUpdate` tùy chọn có cùng contract
với `PUT /api/v1/plm/formulas/{formulaId}`. `expectedUpdatedDate` ở root là concurrency token canonical;
nếu `formulaUpdate.expectedUpdatedDate` được gửi thì phải giống root. Backend áp dụng `formulaUpdate`,
validate transition, tạo đúng một Formula Version và lưu Formula/material/status trong cùng một lần
`SaveChanges`. Nếu validation hoặc transition thất bại, không phần nào được lưu. Khi không có thay đổi
Formula, FE không gửi `formulaUpdate` và vẫn có thể dùng `PATCH /status` như cũ.

Khi `Approved` được gọi kèm `sampleRequestId` hợp lệ cùng Product, backend gửi message/notification trong
conversation của Sample Request với topic `SampleRequestFormulaApproved`
(`plm.sample_request.formula.approved`). Nội dung báo Formula đã được xác nhận và giá tham khảo có thể tra cứu.
Notification có `Link = /crm/quotations/product-pricing-options?keyword={ColourCode}` để người nhận bấm mở
màn hình tra cứu giá đã lọc theo mã màu. Payload không chứa material cost, giá sản xuất, giá bán hoặc margin;
quyền xem giá và quyền truy cập màn hình tra cứu vẫn được kiểm soát độc lập. Sample Request `private` hoặc
khách `KH_VIETAUS` vẫn không tạo message/notification theo rule chung.

`SampleSent` được chuyển khi công thức hiện đang ở trạng thái `Approved`, hoặc gửi lại khi công thức đã là `SampleSent`; backend từ chối chuyển thẳng từ `Draft`, `Cancelled` hoặc trạng thái khác sang `SampleSent`.
`Completed` chỉ được chuyển khi công thức hiện đang ở trạng thái `SampleSent`.

Thiết kế lifecycle mới của công thức dùng thêm ý nghĩa trạng thái:

```text
Draft / Approved              -> còn được chỉnh thông tin và material theo quyền.
SampleSent                    -> đã gửi mẫu, không sửa đè công thức này; muốn cải tiến thì clone/tạo công thức mới.
Completed                     -> Sale đã chọn/chốt công thức khách hàng đồng ý; chỉ trạng thái này mới được dùng để lên đơn hàng.
PendingSaleConfirmation       -> công thức cải tiến đang chờ Sale xác nhận đổi cho Sample Request đã Completed.
Cancelled hoặc Rejected       -> công thức/yêu cầu cập nhật bị hủy hoặc bị từ chối.
```

Khi Sale chốt công thức, backend phải cập nhật cả `SampleRequest.Status = Completed` và `Formula.Status = Completed`, đồng thời gửi message trong cùng Sample Request conversation để báo cho Lab. Khi Lab yêu cầu cập nhật công thức sau khi Sample Request đã Completed, Formula mới chuyển sang `PendingSaleConfirmation`, còn Sample Request chuyển sang `FormulaUpdateRequested` cho đến khi Sale chấp nhận hoặc từ chối.

Payload:

```json
{
  "status": "SampleSent",
  "sampleRequestId": "00000000-0000-0000-0000-000000000000",
  "deliveredSampleQuantityKg": 2.5,
  "expectedUpdatedDate": "2026-07-28T10:30:00"
}
```

Khi chuyển sang `SampleSent`, backend chỉ đánh dấu công thức đã gửi mẫu (`Formula.Status = SampleSent`,
`SentBy/SentDate`). `SampleRequest.FormulaId` không được hiểu là công thức đã gửi mẫu, vì field này chỉ dùng cho công thức
khách hàng đã chọn/chốt. Liên kết lần gửi mẫu nên được lưu bằng `SampleRequestSampleTrial` để không làm sai nghĩa công thức
được chọn.
Luồng `MarkSampleRequestsAsSampleSentAsync` chỉ cập nhật trạng thái gửi mẫu, `SendBy/SendDate` và `UpdatedBy/UpdatedDate`
của Sample Request target; không set `SampleRequest.FormulaId`.

Khi chuyển sang `Completed`, đây mới là lúc Sale/chủ thể nghiệp vụ chọn công thức cuối cùng cho Sample Request. Backend
validate cùng company/product, gắn công thức được chọn vào Sample Request, chuyển `SampleRequest.Status = Completed`, set
công thức đó là selected formula của product, và tạo message InternalMail với topic `SampleRequestFormulaCompleted`
(`plm.sample_request.formula.completed`) có nội dung `Công thức ... đã hoàn thành, sẵn sàng cho báo giá.`

`DELETE` là xóa mềm: set `Formula.IsActive = false`, `Formula.IsSelect = false`, và set các `FormulaMaterial.IsActive`
đang active của công thức đó thành `false`. Không xóa cứng dữ liệu.

Response của các API ghi:

```json
{
  "formulaId": "00000000-0000-0000-0000-000000000000",
  "externalId": "VU260700001",
  "status": "Draft",
  "updatedSampleRequestCount": 0,
  "updatedDate": "2026-07-28T10:30:00"
}
```

## Chi phí NVL snapshot và realtime

API `GET /api/v1/crm/quotations/product-pricing-options` trả hai giá ở mỗi Formula:

- `materialCost`: snapshot đang lưu tại `Formula.TotalPrice`, có thể được Ban giám đốc điều chỉnh.
- `realtimeMaterialCost`: tổng `FormulaMaterial.Quantity * latestUnitPrice` tại thời điểm GET.

`isRealtimeMaterialCostComplete` chỉ là `true` khi công thức có ít nhất một item và mọi item đều có giá mới nhất hợp lệ.
Nếu thiếu ít nhất một giá, `realtimeMaterialCost` trả `null` và `missingMaterialPriceCount` cho biết số item thiếu giá.
Formula không có item cũng trả `realtimeMaterialCost = null` và trạng thái không đầy đủ.
Chi phí realtime được làm tròn 6 chữ số thập phân và không được lưu vào Formula.
Mọi phép tính giá dùng `realtimeMaterialCost`; `materialCost` snapshot không còn
là đầu vào của calculator.

Nếu công thức rỗng hoặc có item thiếu giá, `pricing` trả `null`. Backend không
fallback về `Formula.TotalPrice`.

`GET /api/v1/plm/formulas/{formulaId}` trả `totalPrice` bằng tổng `quantity * latestUnitPrice` của các dòng material/product
đang active trong công thức. Field này dùng cùng nguồn latest price với `materials[].price` và `materials[].priceTotal`,
không lấy từ snapshot `Formula.TotalPrice`.

`GET /api/v1/plm/formulas` cũng áp dụng `ApplicationRoleSets.PLM.FormulaPriceViewers`: user không thuộc nhóm này sẽ nhận
`price = null` trong các nhóm formula select/development/standard.

## Giá bán tiêu chuẩn và dữ liệu Formula legacy

Giá NVL (`totalPrice`/`realtimeMaterialCost`) trên API Formula detail/preview luôn được tính realtime từ
đơn giá NVL mới nhất. Ba chỉ số hiển thị định giá hiện hành là `manufacturingCost`,
`standardSellingPrice` và `profitMarginRate` lấy duy nhất từ `ProductPricingVersion` active, `Approved`
mới nhất theo company/product/currency. Nếu chưa có bản Approved, cả ba field là `null`; không fallback
sang giá policy, Formula hoặc giá realtime.

`FormulaPricingEngine` vẫn dùng policy Published đúng company/profile/currency để tính preview/tier và
so sánh chi phí. Block `pricing` là preview độc lập, không nhận bản giá Approved, vì giá đã duyệt có thể
nằm ngoài giới hạn preview của policy. Vì vậy lỗi preview không được làm API Formula detail thất bại hoặc
thay đổi ba card giá đã duyệt ở cấp ngoài.

`Formula.TotalPrice`, `Formula.ProductionPrice`, `Formula.PresidentPrice` và
`Formula.ProfitMarginPrice` được giữ nguyên để đọc dữ liệu lịch sử. Chúng không còn là nguồn giá
chính, không tham gia fallback và không bị flow pricing mới ghi đè.

## PATCH giá snapshot (deprecated)

```http
PATCH /api/v1/plm/formulas/{formulaId}/pricing
```

Endpoint được giữ để client cũ nhận lỗi có kiểm soát, nhưng không còn ghi Formula:

```json
{
  "manufacturingCost": 12000,
  "standardSellingPrice": 125000,
  "expectedUpdatedDate": "2026-07-23T10:30:00"
}
```

Response là HTTP `410 Gone`, operation failure có mã
`PatchFormulaPricingDeprecated`. Client phải chuyển sang API ProductPricingVersion để create/update
Draft và approve. Endpoint cũ không gọi material price service, calculator hoặc save DB.

## Response giá chuẩn từ backend

GET Formula trả pricing canonical từ engine. Khi thiếu policy trả `pricing = null` cùng
`PricingPolicyMissing`. Khi thiếu giá NVL hoặc giá bằng 0, engine dùng 0 để tiếp tục tính và vẫn trả
`pricing`; `MaterialPriceMissing` cùng `missingMaterialPriceCount` chỉ là cảnh báo.
`GET /api/v1/crm/quotations/product-pricing-options` cũng trả cùng cấu trúc `pricing` trong từng Formula,
do đó màn hình tra cứu và màn hình chỉnh sửa dùng chung một kết quả tính.

`pricing` gồm:

- `profile`: `Powder` hoặc `Compound`.
- `materialCost`: trong `pricing` là chi phí NVL realtime dùng để tính.
- `manufacturingCost`: chi phí sản xuất hiệu lực sau khi áp dụng giá mặc định.
- `usedDefaultManufacturingCost`: cho biết backend có dùng giá mặc định hay không.
- `costBase`: `materialCost + manufacturingCost`.
- `standardSellingPrice`.
- `profitMarginRate`.
- `suggestedPriceTiers`: các bậc giá, biên số lượng, tỷ suất so với NVL và `costBase`.

## Quy tắc tính giá dùng chung

PLM và CRM dùng cùng `FormulaPricingEngine`. Profile không còn được suy từ mã sản phẩm hoặc `Additive`;
engine chỉ dùng `Product.FormulaPricingProfile` đã cấu hình tường minh. Policy được resolve theo
`companyId + profile + currency`, phải ở trạng thái `Published`, active và đã tới `EffectiveFrom`.

Default manufacturing cost, default profit margin, cách làm tròn và toàn bộ tier/price offset đều lấy từ
policy DB. `FormulaPriceCalculator` là hàm thuần chỉ nhận `FormulaPricingPolicyDefinition`; calculator không
chứa profile tiers, currency hoặc default price. Tier có `priceOffset = null` trả `unitPrice = null` và
`requiresManualPrice = true`.

Không có policy phù hợp thì `pricing = null`, `pricingStatus = PricingPolicyMissing`; không fallback sang
luật hard-code. Thiếu ít nhất một giá material hoặc có giá bằng 0 thì `pricingStatus = MaterialPriceMissing`
để cảnh báo, nhưng `pricing` vẫn được tính với các dòng đó bằng 0 và không chặn lưu/duyệt.
Giá được chọn để lập báo giá vẫn phải lưu snapshot vào dòng/bậc giá báo giá.

## Ghi chú riêng cho API chi tiết công thức

`GET /api/v1/plm/formulas/{formulaId}` không nhận query `currency`; backend luôn dùng `VND` để resolve pricing policy và không trả `materialCost` snapshot. API này trả `updatedDate` là mốc concurrency canonical: FE giữ nguyên giá trị từ GET và gửi lại dưới tên `expectedUpdatedDate` khi ghi. Với Formula cũ chưa có `UpdatedDate`, API dùng `CreatedDate` làm mốc fallback. API này cũng trả `realtimeMaterialCost`,
`isRealtimeMaterialCostComplete`, `missingMaterialPriceCount`, `manufacturingCost`, `standardSellingPrice`,
`profitMarginRate`, policy id/version, `suggestedPriceTiers`, `pricingStatus` và `pricing`.

Nếu một dòng NVL/sản phẩm không có latest price hoặc latest price là `null`, dòng đó trả
`hasLatestPrice = false`, `latestUnitPrice = 0`, `latestTotalPrice = 0` để FE cảnh báo. Engine đánh dấu
material cost không đầy đủ nhưng vẫn tạo block `pricing` với dòng thiếu giá được tính bằng 0.

Các dòng `materials[]` của API chi tiết công thức trả thêm `hasLatestPrice`, `latestUnitPrice`, `latestTotalPrice`,
`latestPriceDate`, `latestPriceSource` và `supplierPrices`. User không thuộc `ApplicationRoleSets.PLM.FormulaPriceViewers`
không nhận các giá nhạy cảm; các field tổng giá/pricing trả `null` và `supplierPrices` rỗng.

## Ghi chú riêng cho luồng lưu

### Snapshot nhận diện item

Khi PUT/create Formula, backend xác thực `itemId` theo `itemType` và `CompanyId`, rồi tự lấy
`CategoryId`, tên, mã và đơn vị từ bản ghi Material/Product. FE có thể vẫn gửi các field
`categoryId`, `materialNameSnapshot`, `materialExternalIdSnapshot`, `unit` để tương thích contract cũ,
nhưng backend không dùng các giá trị đó để ghi đè snapshot. Với Product, mã snapshot là
`ColourCode`, fallback `Code`. Các API GET Formula dùng `FormulaItemDisplayResolver` để tải theo batch
dữ liệu Material/Product hiện tại, không tạo N+1 query. Material hiển thị tên/mã hiện tại; Product hiển thị
tên `[ColourCode] Name` và mã là `ExternalId` của Sample Request active mới nhất thuộc Product. Snapshot
chỉ là fallback cho dữ liệu cũ không còn quan hệ nguồn. Rule này cũng áp dụng cho API tra cứu giá Formula
trong CRM. Không dùng resolver cho FormulaVersion, `FormulaMaterialSnapshots`, export/PDF và chứng từ lịch sử,
vì các API đó phải hiển thị đúng snapshot tại thời điểm nghiệp vụ.

Màn hình FE có thể chỉ có một nút `Lưu`, nhưng backend vẫn tách contract lưu thành hai API:

- `PUT /api/v1/plm/formulas/{formulaId}` chỉ lưu thông tin công thức và material. API này không nhận và không ghi đè `manufacturingCost`, `standardSellingPrice`, `profitMarginRate` hoặc `materialCost` ở cấp Formula. Nếu FE gửi `materials`, snapshot NVL `Formula.TotalPrice` được tính lại từ tổng `materials[].quantity * materials[].unitPrice`. Nếu FE gửi `materials: []`, snapshot NVL về `0`. Nếu FE không gửi `materials`, backend giữ nguyên material và snapshot NVL cũ.
- `PATCH /api/v1/plm/formulas/{formulaId}/pricing` chỉ lưu giá sản xuất và giá bán tiêu chuẩn. API này không nhận `materialCost`; snapshot NVL chỉ đổi khi lưu material của công thức.

Vì vậy FE phải tách payload submit theo dirty state dù UI chỉ hiển thị một nút lưu.

## Phiên bản công thức phát triển

Mỗi phiên bản lưu snapshot header Formula, giá và toàn bộ `FormulaMaterial.IsActive = true`. `VersionNo` tăng độc lập trong từng Formula; version hiện hành có `EffectiveTo = null`, còn version trước được đóng tại thời điểm tạo version mới.

Snapshot được tạo tự động khi tạo Formula, thay đổi thật sự qua PUT/PATCH pricing, hoặc thực hiện action trạng thái. PUT/PATCH không tạo version mới nếu nội dung snapshot không đổi. Action trạng thái, lưu phiên bản thủ công và khôi phục luôn tạo version vì đây là các mốc nghiệp vụ. Formula, materials và snapshot dùng chung một lần `SaveChanges`, do đó commit hoặc rollback cùng nhau. Khóa mutation theo Formula cùng unique index `(FormulaId, VersionNo)` ngăn hai request đồng thời tạo dữ liệu trùng; conflict từ database được trả về để client reload.

```http
GET  /api/v1/plm/formulas/{formulaId}/versions
GET  /api/v1/plm/formulas/{formulaId}/versions/{versionNo}
POST /api/v1/plm/formulas/{formulaId}/versions
POST /api/v1/plm/formulas/{formulaId}/versions/{versionNo}/restore
```

Hai API GET yêu cầu `PLM.Formula.Detail.View` và luôn lọc `CurrentUser.CompanyId`. Header giá chỉ được trả cho `FormulaPriceViewers`; danh sách item chỉ được trả cho `FormulaMaterialViewers`, và giá từng item tiếp tục được ẩn nếu user không có quyền xem giá.

## Yêu cầu báo giá lại Formula

Endpoint tương thích hiện hữu:

```http
POST /api/v1/plm/formulas/{formulaId}/requote-requests
```

không còn tự triển khai recipient/message riêng. Handler chuyển tiếp sang
`RequestSampleRequestPriceQuoteCommand` với `formulaId` tường minh, vì vậy cùng dùng validation company/product,
conversation Sample Request, recipient President và topic `plm.sample_request.price_quote.requested` như endpoint
`POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests`.

## Yêu cầu báo giá lại Formula

Endpoint tương thích hiện hữu:

```http
POST /api/v1/plm/formulas/{formulaId}/requote-requests
```

không còn tự triển khai recipient/message riêng. Handler chuyển tiếp sang
`RequestSampleRequestPriceQuoteCommand` với `formulaId` tường minh, vì vậy cùng dùng validation company/product,
conversation Sample Request, recipient President và topic `plm.sample_request.price_quote.requested` như endpoint
`POST /api/v1/plm/sample-requests/{sampleRequestId}/price-quote-requests`.

POST lưu yêu cầu `PLM.Formula.Manage`; POST khôi phục yêu cầu đồng thời `PLM.Formula.Manage` và `PLM.FormulaPricing.Update` vì action này ghi lại cả giá snapshot. Current user phải có `EmployeeId`. Khôi phục không sửa version cũ: backend thay header/material active của Formula bằng snapshot đã chọn rồi tạo một version mới với `ChangeReason = Restored from version ...`. Do contract FormulaVersion không snapshot `ExternalId`, `ProductId`, `EffectiveDate`, `IsSelect` và các field audit trạng thái, thao tác restore giữ nguyên các field đó trên Formula hiện tại.
