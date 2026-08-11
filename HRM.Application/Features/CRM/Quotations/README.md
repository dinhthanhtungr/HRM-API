# Báo giá CRM

## 1. Module này dùng để làm gì?

Module Quotation quản lý một báo giá từ lúc sale tạo bản nháp đến khi ghi nhận đã gửi cho khách hàng.

Module hiện hỗ trợ:

- Tra cứu sản phẩm đã phát triển xong, công thức, nguyên vật liệu và giá tham khảo.
- Tạo báo giá nháp.
- Sửa thông tin chung của báo giá.
- Thay toàn bộ danh sách sản phẩm.
- Lưu một giá cố định hoặc nhiều bậc giá theo khối lượng.
- Cập nhật lại giá sau khi sale đã xin giá bên ngoài.
- Tính subtotal, chiết khấu, VAT và tổng thanh toán.
- Ghi nhận báo giá đã gửi và lưu lịch sử trạng thái.
- Xem danh sách và chi tiết báo giá.

Module hiện **không** tự gửi email, không duyệt báo giá và không theo dõi khách hàng chấp nhận/từ chối.

## 2. Controller và Application khác nhau thế nào?

Controller:

```text
HRM.Api/Controllers/CRM/CustomerCare/QuotationsController.cs
```

chỉ làm bốn việc:

1. Khai báo route và yêu cầu đăng nhập.
2. Nhận body, route parameter hoặc query string.
3. Tạo Command/Query và gửi qua MediatR.
4. Chuyển kết quả thành HTTP response.

Validation, phân quyền dữ liệu, truy vấn database, tính tiền và thay đổi trạng thái nằm trong:

```text
HRM.Application/Features/CRM/Quotations
```

## 3. Quy trình sử dụng

```text
GET product-pricing-options
        |
        | Tra cứu sản phẩm, công thức và giá tham khảo
        v
POST quotations
        |
        | Tạo báo giá Draft
        v
PATCH quotation / PUT lines
        |
        | Sửa header hoặc thay danh sách sản phẩm
        v
POST refresh-prices
        |
        | Ghi giá mới sau khi xin giá bên ngoài
        v
POST mark-sent
        |
        | Ghi nhận đã gửi khách hàng
        v
Quotation chuyển sang Sent
```

Chỉ báo giá `Draft` được sửa header, thay dòng hoặc cập nhật giá.

