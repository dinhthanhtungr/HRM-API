# Báo giá CRM

## 1. Module này dùng để làm gì?

Module Quotation quản lý một báo giá từ lúc sale tạo bản nháp đến khi ghi nhận đã gửi cho khách hàng.

Module hiện hỗ trợ:

- Quản lý bộ luật tính giá Powder/Compound theo phiên bản Draft/Published cho từng công ty.

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
| `GET` | `/api/v1/crm/quotations/pricing-policies` | Xem lịch sử bộ luật tính giá |
| `POST` | `/api/v1/crm/quotations/pricing-policies` | Tạo bản nháp bộ luật tính giá |
| `PUT` | `/api/v1/crm/quotations/pricing-policies/{policyId}` | Sửa bản nháp bộ luật tính giá |
| `POST` | `/api/v1/crm/quotations/pricing-policies/{policyId}/publish` | Công bố bộ luật tính giá |
| `POST` | `/api/v1/crm/quotations/pricing-policies/{policyId}/preview` | Tính thử các tier của Draft/Published |
| `GET` | `/api/v1/crm/quotations/pricing-queue` | Hàng đợi yêu cầu định giá cho President/Developer |
| `GET` | `/api/v1/crm/quotations/{quotationId}/pricing-workspace` | Workspace định giá theo đúng báo giá cho President/Developer |
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

Báo giá `Draft` được phép có line chưa có giá để Sale lưu trước rồi gửi yêu cầu báo giá nội bộ. Line chưa có giá
được lưu với `priceMode = Tiered`, `unitPrice = 0` và `priceTiers = []`; subtotal/tax/total của line đó bằng `0`.
Line có giá phải gửi `productPricingVersionId`; backend tự sao chép tiers từ version `Approved`. Request không có
`productPricingVersionId` nhưng lại gửi `unitPrice` hoặc `priceTiers` sẽ bị từ chối.

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
- `priceMode = Tiered`.
- `productPricingVersionId` của version `Approved`, hoặc để null nếu đang lưu nháp chờ giá.

`sampleRequestId` là tùy chọn nhưng nếu có phải cùng company, customer và product.

### 5.4. Ghi giá mới sau khi xin giá

```http
POST /api/v1/crm/quotations/{quotationId}/refresh-prices
```

Endpoint nhận version giá mới mà Sale đã chọn. Backend tải version `Approved`, kiểm tra company/product/currency,
sao chép tiers vào snapshot của đúng `QuotationLine`, rồi tính lại line total và tổng báo giá.

Ví dụ:

```json
{
  "lines": [
    {
      "quotationLineId": "00000000-0000-0000-0000-000000000000",
      "productPricingVersionId": "00000000-0000-0000-0000-000000000000"
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

Mặc định endpoint lấy tất cả Product active của công ty hiện tại. Product là dữ liệu gốc và nhận thêm
`currency` (mặc định `VND`):

- Có `ProductPricingVersion` Draft/Approved: trả `currentPricing` và không trả Formula để tra cứu giá.
- Không có version giá: trả các nguồn đủ điều kiện trong `pricingSources`.
- Formula chỉ đủ điều kiện khi ở `Approved`, `SampleSent` hoặc `Completed`.
- MFG Formula đủ điều kiện khi đang `IsSelect`, `Processing`, `Completed` hoặc có version `Released`.
- Product chưa có nguồn hợp lệ vẫn được trả để FE hiển thị `NoEligibleSource`.
- Sample Request gần nhất chỉ là dữ liệu bổ sung; không có Sample Request không làm Product biến mất.

Hỗ trợ:

- `requestType`: nếu truyền thì chỉ lấy Product có Sample Request active thuộc loại này.
- `status`: nếu truyền thì chỉ lấy Product có Sample Request active ở trạng thái này.
- `quotationId`: lọc chính xác sản phẩm thuộc một báo giá active trong công ty hiện tại.
- `quotationStatus`: enum trạng thái báo giá; nếu truyền thì chỉ lấy sản phẩm đã nằm trong báo giá active có trạng thái đó.
- `keyword`: tìm theo mã/tên sản phẩm, mã Sample Request, `Formula.ExternalId`, tên Formula hoặc `Quotation.ExternalId`.
  Khi keyword bắt đầu bằng prefix báo giá từ `DocumentPrefix.BBG`, prefix Sample Request từ `DocumentPrefix.TP`
  hoặc prefix Formula từ `DocumentPrefix.VU`, backend chỉ tìm đúng nhóm mã đó bằng `StartsWith`.
- `pageNumber`, `pageSize`.
- `sortBy = externalId | colourCode | productName | updatedDate | createdDate`.
- `sortDirection = asc | desc`.
- `currency`: tiền tệ của bảng giá cần tra cứu, mặc định `VND`.

Nếu không truyền `sortBy`, backend mặc định sắp xếp theo ngày tạo Product giảm dần.
`updatedDate`, `createdDate` và `externalId` dùng dữ liệu Sample Request gần nhất
phù hợp với bộ lọc; Product không có Sample Request vẫn có mặt trong kết quả.

Mỗi sản phẩm trả:

- Thông tin Sample Request gần nhất nếu có; các field này là `null` khi chưa có.
- Mã và tên sản phẩm.
- `pricingStatus = NoEligibleSource | WaitingForPricing | WaitingForApproval | Draft | Approved`.
- `currentPricing`: version giá hiện hành; Sale chỉ nhận version `Approved`, President/Developer thấy Draft mới nhất.
- `hasPricingVersion`, `hasEligiblePricingSource` và `pricingSources`.
- `formulas` chỉ còn dữ liệu chi tiết tương thích cho Formula đủ điều kiện khi Product chưa có version giá.

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

### Bộ luật tính giá theo công ty

`President` và `Developer` có thể tạo một policy `Draft` cho từng tổ hợp
`company + profile + currency`, chỉnh chi phí sản xuất mặc định và các khoảng giá,
sau đó publish. Khi publish, policy đang dùng trước đó chuyển thành `Superseded`.
Policy đã publish không sửa trực tiếp; cần tạo version Draft mới.

`priceOffset` được cộng vào `standardSellingPrice`; số âm là giảm giá và `null`
nghĩa là tier phải nhập giá thủ công. Các khoảng được phép có khoảng trống nhưng
không được chồng lấn. Nếu công ty chưa publish policy, calculator tiếp tục dùng
luật Powder/Compound mặc định để giữ tương thích.

Thay đổi policy không cập nhật ngược `ProductPricingTier` hoặc
`QuotationLinePriceTier` đã lưu. Các bảng này tiếp tục là snapshot lịch sử.

Mỗi `ProductPricingVersion` mới lưu nullable `FormulaPricingPolicyId` để truy vết
policy đã dùng sinh giá. `HasManualTierAdjustment = true` cho biết President hoặc
Developer đã chỉnh tiers sau khi hệ thống tính từ policy. `QuotationLine` truy vết
policy thông qua `ProductPricingVersionId`; không lưu thêm policy trên header báo giá.

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

### 5.7.3. Hàng đợi định giá

```http
GET /api/v1/crm/quotations/pricing-queue?pageNumber=1&pageSize=15&keyword=BBG
```

Endpoint chỉ dành cho `President` và `Developer`. Một báo giá xuất hiện trong hàng đợi khi:

- Báo giá còn `Draft` và nằm trong company/customer visibility hiện tại.
- Đã có action message `QuotationRequested` trong InternalMail.
- Còn ít nhất một line chưa có `ProductPricingVersion` trạng thái `Approved` theo đúng product và currency.

Kết quả luôn sắp theo lần yêu cầu báo giá mới nhất giảm dần, sau đó theo `quotationDate` giảm dần. Response trả
`requestedAt`, số line, số line còn chờ giá và danh sách mã sản phẩm để FE dựng hàng đợi trước khi mở workspace.

### 5.7.4. Workspace định giá theo báo giá

```http
GET /api/v1/crm/quotations/{quotationId}/pricing-workspace
```

Endpoint dành riêng cho `President` và `Developer`, dùng để dựng màn hình định giá theo công việc thay vì bắt FE
ghép quotation detail, pricing options, pricing source và pricing history. Backend kiểm tra company/customer
visibility, sau đó batch-load version giá và nguồn Formula/MFG Formula cho toàn bộ sản phẩm trong báo giá.

Header trả `quotationId`, `quotationExternalId`, khách hàng, Sale phụ trách, currency, status. Mỗi line trả:

- Định danh line và snapshot mã/tên sản phẩm.
- `pricingState = NoEligibleSource | WaitingForPricing | Draft | ApprovedAvailable | Applied`.
- `appliedProductPricingVersionId`, `appliedUnitPrice`, `appliedPriceTiers` đang snapshot trong báo giá.
- `hasNewerApprovedPricing` để Sale biết snapshot hiện tại không còn là version Approved mới nhất.
- `draftPricing`, `approvedPricing` đầy đủ cho President/Developer.
- `pricingSources` đủ điều kiện, đã batch-load để FE không gọi N+1 theo từng sản phẩm. Mỗi nguồn chứa danh sách
  NVL, số lượng chuẩn, đơn giá mới nhất, thành tiền, nguồn giá và ngày giá để dựng tooltip.
- `currentMaterialCost` là tổng NVL theo giá nguồn mới nhất; không lấy `Formula.TotalPrice`.
- `storedMaterialCostSnapshot` và `storedPricingUpdatedDate` là dữ liệu của Draft/Approved gần nhất để FE hiển thị
  nhỏ bên dưới giá hiện tại.
- `effectivePricing` chứa chi phí sản xuất hiệu lực, giá bán tiêu chuẩn, tỷ lệ lợi nhuận và rule tier do backend tính.
  Nếu chi phí sản xuất chưa được lưu, rule hiện tại dùng `10.000` cho Powder và `20.000` cho Compound.
- `displayPriceTiers` dùng tier đã lưu khi version gần nhất có `ProductPricingTiers`; nếu chưa có thì trả tier gợi ý
  từ `FormulaPriceCalculator`. `priceTiersAreStored` và `displayPriceTiers[].isStored` giúp FE phân biệt hai trường hợp.

Thứ tự ưu tiên state là `Applied` -> `ApprovedAvailable` -> `Draft` -> `WaitingForPricing` -> `NoEligibleSource`.
Endpoint chỉ đọc, không tự áp dụng giá vào quotation. Sau khi President approve, Sale vẫn gọi
`POST /api/v1/crm/quotations/{quotationId}/refresh-prices` để chủ động tạo snapshot.

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

Mỗi phần tử `lines[].priceTiers[]` giữ nguyên `unitPrice` là snapshot của chính báo giá đang xem và bổ sung:

- `standardUnitPrice`: giá tier tiêu chuẩn đang có hiệu lực; ưu tiên bảng giá President đã duyệt,
  nếu chưa có thì dùng giá hệ thống tính theo nguồn định giá realtime.
- `standardPriceUpdatedDate`: thời điểm lưu bảng giá đã duyệt; bằng `null` khi giá chuẩn chỉ do hệ thống tính.
- `latestQuotedUnitPrice`: giá cùng tier trong báo giá trước đó gần nhất đã từng gửi.
- `latestQuotedDate`: `sentDate` của báo giá gần nhất nói trên.

Giá báo gần nhất chỉ lấy trong phạm vi báo giá mà người gọi được phép xem, cùng công ty và tiền tệ,
đồng thời không lấy chính báo giá hiện tại.

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

## 6. Bảng giá sản phẩm nội bộ

Giá nội bộ được tách khỏi công thức kỹ thuật và snapshot gửi khách:

- `ProductPricingVersion` lưu một phiên bản giá của `Product + Currency`, có trạng thái
  `Draft`, `Approved`, `Superseded` hoặc `Cancelled`.
- Nguồn ghi mới dùng đúng một cặp `sourceType + sourceId`: `Formula` hoặc `ManufacturingFormula`.
  `SourceFormulaId` và `SourceManufacturingFormulaId` là hai FK tương ứng. Các field nguồn cũ vẫn được giữ
  để đọc dữ liệu lịch sử, nhưng contract ghi mới không nhận chúng.
- Entity/config hiện yêu cầu cột `SourceManufacturingFormulaId`; thay đổi này không kèm migration hoặc script database.
- `ProductPricingTier` lưu các bậc số lượng và đơn giá thuộc một phiên bản giá.
- `QuotationLine.ProductPricingVersionId` chỉ dùng để truy vết nguồn giá. Giá thực sự gửi khách
  vẫn phải được sao chép vào `QuotationLinePriceTiers` để báo giá cũ không đổi khi bảng giá nội bộ thay đổi.

API quản lý bảng giá:

- `GET /api/v1/crm/quotations/product-pricing-workbench?view=NeedsPricing&currency=VND`: danh sách
  Product-centric cho President/Developer. Mỗi `Product + Currency` chỉ có một dòng. Khi không truyền `sortBy`,
  mọi view đều ưu tiên sản phẩm thuộc báo giá đang yêu cầu định giá theo `latestRequestedAt` giảm dần, sau đó
  sắp theo ngày tạo `SampleRequest` mới nhất giảm dần. `view` nhận `NeedsPricing`, `Draft`, `Approved` hoặc `All`;
  mặc định là `All`.
  `President` và `Developer` nhận đầy đủ dữ liệu quản lý giá. `SaleUser` được phép đọc nhưng response bị giới hạn:
  danh sách giữ nguồn công thức cùng `sourceStatus`/`sourceIsEligible`, giá bán tiêu chuẩn, trạng thái bảng giá và số báo giá đang chờ; các field chi phí,
  margin, version id và ngày nội bộ trả `null`/giá trị mặc định. `canOpenPricingDetail = true` cho phép FE giữ thao
  tác mở drawer; `canManagePricing = false` chỉ khóa các thao tác ghi, duyệt và đổi nguồn.
  Có thể gửi `sortBy=createdDate&sortDirection=asc|desc` để đổi thứ tự này.
  `All` chỉ gồm sản phẩm có Formula/MFG Formula đủ điều kiện, đã có `ProductPricingVersion`, hoặc đang nằm trong
  báo giá đã gửi yêu cầu định giá. Product cũ không có nguồn, không có lịch sử giá và không có yêu cầu sẽ bị ẩn;
  sản phẩm đang được yêu cầu nhưng chưa có Formula vẫn được giữ để cảnh báo President.
- `GET /api/v1/crm/quotations/products/{productId}/pricing-workbench?currency=VND`: dữ liệu drawer gồm nguồn
  đang dùng, NVL và giá mới nhất, chênh lệch với snapshot, Draft/Approved, tiers hiển thị, lịch sử version và
  các báo giá đang chờ. Có thể truyền thêm `sourceType=Formula&sourceId={formulaId}` để preview nguồn do FE
  chọn từ Formula lookup trước khi tạo Draft.

  Với `SaleUser`, drawer trả giá bán tiêu chuẩn, `selectedSource` rút gọn gồm `sourceType`, `sourceId`,
  `externalId`, `name` và danh sách tiers chỉ đọc. Tier của Sale chỉ có khoảng khối lượng, đơn giá, cờ cần nhập
  thủ công/cờ đã lưu và thứ tự; `marginVsMaterialPercent`/`marginVsCostPercent` luôn `null`. Chi phí, margin tổng,
  NVL, version Draft/Approved, lịch sử và báo giá liên quan vẫn bị trả `null` hoặc danh sách rỗng. Sale không được
  truyền `sourceType/sourceId` để preview nguồn khác và không có quyền mutation bảng giá.

  Drawer luôn trả trực tiếp `manufacturingCost`, `standardSellingPrice` và `profitMarginRate` là ba giá trị
  hiệu lực giống `summary`. Khi chưa có version DB, các field này được tính từ nguồn realtime và luật giá;
  FE không được bind input từ `draftPricing`/`approvedPricing` vì hai object đó hợp lệ khi null.

  `selectedSource.pricingProfile` luôn cho biết nguồn đang dùng rule `Powder` hay `Compound`, kể cả khi một
  hoặc nhiều NVL chưa có giá và `selectedSource.pricing` phải trả `null`. Trong trường hợp thiếu giá,
  `selectedSource.priceTierTemplates` và `displayPriceTiers` vẫn trả đúng khoảng khối lượng của profile với
  `unitPrice = null`; backend không dùng giá `0` để giả lập phép tính và vẫn không cho duyệt bảng giá.
  Mỗi phần tử `selectedSource.materials[]` trả thêm `categoryId` snapshot từ dòng Formula/MFG Formula để FE
  có thể phân nhóm hoặc mở lookup theo đúng category đã dùng trong công thức.
- `GET /api/v1/crm/quotations/product-pricing-versions?productId={id}&currency=VND`: xem lịch sử version.
- `GET /api/v1/crm/quotations/products/{productId}/pricing-sources`: lấy Formula/MFG Formula đủ điều kiện;
  chỉ President/Developer được gọi.
- `POST /api/v1/crm/quotations/product-pricing-versions`: tạo version `Draft` theo mặc định. Màn President gửi
  `approveImmediately=true` để tạo và duyệt version trong cùng một lần ghi.
- `PUT /api/v1/crm/quotations/product-pricing-versions/{id}`: sửa giá và tiers của một version `Draft`;
  không được đổi nguồn công thức.
- `POST /api/v1/crm/quotations/product-pricing-versions/{id}/approve`: duyệt version và chuyển version `Approved`
  trước đó của cùng `Product + Currency` sang `Superseded`.

Chỉ `President` và `Developer` được xem lịch sử đầy đủ, tạo, sửa hoặc duyệt bảng giá. Endpoint
`GET /products/{productId}/pricing?currency=VND` vẫn cho Sale lấy giá bán đã duyệt nhưng chỉ trả cost/margin
nhạy cảm cho hai role trên. Field `canApplyToQuotation` chỉ true khi có version `Approved` và có tiers.

Ví dụ tạo Draft mới từ Formula:

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "sourceType": "Formula",
  "sourceId": "00000000-0000-0000-0000-000000000000",
  "currency": "VND",
  "approveImmediately": true,
  "materialCostSnapshot": 90000,
  "manufacturingCost": 20000,
  "standardSellingPrice": 140000,
  "profitMarginRate": 27.2727,
  "changedField": "StandardSellingPrice",
  "priceTiers": []
}
```