## 4. Danh sách API

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/v1/crm/quotations` | Tạo báo giá nháp |
| `PATCH` | `/api/v1/crm/quotations/{quotationId}` | Sửa thông tin chung |
| `PUT` | `/api/v1/crm/quotations/{quotationId}/lines` | Thay toàn bộ dòng sản phẩm |
| `POST` | `/api/v1/crm/quotations/{quotationId}/refresh-prices` | Ghi giá mới cho các dòng |
| `POST` | `/api/v1/crm/quotations/{quotationId}/request` | Gửi yêu cầu báo giá nội bộ |
| `POST` | `/api/v1/crm/quotations/{quotationId}/mark-sent` | Ghi nhận đã gửi khách hàng |
| `GET` | `/api/v1/crm/quotations` | Lấy danh sách báo giá |
| `GET` | `/api/v1/crm/quotations/product-pricing-options` | Tra cứu sản phẩm/công thức/giá |
| `GET` | `/api/v1/crm/quotations/products/{productId}/pricing` | Tự chọn một công thức và tính giá cho sản phẩm |
| `GET` | `/api/v1/crm/quotations/{quotationId}/pricing-comparison` | So sánh snapshot với giá hiện tại của toàn bộ lines |
| `GET` | `/api/v1/crm/quotations/{quotationId}` | Xem chi tiết báo giá |
| `GET` | `/api/v1/crm/quotations/{quotationId}/pdf` | Xem hoặc tải PDF báo giá |

## 5. Chi tiết từng API

### 5.1. Tạo báo giá nháp

```http
POST /api/v1/crm/quotations
```

Gọi khi sale bấm tạo báo giá.

Request có thể chứa:

- Khách hàng và người liên hệ.
- Tiền tệ, tỷ giá.
- `taxPercent` áp dụng cho toàn báo giá.
- Ngày báo giá và hạn hiệu lực.
- Điều khoản thanh toán/giao hàng.
- Ghi chú.
- Danh sách `lines` ban đầu.

Backend thực hiện:

- Kiểm tra khách hàng thuộc phạm vi được xem.
- Tạo báo giá trạng thái `Draft`.
- Tự sinh mã prefix `BG` theo tháng nếu không có `externalId`.
- Snapshot dữ liệu dòng sản phẩm.
- Tính lại toàn bộ tổng tiền.
- Ghi audit người tạo và thời gian tạo.

Response thành công là `201 Created`, có `quotationId`, `externalId` và các totals vừa lưu.

### 5.2. Sửa thông tin chung

```http
PATCH /api/v1/crm/quotations/{quotationId}
```

Endpoint này chỉ sửa header:

- Customer/contact.
- Currency và exchange rate.
- VAT.
- Ngày báo giá, hạn hiệu lực.
- Điều khoản.
- Ghi chú.

Endpoint này không sửa:

- Dòng sản phẩm.
- Đơn giá hoặc price tiers.
- Status.
- Sale phụ trách.
- Company và audit fields.

Nếu VAT thay đổi, backend tính lại `taxAmount` và `totalAmount`. Response trả `QuotationTotalsDto`.

Concurrency contract cho các thao tác ghi trên báo giá:

- `GET /api/v1/crm/quotations/{quotationId}` trả `updatedDate`.
- FE giữ nguyên giá trị `updatedDate` đó và gửi lại trong request field `expectedUpdatedDate` nếu màn hình đang có token này.
- Backend so sánh `expectedUpdatedDate` với `Quotation.UpdatedDate` hiện tại trong DB sau khi đã check company/visibility.
- Nếu FE có gửi `expectedUpdatedDate` và giá trị khác nhau, backend trả `409 Conflict` với message yêu cầu reload trước khi lưu lại.
- Response mutate thành công trả lại `updatedDate` mới trong `QuotationTotalsDto` để FE có thể cập nhật token.

### 5.3. Thay toàn bộ dòng sản phẩm

```http
PUT /api/v1/crm/quotations/{quotationId}/lines
```

Request:

```json
{
  "expectedUpdatedDate": "2026-07-27T10:15:30.123",
  "lines": []
}
```

Đây là thao tác **replace toàn bộ**, không phải cập nhật từng dòng:

- Dòng cũ bị xóa khỏi aggregate.
- Backend tạo lại các dòng từ request.
- Các dòng mới có thể nhận `quotationLineId` mới.
- Backend snapshot lại mã, tên và đơn vị sản phẩm.
- Backend tính lại subtotal, discount, VAT và total.
- Các request ghi đồng thời trên cùng `quotationId` (`PATCH`, thay line, refresh giá,
  mark-sent) được xử lý tuần tự trong một instance API và khóa dòng Quotation bằng
  transaction PostgreSQL `FOR UPDATE`. Vì vậy nhiều API process cũng không cùng sửa
  aggregate tại một thời điểm.
- Riêng thao tác thay toàn bộ line sẽ tự xóa tracking state, tải lại aggregate mới nhất
  và thử lưu lại một lần nếu vẫn gặp `DbUpdateConcurrencyException`.

Mỗi dòng cần:

- `productId`.
- `quantity > 0`.
- `priceMode`.
- Giá cố định hoặc danh sách price tiers.

`sampleRequestId` là tùy chọn nhưng nếu có phải cùng company, customer và product.

### 5.4. Ghi giá mới sau khi xin giá

```http
POST /api/v1/crm/quotations/{quotationId}/refresh-prices
```

Tên endpoint dễ gây hiểu nhầm. Endpoint này **không tự đi lấy giá mới** từ Formula, NVL hoặc nhà cung cấp.

Nó chỉ:

1. Nhận giá mới do FE gửi.
2. Ghi giá vào đúng `QuotationLine`.
3. Thay price tiers nếu dòng dùng giá theo khối lượng.
4. Tính lại line total và tổng báo giá.

Ví dụ:

```json
{
  "lines": [
    {
      "quotationLineId": "00000000-0000-0000-0000-000000000000",
      "unitPrice": 0,
      "priceTiers": [
        {
          "quantityRangeLabel": "50-100 kg",
          "minQuantity": 50,
          "maxQuantity": 100,
          "minInclusive": true,
          "maxInclusive": true,
          "unitPrice": 101000,
          "sortOrder": 0
        }
      ]
    }
  ]
}
```

Backend kiểm tra toàn bộ request trước khi ghi để tránh cập nhật dở dang một phần.

### 5.5. Ghi nhận đã gửi khách hàng

```http
POST /api/v1/crm/quotations/{quotationId}/mark-sent
```

Gọi sau khi nhân viên đã thực sự gửi báo giá cho khách hàng bằng quy trình bên ngoài.

Backend:

- Chuyển `Draft` sang `Sent`.
- Ghi `SentDate`.
- Thêm `QuotationStatusHistory`.
- Có thể lưu note từ request.

Endpoint này **không gửi email và không tạo file báo giá**.

Gọi lại với báo giá đã `Sent` là idempotent: không tạo thêm history trùng.

### 5.6. Lấy danh sách báo giá

```http
GET /api/v1/crm/quotations
```

Phục vụ màn hình danh sách. Hỗ trợ:

- `pageNumber`, `pageSize`.
- `keyword`.
- `customerId`.
- `saleEmployeeId`.
- `status`.
- `from`, `to`.
- `sortBy`, `sortDirection`.

Response là dữ liệu tóm tắt; không chứa đầy đủ lines và histories.

### 5.7. Tra cứu dữ liệu định giá sản phẩm

```http
GET /api/v1/crm/quotations/product-pricing-options
```

Đây là endpoint tra cứu trước khi lập báo giá, **không phải danh sách báo giá**.

Mặc định endpoint lấy:

- Sample Request có `Status = Completed`.
- `RequestType = New`.
- Yêu cầu hoàn thành gần nhất của mỗi sản phẩm.
- Tất cả Formula active của sản phẩm.

Hỗ trợ:

- `requestType`.
- `status`: enum trạng thái Sample Request; nếu không truyền thì mặc định `Completed`.
- `quotationId`: lọc chính xác sản phẩm thuộc một báo giá active trong công ty hiện tại.
- `quotationStatus`: enum trạng thái báo giá; nếu truyền thì chỉ lấy sản phẩm đã nằm trong báo giá active có trạng thái đó.
- `keyword`: tìm theo mã/tên sản phẩm, mã Sample Request, `Formula.ExternalId`, tên Formula hoặc `Quotation.ExternalId`.
  Khi keyword bắt đầu bằng prefix báo giá từ `DocumentPrefix.BBG`, prefix Sample Request từ `DocumentPrefix.TP`
  hoặc prefix Formula từ `DocumentPrefix.VU`, backend chỉ tìm đúng nhóm mã đó bằng `StartsWith`.
- `pageNumber`, `pageSize`.
- `sortBy = externalId | colourCode | productName | updatedDate | createdDate`.
- `sortDirection = asc | desc`.

Nếu không truyền `sortBy`, backend mặc định sắp xếp theo ngày tạo mới nhất của
Sample Request hợp lệ thuộc sản phẩm, theo thứ tự giảm dần. `updatedDate` và
`createdDate` cũng là ngày của Sample Request, không phải ngày của Product.
`externalId` là mã của Sample Request hoàn tất gần nhất được chọn cho sản phẩm.

Mỗi sản phẩm trả:

- Thông tin Sample Request gần nhất.
- Mã và tên sản phẩm.
- Danh sách Formula.

Mỗi Formula trả:

- `isCustomerSelected`: `true` khi Formula có `IsSelect = true`, ngược lại là `false`.
- Chi phí NVL snapshot: `materialCost`.
- Chi phí NVL tính theo giá mới nhất: `realtimeMaterialCost`.
- Chi phí sản xuất đã lưu và hiệu lực.
- Giá bán tiêu chuẩn đã lưu và hiệu lực.
- Lợi nhuận cộng thêm.
- `pricing` do `FormulaPriceCalculator` tính.
- Danh sách nguyên vật liệu và giá gần nhất.

`materialCost` là snapshot đang lưu tại `Formula.TotalPrice`.

`realtimeMaterialCost` được xác định như sau:

- Là tổng `FormulaMaterial.Quantity * latestUnitPrice` của toàn bộ item trong công thức.
- Không tự ghi đè `Formula.TotalPrice`.
- Trả `null` nếu công thức rỗng hoặc thiếu ít nhất một giá.

`realtimeMaterialCost` là đầu vào duy nhất của `FormulaPriceCalculator`.
`materialCost` snapshot chỉ để hiển thị/đối chiếu và không tham gia `costBase`,
lợi nhuận, giá bán hiệu lực hoặc suggested price tiers.

Nếu công thức rỗng hoặc thiếu giá:

- `isRealtimeMaterialCostComplete = false`.
- `missingMaterialPriceCount` cho biết số item thiếu giá.
- `realtimeMaterialCost = null`.
- `pricing = null`; backend không fallback về `materialCost` snapshot.
- `manufacturingCost` vẫn luôn được trả cho role có quyền xem chi phí.
- `standardSellingPrice` vẫn trả `Formula.PresidentPrice` nếu DB đã có giá.
- Nếu DB chưa có giá bán tiêu chuẩn thì `standardSellingPrice` và
  `profitMarginRate` trả `null` do không đủ cost base realtime.

Contract giá của mỗi Formula:

- `manufacturingCost`: chi phí sản xuất hiệu lực. Dùng `Formula.ProductionPrice`
  nếu lớn hơn 0; nếu không dùng mặc định `10.000` cho Powder hoặc `20.000` cho Compound.
- `standardSellingPrice`: dùng `Formula.PresidentPrice` nếu DB có giá; nếu chưa có
  thì mặc định bằng `realtimeMaterialCost + manufacturingCost`.
- `profitMarginRate`:
  `(standardSellingPrice - costBase) / costBase * 100`.

`pricing` gồm:

- Profile `Powder` hoặc `Compound`.
- Chi phí sản xuất hiệu lực.
- `costBase`.
- `standardSellingPrice`.
- `profitMarginRate`.
- Các bậc giá gợi ý theo khối lượng.

Các bậc giá gợi ý lấy `standardSellingPrice` làm giá nền. Khi tạo báo giá,
backend vẫn lưu giá được chọn thành snapshot trên Quotation.

Endpoint yêu cầu user đã đăng nhập và tự giới hạn dữ liệu theo company hiện tại.

Quyền xem dữ liệu được áp dụng tại backend:

- `President` và `Developer` nhận đầy đủ chi phí, lợi nhuận, kết quả tính giá, nguyên vật liệu, giá gần nhất và nhà cung cấp.
- Các role còn lại chỉ nhận thông tin định danh sản phẩm, thông tin định danh công thức,
  và `standardSellingPrice`.
- Với các role còn lại, `materialCost`, `realtimeMaterialCost`,
  `isRealtimeMaterialCostComplete`, `missingMaterialPriceCount`,
  `manufacturingCost`, `profitMarginRate`,
  `pricingUpdatedDate`, `pricing` và `materials` đều trả `null`.

Backend phải tải giá mới nhất để tính giá mặc định và tỷ lệ lợi nhuận cho user
được phép xem giá bán, nhưng không trả danh sách NVL hoặc giá nhà cung cấp cho
user không có quyền xem đầy đủ.

### 5.7.1. Tự chọn công thức và tính giá theo sản phẩm

```http
GET /api/v1/crm/quotations/products/{productId}/pricing
```

FE chỉ truyền `productId`. Backend kiểm tra sản phẩm active và thuộc company hiện tại, sau đó
chọn đúng một công thức theo thứ tự:

1. Công thức active có `Formula.IsSelect = true`. Nếu dữ liệu có nhiều công thức cùng được
   đánh dấu, lấy công thức cập nhật gần nhất.
2. Nếu không có công thức được đánh dấu, lấy công thức của Sample Request active gần nhất có
   trạng thái `SampleSent` hoặc `Completed`. Ngày so sánh là
   `SendDate ?? UpdatedDate ?? CreatedDate`.

Response trả `formulaSelectionSource`:

- `CustomerSelected`: công thức lấy từ cờ `Formula.IsSelect`.
- `LatestSampleRequest`: công thức lấy từ Sample Request gửi gần nhất.

Response trả thông tin sản phẩm, công thức, giá bán tiêu chuẩn và kết quả
`FormulaPriceCalculator`. Chi phí NVL được tính lại từ giá nguồn mới nhất; không dùng
`Formula.TotalPrice` snapshot để tính giá preview. Sample Request chỉ được dùng nội bộ để chọn
Formula fallback; endpoint không trả `sampleRequestId` để FE gắn vào dòng báo giá vì route này
không có ngữ cảnh khách hàng.

Nếu công thức rỗng hoặc thiếu giá NVL thì `pricing = null`. Nếu không tìm thấy cả công thức được
đánh dấu lẫn công thức từ Sample Request đã gửi, endpoint trả thất bại thay vì trả giá `0`.

Quyền xem giá nhạy cảm giống endpoint `product-pricing-options`: `President` và `Developer`
được xem đầy đủ chi phí/kết quả tính giá; role khác chỉ nhận giá bán tiêu chuẩn và thông tin định
danh sản phẩm/công thức.

Đây là giá preview. Khi FE đưa giá vào báo giá, backend vẫn phải lưu `UnitPrice` thành snapshot
trên dòng báo giá. FE phải để `sampleRequestId = null` khi dùng endpoint này. Chỉ gửi
`sampleRequestId` khi người dùng chọn một Sample Request xác định và Sample Request đó thuộc đúng
customer, product và company của báo giá.

### 5.7.2. So sánh snapshot với giá hiện tại theo báo giá

```http
GET /api/v1/crm/quotations/{quotationId}/pricing-comparison
```

Endpoint dùng khi mở một báo giá đã có nhiều dòng. FE không gọi
`GET /products/{productId}/pricing` lặp lại cho từng line. Backend kiểm tra company và phạm vi customer,
sau đó tải công thức, nguyên vật liệu và giá nguồn theo batch để tính toàn bộ current price tiers trong một request.

Response:

```json
{
  "quotationId": "quotation-guid",
  "calculatedAt": "2026-07-27T10:00:00",
  "lines": [
    {
      "quotationLineId": "line-guid",
      "productId": "product-guid",
      "productExternalId": "LL5911",
      "productName": "Hạt màu xám",
      "savedPriceMode": "Tiered",
      "savedUnitPrice": 101000,
      "savedPriceTiers": [],
      "formulaId": "formula-guid",
      "formulaExternalId": "VU260700126",
      "formulaName": "F001",
      "formulaSelectionSource": "CustomerSelected",
      "currentPricingStatus": "Available",
      "isCurrentPricingComplete": true,
      "missingMaterialPriceCount": 0,
      "currentPriceTiers": [],
      "hasDifference": true
    }
  ]
}
```

Quy tắc:

- `savedPriceMode`, `savedUnitPrice`, `savedPriceTiers` là snapshot trong database và không bị tính lại.
- `currentPriceTiers` được tính từ giá NVL hiện tại bằng cùng `QuotationCurrentPricingResolver` với endpoint
  pricing một sản phẩm.
- API chỉ đọc, không tự ghi current tiers đè lên snapshot.
- `hasDifference` chỉ có giá trị khi current pricing đầy đủ; nếu không đủ dữ liệu thì trả `null`.
- Response không trả material cost, manufacturing cost hoặc margin, nên sale có thể xem current selling tiers
  mà không nhận dữ liệu chi phí nhạy cảm.
- Khi sale chủ động áp dụng current tiers, FE mới gửi snapshot mới qua API ghi giá/lines hiện có.

`currentPricingStatus`:

- `Available`: toàn bộ current tiers có giá.
- `ProductNotFound`: sản phẩm không còn active trong company.
- `FormulaNotFound`: không tìm thấy công thức ưu tiên hoặc fallback.
- `FormulaMaterialsMissing`: công thức không có dòng nguyên vật liệu active.
- `MaterialPriceMissing`: thiếu ít nhất một giá nguồn hiện tại.
- `ManualTierPriceRequired`: có tier theo rule yêu cầu Ban giám đốc nhập giá thủ công.

### 5.8. Xem chi tiết báo giá

```http
GET /api/v1/crm/quotations/{quotationId}
```

Response gồm:

- Header báo giá.
- Customer/contact/sale.
- Currency và totals.
- VAT.
- Lines.
- Giá cố định hoặc price tiers.
- Lịch sử trạng thái.

### 5.9. Xuất PDF báo giá

```http
GET /api/v1/crm/quotations/{quotationId}/pdf
GET /api/v1/crm/quotations/{quotationId}/pdf?download=true
```

- Mặc định trả `application/pdf` với `Content-Disposition: inline` để xem/in trên trình duyệt.
- `download=true` trả file tải xuống tên `Bao-gia-{ExternalId}.pdf`.
- Áp dụng cùng company/customer visibility với API xem chi tiết để chống IDOR.
- PDF dùng snapshot sản phẩm, giá cố định, price tiers và totals đang lưu trên Quotation;
  không tải lại Formula hoặc giá NVL realtime.
- Label cột price tiers lấy từ snapshot `Customer.QuotationLinePriceTiers.QuantityRangeLabel`;
  khi in PDF sẽ ẩn hậu tố nội bộ như `liên hệ BGĐ` hoặc `liên hệ Ban giám đốc` nếu label có kèm theo.
- Header của bảng giá dùng chữ đậm; các giá trị trong thân bảng dùng chữ thường để dễ đọc.
- Tên/địa chỉ khách hàng, người liên hệ, nhân viên phụ trách và thông tin công ty hiện
  được đọc từ dữ liệu liên kết tại thời điểm xuất PDF. Muốn chứng từ đã gửi bất biến
  hoàn toàn thì cần lưu thêm snapshot header hoặc lưu chính file PDF khi `mark-sent`.
- Header/footer ISO dùng cấu hình `Pdf:Quotation` và tự fallback sang text nếu thiếu ảnh.
- QuestPDF license lấy từ `Pdf:QuestPdfLicense`; chỉ cấu hình `Community` khi doanh nghiệp
  đáp ứng đúng điều kiện license.
- Endpoint tạo file on-demand và chưa lưu file PDF vào storage.
- Sau bảng giá, PDF hiển thị các ghi chú cố định về VAT, phí giao hàng, phụ thu hóa đơn và
  khối `Các điều khoản khác` theo mẫu báo giá hiện hành. `DeliveryTerms`, `PaymentTerms`
  và `ValidUntil` lấy từ báo giá nếu có; các dòng còn lại dùng mặc định trong renderer PDF.

Các asset mặc định:

```text
wwwroot/images/Logos/VietAusLogo.png
wwwroot/images/Iso/bureau-veritas.png
wwwroot/images/Iso/GRS.png
wwwroot/images/Iso/QR.png
```

`FormCode` và `EffectiveDate` phải được cấu hình đúng form ISO của báo giá,
không dùng lại mã form Delivery Order.

## 6. Quy tắc giá theo khối lượng của dòng sản phẩm

Contract ghi mới hiện chỉ chấp nhận `Tiered`. `Fixed` được giữ trong enum/database để đọc dữ liệu lịch sử và
chuyển đổi các draft cũ, nhưng create/replace/refresh line sẽ từ chối `Fixed`. Mọi line phải lưu toàn bộ tiers làm
snapshot, kể cả khi sale giữ nguyên giá gợi ý mà không chỉnh tay. Mỗi tier phải có `unitPrice > 0`.

`mark-sent` từ chối báo giá có line Fixed, thiếu tiers hoặc có tier giá không dương. FE phải chuyển draft cũ sang
Tiered trước khi xác nhận đã gửi khách hàng.

### Fixed

```text
priceMode = 0
```

- Dùng một `unitPrice`.
- `priceTiers` phải rỗng.

### Tiered

```text
priceMode = 10
```

- Có từ 1 đến 100 price tiers.
- Backend tìm đúng một tier khớp với `quantity`.
- Giá của tier khớp được snapshot vào `QuotationLine.UnitPrice` để tính tổng.

Quy tắc price tier:

- Label bắt buộc, tối đa 50 ký tự.
- Min/max không âm.
- `sortOrder` không âm và không trùng.
- Các khoảng không được chồng lấn.
- Phải có đúng một khoảng khớp quantity.
- Hai khoảng chung biên không được cùng bao gồm điểm biên đó.

## 7. Quy tắc tính tiền

### Dòng sản phẩm

```text
gross = quantity * unitPrice
lineDiscount = gross * discountPercent / 100
lineTotal = gross - lineDiscount
```

`lineTotal` chưa bao gồm VAT.

### Toàn báo giá

```text
subTotal = tổng gross của các dòng
discountAmount = tổng chiết khấu các dòng
taxableAmount = max(0, subTotal - discountAmount)
taxAmount = taxableAmount * taxPercent / 100
totalAmount = taxableAmount + taxAmount
```

VAT chỉ nằm ở header `Quotation.TaxPercent`, không nằm trên từng line.

Các totals được làm tròn 6 chữ số thập phân theo schema hiện tại.

## 8. Snapshot nghĩa là gì?

Báo giá phải giữ đúng dữ liệu tại thời điểm lập/gửi.

Vì vậy:

- Line lưu snapshot mã, tên, đơn vị và giá sản phẩm.
- Price tiers được lưu riêng trên Quotation.
- Giá NVL hoặc Formula thay đổi sau này không tự sửa báo giá cũ.
- Muốn đổi giá báo giá Draft phải gọi `refresh-prices` rõ ràng.
- Báo giá đã `Sent` không được tự thay giá.

Giới hạn hiện tại: `QuotationLine` chưa snapshot `FormulaId` và `FormulaExternalId`; endpoint pricing options chủ yếu cung cấp dữ liệu để FE tham khảo và chọn giá.

## 9. Bảo mật và multi-tenant

Toàn controller có `[Authorize]`.

Mọi handler user-facing phải:

- Lọc theo company hiện tại.
- Áp dụng `CustomerVisibilityService`.
- Chống truy cập báo giá của customer ngoài scope dù biết `quotationId`.
- Kiểm tra product active và cùng company.
- Kiểm tra contact thuộc đúng customer.
- Kiểm tra Sample Request cùng company/customer/product.

Backend tự lấy từ current user:

- `CompanyId`.
- `SaleEmployeeId`.
- `CreatedBy`, `UpdatedBy`.

FE không được gửi hoặc thay đổi các field này.

## 10. Kiểu response hiện tại

Các endpoint hiện chưa hoàn toàn đồng nhất:

- `POST /quotations`: `201 Created`, body là `OperationResult<QuotationCreateResultDto>`.
- `PATCH /quotations/{id}`: `200 OK`, body là `QuotationTotalsDto`.
- `PUT /lines`: `200 OK`, body là `OperationResult<QuotationTotalsDto>`.
- `POST /refresh-prices`: `200 OK`, body là `OperationResult<QuotationTotalsDto>`.
- `POST /mark-sent`: `204 No Content`.
- `GET /quotations/{id}/pdf`: raw `application/pdf`.
- Các GET thành công: body là `result.Data`.
- `QuotationTotalsDto` của mutation thành công có `updatedDate` mới để FE gửi lại ở `expectedUpdatedDate` cho lần lưu tiếp theo.

FE cần normalize đúng từng response; không giả định mọi endpoint đều có cùng wrapper.

## 11. Bảng dữ liệu chính

- `Customer.Quotations`.
- `Customer.QuotationLines`.
- `Customer.QuotationLinePriceTiers`.
- `Customer.QuotationStatusHistories`.

## 12. Xác nhận đã gửi báo giá

`POST /api/v1/crm/quotations/{quotationId}/mark-sent` nhận:

```json
{
  "interactionType": "Quotation",
  "note": "Đã gửi file PDF qua email khách hàng"
}
```

`interactionType` của endpoint này chỉ chấp nhận `Quotation` và mặc định là `Quotation`, vì vậy FE có thể bỏ field
này. Các giá trị `Email`, `Zalo` và những loại interaction khác vẫn được giữ cho các API CRM thông thường.

Khi báo giá Draft hợp lệ được xác nhận đã gửi, backend thực hiện đồng thời:

- Chuyển trạng thái sang `Sent`, lưu `SentDate` và status history.
- Khóa nghiệp vụ sửa header, line và refresh giá theo quy tắc trạng thái hiện có.
- Tạo `CustomerInteraction` theo `CustomerId` với `InteractionType = Quotation`; interaction lưu mã/tên sản phẩm,
  giá cố định hoặc toàn bộ price tiers, ghi chú line, ngày gửi và tiền tệ từ snapshot báo giá.
- Tạo hoặc tái sử dụng một InternalMail conversation có `RelatedType = Quotation`, thêm leader của sale group liên quan
  cùng President/Developer active cùng công ty làm participant và tạo action message.
- Publish notification `QuotationSent` cho leader của sale group liên quan cùng President/Developer, không gửi lại cho chính người thao tác.

Gọi lại endpoint cho báo giá đã `Sent` trả thành công theo semantics idempotent và không tạo thêm interaction,
message hoặc notification.

Đây là xác nhận của sale sau khi đã gửi báo giá. Endpoint không tự gửi email/PDF ra ngoài cho khách hàng.

## 12.1. Yêu cầu báo giá nội bộ

`POST /api/v1/crm/quotations/{quotationId}/request` nhận:

```json
{
  "message": "Vui lòng kiểm tra và cung cấp giá cho báo giá này.",
  "isUrgent": false
}
```

Endpoint chỉ áp dụng cho báo giá `Draft` mà người gọi được phép xem. Mỗi lần gọi tạo một action message mới và
publish notification `QuotationRequested`; endpoint không đổi trạng thái báo giá và không gửi email ra ngoài.

Backend tìm hoặc tạo conversation theo đúng khóa:

```text
CompanyId = current company
RelatedType = Quotation
RelatedId = quotationId
```

Vì vậy các lần yêu cầu tiếp theo của cùng báo giá đều nằm trong một conversation, nhưng có `messageId` và
`notificationId` mới. Người nhận gồm leader active của sale group chứa `quotation.SaleEmployeeId` cùng
President/Developer active trong công ty, loại người thao tác hiện tại.

Response trả `quotationId`, `conversationId`, `messageId`, `notificationId`, `requestedAt`. FE dùng
`conversationId` và `messageId` để mở đúng cuộc trao đổi và đúng vị trí message.

Payload message không chứa route của FE. Thay vào đó payload chứa action nghiệp vụ:

```json
{
  "action": {
    "code": "Quotation.OpenPricingOptions",
    "parameters": {
      "quotationId": "quotation-guid",
      "quotationExternalId": "BBG260700001"
    }
  }
}
```

FE ánh xạ `action.code` sang route phù hợp với từng client. Web gọi
`GET /api/v1/crm/quotations/product-pricing-options?quotationId={quotationId}` để lọc chính xác sản phẩm thuộc báo
giá. `Notification.Link` để trống; backend không phụ thuộc cấu trúc route của FE.

## 13. Quy tắc notification và InternalMail cho báo giá

Mọi notification, trao đổi nội bộ hoặc sự kiện nghiệp vụ liên quan trực tiếp đến một báo giá phải đi theo cùng một contract để FE gom về mục **Báo giá** trong Notification Hub.

### 13.1. Category code, event group và topicCode

- Tất cả notification của báo giá phải có `categoryCode = quotation`.
- Mỗi topic phải có `eventGroupCode` đúng mục đích, ví dụ `request`, `status`, `message`.
- `TopicNotifications` là enum legacy/backend có thể đang lưu dạng số trong database. Khi cần topic mới, chỉ append ở cuối enum với explicit numeric value kế tiếp; không chèn vào giữa, không đổi số, không xóa và không rename topic cũ.
- Mỗi sự kiện nghiệp vụ riêng phải có topic riêng. Không dùng topic chung chung như `*.updated` nếu event có nghĩa rõ như requested, sent, completed, approved, rejected hoặc message created.
- Mọi mapping topic sang `topicCode`, `categoryCode`, `eventGroupCode` và `aggregateType` phải nằm trong `HRM.Domain/Enums/Notifications/NotificationTopicCatalog.cs`; không tạo switch mapping riêng trong handler/service.
- `topicCode` của báo giá dùng namespace `crm.quotation...`, ví dụ:
  - `crm.quotation.sent`
  - `crm.quotation.requested`
  - `crm.quotation.completed`
  - `crm.quotation.approved`
  - `crm.quotation.rejected`
  - `crm.quotation.message.created`

Khi tạo topic báo giá mới, checklist bắt buộc:

1. Kiểm tra số lớn nhất hiện có trong `HRM.Domain/Enums/Notifications/TopicNotifications.cs`.
2. Append enum mới ở cuối với explicit numeric value kế tiếp.
3. Thêm definition vào `NotificationTopicCatalog` với `categoryCode = quotation`, event group phù hợp và `aggregateType = Quotation`.
4. Publish qua `INotificationService.PublishAsync`, không gọi SignalR/Web Push trực tiếp.
5. Set `AggregateId`, `AggregateCode`, `ConversationId`, `MessageId` trên `PublishNotificationRequest` khi có.
6. Cập nhật README này và `HRM.Application/Features/Notifications/README.md` nếu đổi topic/payload/recipient rule.
7. Build và kiểm tra API feed/detail/unread-summary trả đúng các presentation code và context.

Topic yêu cầu báo giá hiện đã được sử dụng:

```csharp
QuotationRequested = 36
```

Ví dụ topic có thể append trong tương lai:

```csharp
QuotationCompleted = 37,
QuotationApproved = 38,
QuotationRejected = 39
```

Ví dụ catalog:

```csharp
TopicNotifications.QuotationCompleted =>
    Quotation("crm.quotation.completed"),