Chọn một nguồn khác phải gọi `POST` để tạo version mới. Luồng President chuẩn dùng một nút `Lưu giá` và gửi
`approveImmediately=true`: backend kiểm tra nguồn/tiers, tạo version `Approved`, chuyển Approved cũ thành
`Superseded`, chuyển Draft cũ thành `Cancelled` và phát notification cho Sale trong cùng nghiệp vụ. Cặp API
Draft + approve riêng vẫn được giữ để tương thích các màn cũ hoặc quy trình cần lưu nháp thật.

Workbench luôn tính `currentMaterialCost` từ đơn giá NVL mới nhất. Nếu đang có Draft/Approved, nguồn công thức
đã lưu được giữ nguyên để so sánh với `storedMaterialCostSnapshot`; backend không tự chuyển sang công thức khác
khi trạng thái công thức cũ thay đổi. Nguồn cũ khi không còn hợp lệ được trả với `sourceIsEligible = false` để FE
cảnh báo và buộc President chọn nguồn mới trước lần duyệt tiếp theo. Nếu chưa có version, backend ưu tiên Formula
`Approved`, sau đó ưu tiên công thức có cờ khách hàng chọn và ngày cập nhật mới nhất.

Khi chưa có version nhưng có nguồn đủ điều kiện, workbench trả `pricingStatus = Draft`,
`isSystemCalculatedDraft = true` và cả hai version id đều null. Đây chỉ là nháp tính realtime của hệ thống,
không phải bản ghi DB. Sau khi President lưu với `approveImmediately=true`, response và lần GET tiếp theo trả
`pricingStatus = Approved`, `isSystemCalculatedDraft = false` và có `approvedPricingVersionId`.

### 6.1. Cách đọc response Product Pricing Workbench

Ví dụ rút gọn khi Product chưa từng có `ProductPricingVersion`, nhưng đã có Formula hợp lệ và đủ giá NVL:

```json
{
  "summary": {
    "canOpenPricingDetail": true,
    "canManagePricing": true,
    "productCode": "TO21088D",
    "pricingStatus": "Draft",
    "isSystemCalculatedDraft": true,
    "sourceExternalId": "VU260700083",
    "sourceName": "F001",
    "sourceStatus": "SampleSent",
    "sourceIsEligible": true,
    "sourceIsCustomerSelected": true,
    "currentMaterialCost": 260000,
    "isCurrentMaterialCostComplete": true,
    "missingMaterialPriceCount": 0,
    "storedMaterialCostSnapshot": null,
    "manufacturingCost": 10000,
    "usedDefaultManufacturingCost": false,
    "standardSellingPrice": 270000,
    "profitMarginRate": 0,
    "draftPricingVersionId": null,
    "approvedPricingVersionId": null,
    "pricingUpdatedDate": "2026-07-25T08:30:12.259251"
  },
  "manufacturingCost": 10000,
  "standardSellingPrice": 270000,
  "profitMarginRate": 0,
  "draftPricing": null,
  "approvedPricing": null,
  "displayPriceTiers": [
    {
      "quantityRangeLabel": "< 50 kg",
      "unitPrice": 320000,
      "requiresManualPrice": false,
      "isStored": false
    }
  ],
  "pricingHistory": [],
  "relatedQuotations": []
}
```