```

### 13.2. Payload tối thiểu

Payload notification của báo giá chỉ là metadata để FE điều hướng/gom thread, không phải nguồn dữ liệu nghiệp vụ chính.

Payload nên dùng camelCase và chứa tối thiểu:

```json
{
  "contentType": "QuotationCompleted",
  "relatedType": "Quotation",
  "relatedId": "quotation-guid",
  "relatedExternalId": "BBG260700001",
  "conversationId": "conversation-guid",
  "messageId": "message-guid",
  "isUrgent": false
}
```

Quy tắc:

- `relatedType` phải là `Quotation` cho mọi event thuộc báo giá.
- `relatedId` là `QuotationId`.
- `relatedExternalId` là mã báo giá như `BBG260700001`.
- Nếu notification đại diện cho một InternalMail thread hoặc message, payload phải có `conversationId` và `messageId` để FE gom feed với conversation thành một dòng.
- Không đưa dữ liệu nhạy cảm, token, cookie, secret, thông tin cross-company hoặc toàn bộ nội dung báo giá vào payload.
- FE muốn xem chi tiết báo giá phải gọi API báo giá/detail đúng quyền; không đọc dữ liệu nghiệp vụ chính từ payload.

### 13.3. Conversation báo giá

Mọi trao đổi gắn với một báo giá phải dùng hoặc tái sử dụng một `InternalConversation` active có:

```text
RelatedType = Quotation
RelatedId = quotationId
RelatedExternalId = quotationExternalId
```

Tiêu đề conversation được đồng bộ theo các mã sản phẩm snapshot đang có trên báo giá:

```text
Báo giá BBG260800001 - TP4909
Báo giá BBG260800001 - TP4909, LL5911
Báo giá BBG260800001 - TP4909, LL5911, WT31004, TL31255
```

Backend ghi đầy đủ các mã theo `SortOrder` và loại mã trùng không phân biệt hoa thường. Việc rút gọn
tiêu đề để hiển thị, ví dụ chỉ hiện một số mã rồi thêm `+N`, thuộc trách nhiệm của FE và không làm thay đổi
subject đang lưu. Khi `PUT /quotations/{quotationId}/lines` thay danh sách dòng, backend chỉ đồng bộ
`InternalConversation.Subject` nếu conversation đã tồn tại; không tự tạo conversation, không sửa body message,
notification hoặc payload lịch sử. Khi `/request` hoặc `/mark-sent` tạo hay tái sử dụng conversation, subject
cũng được dựng lại từ snapshot line hiện tại.

FE ở Notification Hub mục Báo giá sẽ lấy dữ liệu từ hai nguồn:

```http
GET /api/v1/notifications/feed?categoryCode=quotation
GET /api/v1/internal-mail/conversations?relatedType=Quotation&archived=false
```

Sau đó FE gom theo `conversationId` nếu payload notification có trường này. Vì vậy mọi event báo giá có trao đổi thread nên đi qua cùng conversation báo giá, không tạo conversation rời rạc nếu cùng một `quotationId`.

### 13.4. Publish notification từ nghiệp vụ báo giá

FE không được gọi API notification để tự tạo thông báo báo giá. Notification phải là side effect của command nghiệp vụ trong backend, ví dụ:

- `mark-sent`: tạo/tái sử dụng conversation báo giá, tạo action message, publish `QuotationSent`.
- Yêu cầu báo giá: command nghiệp vụ tạo request/message, publish `QuotationRequested`.
- Hoàn thành báo giá: command nghiệp vụ cập nhật trạng thái hoặc message, publish `QuotationCompleted`.
- Duyệt/từ chối báo giá: command nghiệp vụ cập nhật trạng thái hoặc message, publish `QuotationApproved`/`QuotationRejected`.
- Reply trong conversation báo giá: API InternalMail publish `QuotationMessageCreated`.

Recipient phải được resolve trong backend theo rule nghiệp vụ của từng action, ví dụ leader của sale group, President/Developer,
Lab/Sale/participant liên quan. Không lấy danh sách recipient tùy ý từ FE nếu rule cần kiểm soát quyền.

Với báo giá, không dùng role `Leader` global để gửi thông báo vì role này có thể thuộc nhiều phòng ban. Khi cần gửi cho leader sale,
dùng `ISaleGroupRecipientResolver`:

- Leader sale = employee active có `MemberInGroup.IsAdmin = true` trong group sale `CMR` hoặc `CMR.*`.
- Group sale liên quan được lấy từ `quotation.SaleEmployeeId`.
- `President` và `Developer` vẫn là recipient global cùng công ty nếu action cần báo cho cấp quản lý toàn hệ thống.
- Người thao tác hiện tại phải được loại khỏi danh sách nhận notification.

`QuotationSent` và `QuotationRequested` hiện resolve recipient theo rule:

```text
sale group leaders của quotation.SaleEmployeeId
+ President/Developer active cùng company
- sender employee
```

### 13.5. Quy tắc gom hiển thị trên FE

Để tất cả nội dung thuộc một báo giá nằm trong mục Báo giá:

- `categoryCode` backend trả về phải là `quotation`.
- `topicCode` bắt đầu bằng `crm.quotation`.
- Conversation phải có `RelatedType = Quotation`.
- Payload notification có `relatedType = Quotation`.
- Nếu có conversation, payload có `conversationId` để FE gom Notification và InternalMail thành một dòng.

Nếu một notification thuộc báo giá bị hiển thị ở mục khác, kiểm tra theo thứ tự:

1. Topic đó đã có mapping trong `NotificationTopicCatalog` chưa.
2. Mapping có `categoryCode = quotation` và đúng `eventGroupCode` chưa.
3. Handler có publish đúng `TopicNotifications.<QuotationEvent>` chưa.
4. Payload có `relatedType = Quotation`, `relatedId`, `relatedExternalId`, `conversationId` chưa.
5. Conversation có `RelatedType = Quotation` và `RelatedId = quotationId` chưa.

## 14. Giới hạn hiện tại

Chưa có:

- Submit/approve.
- Accept/reject.
- Cancel/restore.
- Revise/version mới.
- API tự gửi email.
- Tự lưu và gửi bản PDF cố định khi `mark-sent`.
- Endpoint xóa báo giá.

Repo hiện chưa chứa migration cho các cột/bảng báo giá mới. Database chạy API phải được đồng bộ schema tương ứng trước khi sử dụng chức năng price tiers.