Ý nghĩa nghiệp vụ:

| Field/nhóm field | Ý nghĩa và nguồn dữ liệu |
|---|---|
| `canOpenPricingDetail` | Cho phép FE mở drawer. Đây không đồng nghĩa với quyền sửa. |
| `canManagePricing` | Cho phép đổi nguồn, nhập và lưu/duyệt giá. President/Developer là nhóm có quyền đầy đủ hiện tại. |
| `pricingStatus = Draft` + `isSystemCalculatedDraft = true` | Nháp ảo do hệ thống tính realtime; chưa có record `ProductPricingVersion`. Không được hiểu `Draft` một mình là đã lưu DB. |
| `draftPricingVersionId`/`approvedPricingVersionId` | Id record đã persist. Cả hai `null` trong ví dụ xác nhận chưa có version DB. |
| `source*` | Nguồn kỹ thuật được chọn để tính giá. Ví dụ dùng Formula `VU260700083 - F001`; `sourceIsEligible` cho biết còn đủ điều kiện định giá, `sourceIsCustomerSelected` là cờ công thức khách hàng chọn. |
| `currentMaterialCost` | Tổng realtime `SUM(material.quantity * latestUnitPrice)`. Không đọc từ snapshot cũ của Formula/ProductPricingVersion. |
| `isCurrentMaterialCostComplete`/`missingMaterialPriceCount` | Cho biết mọi NVL đã có đơn giá hay chưa. Khi thiếu giá, cost/pricing/tier tính toán có thể là `null`; không thay `null` bằng `0`. |
| `storedMaterialCostSnapshot` | Chi phí NVL đã lưu trong Draft/Approved gần nhất. `null` nghĩa là chưa có version để so sánh. |
| `materialCostDifference*` | Chỉ có khi đồng thời có current cost và stored snapshot; dùng so sánh biến động giá NVL, không phải lợi nhuận. |
| `manufacturingCost` | Chi phí sản xuất hiệu lực. `usedDefaultManufacturingCost` cho biết đang dùng mức mặc định theo profile hay giá có sẵn từ nguồn/version. |
| `standardSellingPrice` | Giá bán hiệu lực do backend tính từ realtime cost và version đang có. Trong ví dụ: `260000 + 10000 = 270000`. |
| `profitMarginRate` | `(standardSellingPrice - costBase) / costBase * 100`, với `costBase = currentMaterialCost + manufacturingCost`. |
| Ba field giá ở top-level | Là giá canonical để FE bind vào ba input trong drawer; chúng phải giống giá hiệu lực trong `summary`. Không bind từ `draftPricing`/`approvedPricing` vì hai object có thể `null`. |
| `selectedSource` | Chi tiết nguồn đang preview/dùng: profile, NVL, giá mới nhất, tooltip nguồn/ngày giá và kết quả `FormulaPriceCalculator`. `selectedSource.materialCostSnapshot` hiện là snapshot ứng viên bằng realtime cost để phục vụ flow lưu, không chứng minh đã persist; muốn biết đã lưu phải xem version id/object. |
| `draftPricing`/`approvedPricing` | Bản ghi DB mới nhất theo từng trạng thái. `null` nghĩa là không có version tương ứng. |
| `displayPriceTiers` | Tier dùng để hiển thị. `isStored = true` là tier từ `ProductPricingTier`; `false` là tier hệ thống gợi ý. `requiresManualPrice = true` cùng `unitPrice = null` nghĩa là President phải nhập thủ công. |
| `pricingHistory` | Các version đã lưu trước đó. Danh sách rỗng nghĩa là chưa có lịch sử, không phải lỗi tải dữ liệu. |
| `relatedQuotations` | Các báo giá đang chờ định giá liên quan đến Product. Danh sách rỗng tương ứng `waitingQuotationCount = 0`. |
| `pricingUpdatedDate` | Ưu tiên ngày của version đã lưu; nếu chưa có version thì fallback về ngày cập nhật nguồn Formula/MFG Formula. Vì vậy không luôn là ngày President lưu giá. |

Các mốc để FE phân biệt nguồn dữ liệu:

```text
Nháp hệ thống, chưa lưu:
pricingStatus = Draft
isSystemCalculatedDraft = true
draftPricingVersionId = null
approvedPricingVersionId = null
displayPriceTiers[].isStored = false

Draft đã lưu:
isSystemCalculatedDraft = false
draftPricingVersionId != null
draftPricing != null

Giá đã duyệt:
pricingStatus = Approved
approvedPricingVersionId != null
approvedPricing != null
```

Đối với quyền hạn chế như Sale, các field cost/margin/material/history có thể bị `null` hoặc trả danh sách rỗng
do visibility rule. FE phải dùng `canOpenPricingDetail` và `canManagePricing` để quyết định mở drawer/read-only,
không suy quyền từ việc một field tình cờ có giá trị.

`POST` và `PUT` nhận thêm `changedField` dạng string enum:

```text
ManufacturingCost
StandardSellingPrice
ProfitMarginRate
```

Backend là nguồn tính giá chính thức. `MaterialCostSnapshot` và `CalculatedAt` trong request được giữ để tương
thích client cũ nhưng không được tin khi ghi: backend lấy chi phí NVL realtime từ `sourceType + sourceId` và dùng
thời gian server. Quy tắc chuẩn hóa:

- Giá do hệ thống tính (`currentMaterialCost`, thành tiền NVL, cost base, standard selling price và tier gợi ý)
  được làm tròn về số nguyên bằng `MidpointRounding.AwayFromZero`.
- Giá do người dùng nhập và lưu được giữ phần thập phân tối đa theo precision hiện có; backend không ép về số nguyên.
- `ProductPricingTier.UnitPrice` và `QuotationLinePriceTier.UnitPrice` cho phép bằng `0`, chỉ giá âm bị từ chối.

```text
costBase = materialCostRealtime + manufacturingCost

changedField = ManufacturingCost:
    giữ profitMarginRate hiện tại, tính lại standardSellingPrice

changedField = StandardSellingPrice:
    profitMarginRate = (standardSellingPrice - costBase) / costBase * 100

changedField = ProfitMarginRate:
    standardSellingPrice = costBase * (1 + profitMarginRate / 100)
```

`changedField` tạm thời nullable để tương thích client cũ. Với `PUT`, nếu không gửi thì backend suy luận từ field
khác dữ liệu đang lưu; khi không phân biệt được, `StandardSellingPrice` được ưu tiên làm giá trị chính. Client mới
phải gửi `changedField` rõ ràng. Response của `POST`/`PUT` trả lại toàn bộ giá đã được backend chuẩn hóa và FE phải
thay state đang hiển thị bằng response đó.

Khi approve, backend lấy lại material cost realtime lần cuối, giữ nguyên `StandardSellingPrice` President đã chốt,
tính lại `ProfitMarginRate`, ghi `MaterialCostSnapshot`/`CalculatedAt` rồi mới chuyển version sang `Approved`.
Template tier có thể nằm ở FE để khởi tạo, nhưng sau lần lưu đầu tiên phải gửi toàn bộ tiers qua `PUT` và những lần
mở lại phải đọc tiers đã lưu trong DB.

## 7. Quy tắc giá theo khối lượng của dòng sản phẩm

Contract ghi mới hiện chỉ chấp nhận `Tiered`. `Fixed` được giữ để đọc dữ liệu lịch sử. Create/replace cho phép
line nháp chưa có giá khi không gửi `productPricingVersionId`; trường hợp này không được gửi `unitPrice` hoặc tiers.
Khi có `productPricingVersionId`, backend kiểm tra version phải `Approved`, đúng company, product và currency rồi
tự sao chép toàn bộ `ProductPricingTiers` vào snapshot `QuotationLinePriceTiers`; không tin giá do FE gửi.

Refresh price nhận `quotationLineId + productPricingVersionId`, sau đó cũng sao chép snapshot từ backend.
Đổi currency bị từ chối khi báo giá đã có snapshot để tránh trộn hai loại tiền tệ.

`mark-sent` từ chối báo giá có line Fixed, thiếu tiers, tier không dương hoặc không tham chiếu version đang
`Approved` của đúng company/product/currency. PDF chỉ đọc snapshot; line draft chưa có giá hiển thị trạng thái
`Chờ duyệt giá / Pending pricing` và không tự tính lại từ Formula.

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

Yêu cầu nội bộ được phép gửi khi một hoặc nhiều line chưa có giá. Báo giá vẫn giữ trạng thái `Draft` để quản lý
bổ sung giá và Sale tiếp tục chỉnh sửa.

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
`GET /api/v1/crm/quotations/{quotationId}/pricing-workspace` để lấy toàn bộ dữ liệu màn hình định giá trong một
request. `Notification.Link` để trống; backend không phụ thuộc cấu trúc route của FE.

Khi một `ProductPricingVersion` được approve, backend publish `QuotationPricingApproved` riêng cho từng báo giá
`Draft` active cùng company, currency và có line thuộc sản phẩm vừa duyệt. Người nhận là Sale phụ trách báo giá,
phải active, cùng company và khác người duyệt. Notification không tạo InternalMail message mới và không tự áp dụng
giá; payload chỉ chứa quotation/product/version cùng `action.code = Quotation.Open` để Sale mở báo giá rồi chủ động
gọi `refresh-prices`.

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

Khi Lab đổi `Product.ColourCode` qua PATCH Sample Request, backend tự đồng bộ mã mới vào
`ProductExternalIdSnapshot` của mọi line thuộc báo giá `Draft` đang tham chiếu sản phẩm đó và dựng lại subject
conversation. Báo giá được cập nhật `UpdatedDate` để FE đang giữ dữ liệu cũ phải tải lại trước khi ghi tiếp.
Báo giá không còn ở `Draft`, body message, message reference, notification và payload lịch sử không bị thay đổi.

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
