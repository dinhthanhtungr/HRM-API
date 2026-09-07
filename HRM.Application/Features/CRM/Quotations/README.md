# Báo giá CRM

## 1. Module này dùng để làm gì?

Module Quotation quản lý một báo giá từ lúc sale tạo bản nháp đến khi ghi nhận đã gửi cho khách hàng.

Module hiện hỗ trợ:

- Quản lý bộ luật tính giá Powder/Compound theo phiên bản Draft/Published cho từng công ty.

Khi hệ thống tính giá tham khảo, policy gắn đúng `CategoryId` của sản phẩm được ưu tiên và không xét
`Profile` legacy. Chỉ khi không có policy cho category đó, hệ thống mới fallback sang policy chung
(`CategoryId = null`) theo `Profile` Powder/Compound.

API policy trả cả tier đang tắt với `isActive = false` để màn quản lý chỉnh lại được; engine tính giá chỉ dùng
tier có `isActive = true`.

- Tra cứu sản phẩm đã phát triển xong, công thức, nguyên vật liệu và giá tham khảo.
- Tạo báo giá nháp.
- Sửa thông tin chung của báo giá.
- Thay toàn bộ danh sách sản phẩm.
- Lưu các bậc giá theo khối lượng.
- Cập nhật lại giá sau khi sale đã xin giá bên ngoài.
- Tính subtotal, chiết khấu, VAT và tổng thanh toán.
- Điều phối duyệt giá chuẩn, snapshot giá đã duyệt và ghi nhận báo giá đã gửi.
- Xem danh sách và chi tiết báo giá.

Module hiện **không** tự gửi email và không theo dõi khách hàng chấp nhận/từ chối. Trạng thái `Approved`
chỉ có nghĩa toàn bộ sản phẩm đã có giá chuẩn nội bộ được duyệt, không phải khách hàng đã chấp nhận.

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
POST request
        |
        | Draft -> PendingApproval; BE chờ đủ giá chuẩn
        v
President/Developer approve ProductPricingVersion
        |
        | BE gắn nguồn giá chuẩn, bổ sung tier còn thiếu; PendingApproval -> Approved
        v
PUT customer-price-tiers
        |
        | Sale chỉnh giá thực gửi khách nhưng không đổi product/source
        v
POST mark-sent
        |
        | Draft/PendingApproval/Approved -> Sent khi tier gửi khách hợp lệ
        v
Quotation chuyển sang Sent
```

State machine do backend kiểm soát:

```text
Draft -> PendingApproval -> Approved -> Sent
PendingApproval/Approved -> Draft khi Sale thu hồi
Approved -> PendingApproval khi giá chuẩn hết hiệu lực
Draft/PendingApproval/Approved -> Sent khi Sale xác nhận đã gửi khách
```

## 4. Danh sách API

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/v1/crm/quotations` | Tạo báo giá nháp |
| `PATCH` | `/api/v1/crm/quotations/{quotationId}` | Sửa thông tin chung |
| `PUT` | `/api/v1/crm/quotations/{quotationId}/lines` | Thay toàn bộ dòng sản phẩm |
| `POST` | `/api/v1/crm/quotations/{quotationId}/refresh-prices` | Ghi giá mới cho các dòng |
| `POST` | `/api/v1/crm/quotations/{quotationId}/request` | Gửi yêu cầu báo giá nội bộ |
| `PUT` | `/api/v1/crm/quotations/{quotationId}/customer-price-tiers` | Sale sửa tier thực gửi khách khi đang chờ hoặc đã đủ giá chuẩn |
| `POST` | `/api/v1/crm/quotations/{quotationId}/withdraw-pricing-request` | Sale thu hồi yêu cầu về Draft |
| `POST` | `/api/v1/crm/quotations/{quotationId}/mark-sent` | Ghi nhận đã gửi khách hàng |
| `GET` | `/api/v1/crm/quotations` | Lấy danh sách báo giá |
| `GET` | `/api/v1/crm/quotations/customer-terms?customerId={customerId}` | Gợi ý terms khi FE chọn khách hàng |
| `GET` | `/api/v1/crm/quotations/product-pricing-options` | Tra cứu sản phẩm/công thức/giá |
| `GET` | `/api/v1/crm/quotations/pricing-policies` | Xem lịch sử bộ luật tính giá |
| `POST` | `/api/v1/crm/quotations/pricing-policies` | Tạo bản nháp bộ luật tính giá |
| `PUT` | `/api/v1/crm/quotations/pricing-policies/{policyId}` | Cập nhật trực tiếp policy hiện có và công bố ngay |
| `POST` | `/api/v1/crm/quotations/pricing-policies/{policyId}/publish` | Công bố bộ luật tính giá |
| `POST` | `/api/v1/crm/quotations/pricing-policies/{policyId}/preview` | Tính thử các tier của Draft/Published |

Trong response của pricing policy, tier chưa cấu hình `PriceOffset` trả `priceOffset = 0`
để FE luôn nhận một giá trị số, đồng thời `requiresManualPrice = true` để không nhầm với
offset 0 đã được cấu hình chủ đích. Khi ghi policy, request vẫn có thể gửi
`priceOffset = null` để biểu thị tier cần nhập giá thủ công.
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

- Khách hàng, địa chỉ snapshot (`customerAddressSnapshot`) và người liên hệ
  (`contactId`, `contactName`, `contactPhone`).
- Tiền tệ, tỷ giá.
- `taxPercent` áp dụng cho toàn báo giá.
- Ngày báo giá và hạn hiệu lực.
- Điều khoản thanh toán/giao hàng.
- Ghi chú.
- `terms`: tối đa 20 điều khoản song ngữ tùy chỉnh để snapshot và in PDF.

`contactName` và `contactPhone` là snapshot trên Quotation. Khi create/PATCH có `contactId` nhưng không gửi hai
field snapshot, backend lấy tên và số điện thoại hiện tại từ Contact. Giá trị FE gửi được ưu tiên; `contactPhone`
tối đa 50 ký tự. Với PATCH, không gửi field nghĩa là giữ nguyên, còn gửi chuỗi trắng nghĩa là xóa snapshot.
`GET /{quotationId}` trả lại `contactPhone`; PDF ưu tiên snapshot này và chỉ fallback `Contact.Phone` cho dữ liệu cũ.

`customerAddressSnapshot` cũng là dữ liệu snapshot, tối đa 1.000 ký tự. Khi create không gửi field, backend lấy
`Customer.RegistrationAddress`; khi đổi Customer bằng PATCH mà không gửi địa chỉ, backend tự snapshot địa chỉ của
Customer mới. PATCH không gửi field thì giữ nguyên, gửi chuỗi trắng thì xóa. GET detail trả field này và PDF ưu tiên
snapshot, chỉ fallback về `Customer.RegistrationAddress` cho dữ liệu cũ.

### 5.1.1. Gợi ý terms theo khách hàng

```http
GET /api/v1/crm/quotations/customer-terms?customerId={customerId}
```

FE gọi endpoint này khi người dùng chọn customer trước khi tạo quotation. Backend chỉ tìm quotation thuộc cùng
company và customer mà người gọi được quyền xem; không dùng dữ liệu customer/quotation ngoài phạm vi đó. Nếu có
quotation gần nhất chứa ít nhất một term active, response trả các term active theo `sortOrder`, kèm
`sourceQuotationId`, `sourceQuotationExternalId`, `sourceQuotationDate` và `usedDefaultTerms = false`.

Nếu customer chưa có term active trong lịch sử quotation, response có `sourceQuotationId = null` và
`usedDefaultTerms = true`, với 6 terms mặc định: giao hàng 7 ngày, kho khách hàng, bao PP 25kg, tối thiểu 1000kg,
thanh toán ngay và hiệu lực 15 ngày kể từ ngày backend tạo response. Các terms này chỉ là gợi ý cho form; FE phải gửi
chúng trong `POST /api/v1/crm/quotations` để lưu thành snapshot của quotation mới. Endpoint không tạo hoặc sửa dữ liệu.

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "sourceQuotationId": null,
  "sourceQuotationExternalId": null,
  "sourceQuotationDate": null,
  "usedDefaultTerms": true,
  "terms": [
    {
      "quotationTermId": "00000000-0000-0000-0000-000000000000",
      "labelVi": "Thời hạn giao hàng",
      "labelEn": "Delivery date (from PO receipt)",
      "valueVi": "7 ngày",
      "valueEn": "7 days",
      "sortOrder": 0,
      "isActive": true
    }
  ]
}
```

`quotationTermId = 00000000-0000-0000-0000-000000000000` trong default response nghĩa là term chưa được lưu.
Khi dùng lịch sử customer, ID khác rỗng và chỉ dùng để truy vết quotation nguồn; FE không gửi ID này trong create request.
- Danh sách `lines` ban đầu.

Backend thực hiện:

- Kiểm tra khách hàng thuộc phạm vi được xem.
- Tạo báo giá trạng thái `Draft`.
- Tự sinh mã prefix `BG` theo tháng nếu không có `externalId`.
- Snapshot dữ liệu dòng sản phẩm.
- Tính lại toàn bộ tổng tiền.
- Ghi audit người tạo và thời gian tạo.

Mỗi line phải có ít nhất một `priceTiers` đang bật (`isActive = true`). Backend kiểm tra cấu trúc khoảng giá nhưng
không bắt buộc tier phải bao phủ `quantity`, sau đó lưu toàn bộ tiers thành snapshot. `QuotationLine.UnitPrice` và `LineTotal`
không lấy từ tier: nếu line tham chiếu bảng giá đã duyệt thì dùng `ProductPricingVersion.StandardSellingPrice`; nếu là
giá thủ công được cho phép thì dùng `QuotationLineRequest.UnitPrice`. Việc Sale sửa customer tiers không làm đổi giá
chuẩn và tổng tiền của line.
Không còn hỗ trợ line nháp không có giá hoặc giá cố định trực tiếp.

Mỗi tier có `commissionAmount >= 0`, mặc định `0`. FE chỉ gửi `unitPrice` và `commissionAmount`; không gửi
`customerUnitPrice`. Backend tính và lưu `customerUnitPrice = unitPrice + commissionAmount` trong cùng thao tác ghi
tier. Response detail trả cả ba giá. PDF, nội dung xác nhận đã gửi và giá báo gần nhất dùng trực tiếp
`customerUnitPrice`; `unitPrice` tiếp tục là giá gốc chưa gồm hoa hồng.

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
- Danh sách `terms` tùy chỉnh.
- Ghi chú.

Ở `Draft` có thể sửa toàn bộ các field trên. Ở `PendingApproval` và `Approved`, người dùng nhìn thấy khách hàng theo CRM visibility được sửa contact,
`validUntil`, VAT, payment/delivery terms và note. Customer, currency, exchange rate và quotation date bị khóa;
product/line/source vẫn chỉ sửa được ở `Draft`. `Sent` không cho PATCH.

`terms` có replace semantics rõ ràng: không gửi field thì giữ nguyên; gửi danh sách mới thì các term active cũ
được tắt và danh sách mới được lưu; gửi `[]` là tắt toàn bộ điều khoản tùy chỉnh. Mỗi item gồm
`labelVi`, `labelEn`, `valueVi`, `valueEn`, `sortOrder`, `isActive`; `labelVi` và ít nhất một value là bắt buộc.

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
- Ít nhất một `priceTiers` có `isActive = true`; tier không bắt buộc phải khớp `quantity`.
- `productPricingVersionId` là tùy chọn, dùng để lưu nguồn version giá đã duyệt nếu Sale chọn nguồn đó.

Backend kiểm tra khoảng khối lượng rồi lưu đúng các đơn giá Sale nhập thành snapshot của báo giá;
`QuotationLine.UnitPrice` lấy từ `ProductPricingVersion.StandardSellingPrice` khi dùng bảng giá đã duyệt, hoặc từ
`QuotationLineRequest.UnitPrice` khi dùng giá thủ công được cho phép. Tier khớp quantity không quyết định giá line.
Việc sửa giá trong báo giá không cập nhật ngược `ProductPricingVersion`.

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

- Người dùng nhìn thấy khách hàng theo CRM visibility được gọi. Cho phép `Draft`, `PendingApproval` hoặc `Approved` chuyển sang `Sent`.
- Ghi `SentDate`.
- Thêm `QuotationStatusHistory`.
- Có thể lưu note từ request.

Endpoint này **không gửi email và không tạo file báo giá**.

Gọi lại với báo giá đã `Sent` là idempotent: không tạo thêm history trùng.

### 5.5.1. Gửi yêu cầu và tự hoàn tất duyệt giá

`POST /api/v1/crm/quotations/{quotationId}/request` chỉ nhận báo giá `Draft` có line active và nằm trong CRM visibility của người gọi.
Backend chuyển sang `PendingApproval`, ghi history, tạo action message/topic `QuotationRequested`, sau đó kiểm tra
ngay mọi line. Một line đủ điều kiện khi có `ProductPricingVersion` VND active, `Approved`, cùng company/product,
`StandardSellingPrice > 0`, có tier active không âm và chưa quá hạn rà soát. Khi toàn bộ line đủ, backend snapshot
version id và tính lại totals rồi chuyển báo giá sang `Approved` ngay. Với quotation khác VND, backend đổi giá chuẩn
và tier VND sang tiền tệ báo giá bằng `giá báo giá = giá VND / exchangeRate` trước khi snapshot. Tier Sale đã nhập
trong lúc chờ được giữ làm giá thực gửi khách; nếu line chưa có tier hợp lệ, backend mới fallback snapshot tier từ
bảng giá chuẩn đã quy đổi.

Response bổ sung `quotationStatus` và `updatedDate`. Worker lifecycle chạy lại reconciliation theo giờ để phục hồi
trường hợp side effect tức thời bị gián đoạn.

```json
{
  "quotationId": "00000000-0000-0000-0000-000000000000",
  "conversationId": "00000000-0000-0000-0000-000000000000",
  "messageId": "00000000-0000-0000-0000-000000000000",
  "notificationId": "00000000-0000-0000-0000-000000000000",
  "requestedAt": "2026-08-29T10:00:00+07:00",
  "quotationStatus": 10,
  "updatedDate": "2026-08-29T10:00:00+07:00"
}
```

`quotationStatus` dùng enum số hiện hành (`Draft=0`, `PendingApproval=10`, `Approved=20`, `Sent=30`). Giá trị
`Approved` ngay trong response nghĩa là mọi line đã được snapshot thành công. `updatedDate`
luôn là token persist mới nhất, không phải thời gian notification.

### 5.5.2. Sửa tier gửi khách và thu hồi

`PUT /api/v1/crm/quotations/{quotationId}/customer-price-tiers` nhận báo giá `PendingApproval` hoặc `Approved` và
cho phép người dùng nhìn thấy khách hàng theo CRM visibility thao tác. Sale có thể chuẩn bị rồi gửi giá khách trong lúc President/Developer tiếp tục xử lý
giá chuẩn nội bộ; trạng thái giá chuẩn không chặn `mark-sent`.
Request gồm `expectedUpdatedDate` và các line `{ quotationLineId, priceTiers, note }`. Endpoint chỉ thay snapshot
tier gửi khách/note, giữ nguyên product và `ProductPricingVersionId`, validate khoảng không chồng lấn và giá `>= 0`,
sau đó tính lại totals. Response là `QuotationTotalsDto` có token `updatedDate` mới.

```json
{
  "expectedUpdatedDate": "2026-08-29T10:00:00+07:00",
  "lines": [
    {
      "quotationLineId": "00000000-0000-0000-0000-000000000000",
      "priceTiers": [
        {
          "quantityRangeLabel": "50 - 100 kg",
          "minQuantity": 50,
          "maxQuantity": 100,
          "minInclusive": true,
          "maxInclusive": true,
          "unitPrice": 120000,
          "commissionAmount": 5000,
          "sortOrder": 0,
          "isActive": true
        }
      ],
      "note": "Giá riêng cho lần gửi này"
    }
  ]
}
```

`priceTiers = []` bị từ chối; `unitPrice = 0` và `commissionAmount = 0` hợp lệ, giá âm bị từ chối.
`commissionAmount` là tiền hoa hồng trên mỗi đơn vị, không ghi đè `unitPrice`; `customerUnitPrice` do BE tính và
không thuộc request contract. `note = null` hoặc chuỗi trắng xóa note
của line vì đây là PUT contract đầy đủ cho line được gửi. Các line không xuất hiện trong request được giữ nguyên.

`POST /api/v1/crm/quotations/{quotationId}/withdraw-pricing-request` nhận `expectedUpdatedDate` và `reason` bắt buộc,
tối đa 500 ký tự. Người dùng nhìn thấy khách hàng theo CRM visibility được chuyển `PendingApproval` hoặc `Approved` về `Draft`; snapshot cũ được giữ để
tham khảo. Backend ghi history và system message trong thread. `Sent` không thể thu hồi.

Quyền mutation của các endpoint trên dùng cùng CRM customer visibility với list/detail: Sale thường chỉ thao tác trên khách hàng trong scope của mình, leader thao tác trong các group mình quản lý, còn user có full customer view như Sale Admin, President hoặc Developer thao tác trên mọi khách hàng cùng công ty. Không endpoint nào trong nhóm này còn bắt buộc người gọi phải trùng `Quotation.SaleEmployeeId`.

```json
{
  "expectedUpdatedDate": "2026-08-29T10:05:00+07:00",
  "reason": "Khách thay đổi sản phẩm"
}
```

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
- `approvedStandardSellingPrice`: giá chuẩn của version `Approved` mới nhất có giá lớn hơn 0.
- `approvedStandardSellingPriceEffectiveFrom`: thời điểm President/Developer duyệt giá, lấy từ `ApprovedAt`.
- `systemCalculatedStandardSellingPrice`: giá realtime của nguồn định giá hợp lệ được backend ưu tiên
  (nguồn khách chọn trước, sau đó theo loại nguồn và ngày cập nhật).
- `standardSellingPrice`: giá FE nên hiển thị, ưu tiên `approvedStandardSellingPrice`; chỉ fallback sang
  `systemCalculatedStandardSellingPrice` khi chưa có giá được duyệt.
- `standardSellingPriceSource = ApprovedPricingVersion | SystemCalculated | Unavailable` cho biết nguồn của
  `standardSellingPrice`; FE không tự suy nguồn từ `currentPricing` hoặc `formulas`.
- `hasPricingVersion`, `hasEligiblePricingSource` và `pricingSources`.
- `formulas` chỉ còn dữ liệu chi tiết tương thích cho Formula đủ điều kiện khi Product chưa có version giá.

Các field giá chuẩn tổng hợp chỉ được trả cho role được phép mở Product Pricing Workbench
(`SaleUser`, `President`, `Developer`). User khác nhận giá `null` và source `Unavailable`. Cost NVL, chi phí sản
xuất và margin vẫn theo field visibility cũ; việc được xem giá bán tiêu chuẩn không cấp quyền xem chi phí nội bộ.

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
- `standardSellingPrice`, `profitMarginRate` và tiers trả `null` nếu không thể tính từ
  policy Published và material cost realtime.

Contract giá của mỗi Formula lấy từ `FormulaPricingEngine`: manufacturing cost mặc định,
profit margin, rounding và tiers đều thuộc policy DB. `Formula.ProductionPrice` và
`Formula.PresidentPrice` chỉ còn là field legacy read-only để hiển thị lịch sử, không là
nguồn giá chính và không được dùng làm fallback.

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
`company + profile + currency`. Request tạo/sửa phải luôn có `name`, currency ISO 3 ký tự,
`defaultManufacturingCost`, `defaultProfitMarginRate`, `roundingRule`,
`roundingIncrement`, `effectiveFrom` và tiers. Mỗi bản nháp nhận `version` kế tiếp trong
tổ hợp đó. Policy đã publish không sửa trực tiếp; cần tạo version Draft mới.

Provider chỉ resolve policy `Published`, `isActive = true`, đúng company/profile/currency
và có `effectiveFrom <= thời điểm hiện tại`. Khi publish, policy Published cũ trong cùng tổ
hợp chuyển thành `Superseded`. Không có policy phù hợp thì flow tạo Product Pricing Version
trả error code `PricingPolicyMissing`, không dùng giá/tier mặc định từ calculator.

`Product.FormulaPricingProfile` là profile được cấu hình tường minh cho sản phẩm; flow mới
không suy profile từ product code hoặc `Additive`. Product chưa cấu hình profile sẽ bị từ chối
trước khi resolve policy.

`priceOffset` được cộng vào `standardSellingPrice`; số âm là giảm giá và `null`
nghĩa là tier phải nhập giá thủ công. Các khoảng được phép có khoảng trống nhưng
không được chồng lấn. `roundingRule` (`Nearest`, `Up`, `Down`) cùng
`roundingIncrement` quyết định cách làm tròn cost, standard price và tier price;
`defaultProfitMarginRate` chỉ dùng khi preview/tạo đề xuất chưa có giá bán tiêu chuẩn.

Thay đổi policy không cập nhật ngược `ProductPricingTier` hoặc
`QuotationLinePriceTier` đã lưu. Các bảng này tiếp tục là snapshot lịch sử.

Mỗi `ProductPricingVersion` mới lưu `FormulaPricingPolicyId`; response trả thêm
`formulaPricingPolicyVersion` từ policy được gắn để truy vết chính xác luật đã dùng sinh giá.
`HasManualTierAdjustment = true` khi ít nhất một giá tier khác giá policy gợi ý hoặc policy có tier
`priceOffset = null` cần nhập thủ công. `QuotationLine` truy vết
policy thông qua `ProductPricingVersionId`; không lưu thêm policy trên header báo giá.

Endpoint yêu cầu user đã đăng nhập và tự giới hạn dữ liệu theo company hiện tại.

### Pricing Engine dùng chung

`HRM.Application/Commons/Pricing/Services/FormulaPricingEngine` là boundary dùng chung
cho cả API đọc và luồng ghi/duyệt giá.
Engine nhận company, product/source reference, profile đã cấu hình tường minh, currency,
material cost hoặc danh sách material realtime, manufacturing override, selling price,
profit margin và `changedField`.

Engine batch-resolve policy bằng key `companyId + profile + currency`, chỉ nhận Published
policy còn active và đã hiệu lực. Result luôn mang `formulaPricingPolicyId` và version,
profile/source, trạng thái đầy đủ giá nguyên vật liệu, cost base, margin, giá chuẩn và tiers
gợi ý. `PricingPolicyMissing` là lỗi ổn định khi không resolve được policy; thiếu giá material
không fallback mà trả `isMaterialCostComplete = false` cùng `missingMaterialPriceCount`.
Mọi làm tròn và tier manual (`priceOffset = null`) lấy từ policy definition.

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
GET /api/v1/crm/quotations/products/{productId}/pricing?currency=VND&customerId={customerId}
```

`currency` là bắt buộc; `customerId` là tùy chọn. Backend kiểm tra product active, company scope
và customer visibility. `customerId` chỉ dùng để tìm giá đã gửi gần nhất của đúng khách hàng;
nếu không truyền thì `latestQuotedPricing = null`.

Backend chọn nguồn công thức theo rule chung của Product Pricing Workbench. Response là một
**line preview** chưa persist và tách rõ bốn phần giá:

- `priceTiers`: tier khởi tạo cho cột **Giá báo lần này**. Sale sửa rồi gửi lại trong
  create/replace lines để lưu snapshot.
- `approvedPricing`: giá chuẩn mới nhất President đã duyệt, gồm version, status, `approvedAt`,
  nguồn và tiers. Chưa có version Approved thì object này là `null`.
- `systemCalculatedPricing`: tier hệ thống tính realtime từ nguồn công thức, giá NVL và pricing
  policy hiện hành. Object này độc lập với giá Approved để FE so sánh.
- `latestQuotedPricing`: snapshot tier của báo giá `Sent` gần nhất, cùng customer, product và
  currency. Chưa từng gửi hoặc không truyền `customerId` thì object này là `null`.

`defaultPriceTierSource` giải thích nguồn dùng để tạo `priceTiers`:

- `ApprovedPricingVersion`: ưu tiên `approvedPricing.priceTiers`.
- `SystemCalculated`: chưa có tier Approved, dùng `systemCalculatedPricing.priceTiers`.
- `null`: cả hai nguồn đều không có tier.

API không trả `defaultPriceTiers` vì nội dung đó trùng với `priceTiers`.

```json
{
  "productId": "product-guid",
  "productExternalId": "TP4909",
  "productName": "Hạt màu",
  "currency": "VND",
  "defaultPriceTierSource": "ApprovedPricingVersion",
  "canApplyToQuotation": true,
  "priceTiers": [
    {
      "isSnapshot": false,
      "requiresManualPrice": false,
      "quantityRangeLabel": "< 50 kg",
      "unitPrice": 215000,
      "standardUnitPrice": 215000,
      "latestQuotedUnitPrice": 210000
    }
  ],
  "approvedPricing": {
    "productPricingVersionId": "pricing-version-guid",
    "version": 3,
    "status": "Approved",
    "approvedAt": "2026-08-20T15:52:00",
    "sourceType": "Formula",
    "sourceId": "formula-guid",
    "sourceExternalId": "VU260600325",
    "sourceName": "F001",
    "priceTiers": [
      {
        "quantityRangeLabel": "< 50 kg",
        "unitPrice": 215000,
        "requiresManualPrice": false
      }
    ]
  },
  "systemCalculatedPricing": {
    "pricingStatus": "Available",
    "calculatedAt": "2026-08-23T10:30:00",
    "sourceType": "Formula",
    "sourceId": "formula-guid",
    "sourceExternalId": "VU260600325",
    "sourceName": "F001",
    "priceTiers": [
      {
        "quantityRangeLabel": "< 50 kg",
        "unitPrice": 218000,
        "requiresManualPrice": false
      }
    ]
  },
  "latestQuotedPricing": {
    "quotationId": "quotation-guid",
    "quotationExternalId": "BBG260800002",
    "quotationDate": "2026-08-18T00:00:00",
    "sentDate": "2026-08-18T16:20:00",
    "priceTiers": [
      {
        "quantityRangeLabel": "< 50 kg",
        "unitPrice": 210000,
        "requiresManualPrice": false
      }
    ]
  }
}
```

Preview có `quantity = 0`, `unitPrice = 0`, `discountPercent = 0` và `lineTotal = 0`;
FE cập nhật khi người dùng nhập số lượng/chọn tier. Tất cả tier preview có
`isSnapshot = false`. Tier hệ thống chưa tính được giá có `unitPrice = null` trong
`systemCalculatedPricing`; ở `priceTiers` tương ứng dùng `unitPrice = 0` cùng
`requiresManualPrice = true` để Sale có ô nhập. Giá `0` do người dùng chủ đích nhập vẫn hợp lệ.
Khi Sale lưu lines, backend lưu đúng tiers FE gửi thành snapshot của báo giá.

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
- `effectivePricing` chứa chi phí sản xuất hiệu lực, giá bán tiêu chuẩn, tỷ lệ lợi nhuận và rule tier do engine tính
  từ policy Published của đúng company/profile/currency.
- `displayPriceTiers` dùng tier đã lưu khi version gần nhất có `ProductPricingTiers`; nếu chưa có thì trả tier gợi ý
  từ engine/policy. `priceTiersAreStored` và `displayPriceTiers[].isStored` giúp FE phân biệt hai trường hợp.

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
- Price tiers snapshot của từng line.
- Lịch sử trạng thái.

Mỗi `lines[]` tách bốn nguồn tier độc lập. FE không phải ghép giá chuẩn, giá hệ thống và giá báo cũ vào
từng hàng snapshot:

- `priceTiers`: giá **báo lần này** của chính quotation. Đây là list duy nhất Sale được sửa và gửi trong
  `PUT /quotations/{quotationId}/lines`. Khi đã lưu, từng tier có `isSnapshot = true`. GET detail trả cả tier
  đang tắt; FE dùng `isActive = false` để nhận biết tier không được áp dụng vào tính giá hiện tại.
- `approvedPricing`: object giá President đã duyệt, gồm version, `status`, `approvedAt`, `standardSellingPrice`,
  công thức nguồn và `priceTiers`. Chưa từng duyệt thì `null`.
- `systemCalculatedPricing`: object giá realtime từ công thức/NVL/policy hiện hành, gồm `pricingStatus`,
  `calculatedAt`, `standardSellingPrice`, nguồn và `priceTiers`. Nó không ghi dữ liệu vào quotation.
- `latestQuotedPricing`: object tiers của quotation `Sent` gần nhất cho cùng customer + product + currency,
  gồm quotation, `sentDate` và `priceTiers`. Chưa từng gửi thì `null`.

`defaultPriceTierSource` giải thích nguồn dùng để dựng `priceTiers` khi line chưa có snapshot: ưu tiên
`ApprovedPricingVersion`, sau đó `SystemCalculated`. Các tier được dựng có `isSnapshot = false` và
`quotationLinePriceTierId = Guid.Empty`; chúng chỉ là dữ liệu khởi tạo để Sale nhập trước khi lưu. Tier bắt
buộc nhập tay có `unitPrice = 0` và `requiresManualPrice = true`. FE gửi lại list đó qua API create/replace
lines để lưu thành snapshot; GET detail không tự ghi database.

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
- PDF chỉ dùng snapshot sản phẩm, price tiers, commission và totals đang lưu trên Quotation;
  không tải lại Formula hoặc giá NVL realtime.
- Giá tier in cho khách là `unitPrice + commissionAmount`; API vẫn trả riêng hai field để FE chỉnh và hiển thị.
  Khi tổng hai field bằng `0`, PDF để trống ô giá để không thể hiện nhầm là giá bán 0 đồng.
- Báo giá `Draft` có watermark nền `BẢN NHÁP / DRAFT` và dòng nhắc tài liệu nội bộ; khi trạng thái là `Sent`
  thì hai dấu hiệu này tự biến mất. FE không truyền cờ watermark.
- Label cột price tiers lấy từ snapshot `Customer.QuotationLinePriceTiers.QuantityRangeLabel`;
  khi in PDF sẽ ẩn hậu tố nội bộ như `liên hệ BGĐ` hoặc `liên hệ Ban giám đốc` nếu label có kèm theo.
- Header của bảng giá dùng chữ đậm; các giá trị trong thân bảng dùng chữ thường để dễ đọc.
- Mỗi dòng sản phẩm trên PDF có cột `Ghi chú/Note`, lấy từ snapshot
  `Customer.QuotationLines.Note`; để trống thì in `-`. Cột này không lấy từ
  `Quotation.Note` (ghi chú chung của cả báo giá).
- Tên/địa chỉ khách hàng, người liên hệ, nhân viên phụ trách và thông tin công ty hiện
  được đọc từ dữ liệu liên kết tại thời điểm xuất PDF. Muốn chứng từ đã gửi bất biến
  hoàn toàn thì cần lưu thêm snapshot header hoặc lưu chính file PDF khi `mark-sent`.
- Header/footer ISO dùng cấu hình `Pdf:Quotation` và tự fallback sang text nếu thiếu ảnh.
- QuestPDF license lấy từ `Pdf:QuestPdfLicense`; chỉ cấu hình `Community` khi doanh nghiệp
  đáp ứng đúng điều kiện license.
- Endpoint tạo file on-demand và chưa lưu file PDF vào storage.
- Sau bảng giá, PDF chỉ hiển thị `Quotation.Note` do FE gửi; nếu note trống thì không hiển thị khối ghi chú.
  Khối `Các điều khoản khác` ưu tiên `Quotation.Terms` active theo `sortOrder`, ghép label/value Việt-Anh.
  Báo giá cũ chưa có bất kỳ term snapshot nào tiếp tục dùng mẫu legacy từ `DeliveryTerms`, `PaymentTerms`,
  `ValidUntil` và các giá trị mặc định trong renderer.

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
- `Note` là ghi chú nội bộ tùy chọn cho từng version giá chuẩn. Chuỗi rỗng chỉ được lưu
  là `null`; chỉ President/Developer nhận lại ghi chú đã lưu, Sale nhận `null`.
- Nguồn ghi mới dùng đúng một cặp `sourceType + sourceId`: `Formula` hoặc `ManufacturingFormula`.
  `SourceFormulaId` và `SourceManufacturingFormulaId` là hai FK tương ứng. Các field nguồn cũ vẫn được giữ
  để đọc dữ liệu lịch sử, nhưng contract ghi mới không nhận chúng.
- Entity/config hiện yêu cầu cột `SourceManufacturingFormulaId`; thay đổi này không kèm migration hoặc script database.
- `ProductPricingTier` lưu các bậc số lượng và đơn giá thuộc một phiên bản giá.
- Khi Formula có một dòng thành phần kiểu `Product`, BE lấy giá vốn thành phẩm theo thứ tự:
  `ProductPricingVersion` đang `Approved` mới nhất của đúng `Company + Currency` và có
  `StandardSellingPrice`; nếu chưa có, BE lấy tổng `Quantity × giá NVL` của `Formula.IsSelect = true`
  đang áp dụng cho TP đó; chỉ khi Formula trống/thiếu giá/vòng lặp mới fallback sang
  `MerchandiseOrderDetails.UnitPriceAgreed` gần nhất. Nguồn đơn nội bộ là legacy transaction fallback.
  Formula lồng nhau được resolve theo cùng thứ tự ưu tiên; các truy vấn đều batch theo danh sách product id,
  không query từng dòng Formula.
- `QuotationLine.ProductPricingVersionId` chỉ dùng để truy vết nguồn giá. Giá thực sự gửi khách
  vẫn phải được sao chép vào `QuotationLinePriceTiers` để báo giá cũ không đổi khi bảng giá nội bộ thay đổi.
- `FormulaPricingPolicyTier`, `ProductPricingTier`, `QuotationLine` và `QuotationLinePriceTier` có
  `IsActive`; PUT tạo/cập nhật có thể gửi `false`. Các GET, PDF, pricing workspace/reference và tổng tiền
  chỉ sử dụng bản ghi active, nên bản ghi inactive không xuất hiện trong response.

API quản lý bảng giá:

- `GET /api/v1/crm/quotations/product-pricing-workbench?view=NeedsPricing&currency=VND`: danh sách
  Product-centric cho President/Developer. Bảng giá chuẩn được quản lý bằng VND; không truyền `currency` cũng mặc định
  là VND. Mỗi `Product + Currency` chỉ có một dòng. Mọi thứ tự sắp xếp đều
  ưu tiên sản phẩm thuộc báo giá đang yêu cầu định giá trước, tiếp theo là giá chuẩn đã hết hạn, rồi các giá chuẩn
  có ngày hết hạn gần nhất; `sortBy` của FE chỉ sắp xếp trong từng nhóm ưu tiên này. `view` nhận `NeedsPricing`, `Draft`, `Approved` hoặc `All`;
  mặc định là `All`.
  Tiền tệ của quotation request không được dùng để lọc hàng đợi: mã BBG VND và USD đều có thể đưa Product vào
  workbench, nhưng bản ghi giá chuẩn hiển thị và quản lý tại đây vẫn là VND.
  `President` và `Developer` nhận đầy đủ dữ liệu quản lý giá. `SaleUser` được phép đọc nhưng response bị giới hạn:
  danh sách giữ nguồn công thức cùng `sourceStatus`/`sourceIsEligible`, giá bán tiêu chuẩn, trạng thái bảng giá và số báo giá đang chờ; các field chi phí,
  margin, version id và ngày nội bộ trả `null`/giá trị mặc định. `canOpenPricingDetail = true` cho phép FE giữ thao
  tác mở drawer; `canManagePricing = false` chỉ khóa các thao tác ghi, duyệt và đổi nguồn.
  Có thể gửi `sortBy=createdDate&sortDirection=asc|desc` để đổi thứ tự này.
  `All` chỉ gồm sản phẩm có Formula/MFG Formula đủ điều kiện, đã có `ProductPricingVersion`, hoặc đang nằm trong
  báo giá đã gửi yêu cầu định giá. Khi không truyền `keyword`, workbench ẩn sản phẩm có Sample Request của khách nội bộ
  `KH_VIETAUS`; khi người dùng chủ động tìm bằng `keyword`, các sản phẩm này được phép xuất hiện. Khi `keyword` bắt đầu bằng `BBG`, API cũng tìm trực tiếp các dòng active của
  báo giá active bất kể tiền tệ mà người gọi có quyền xem, kể cả khi báo giá đã chuyển khỏi `PendingApproval`.
  Product cũ không có nguồn, không có lịch sử giá và không có yêu cầu sẽ bị ẩn;
  sản phẩm đang được yêu cầu nhưng chưa có Formula vẫn được giữ để cảnh báo President.

  Với version `Approved` được tạo từ policy có `priceValidityDays`, response summary trả thêm
  `priceConfirmedAt` (mốc duyệt version), `priceExpiresAt` (= `priceConfirmedAt + priceValidityDays`),
  `remainingValidityDays`, `overdueDays` và `isPriceExpired`. Các field này chỉ áp dụng cho giá chuẩn đã duyệt;
  `null` nghĩa là chưa có giá chuẩn hoặc policy không đặt thời hạn. Ngày hết hạn vẫn còn hiệu lực có
  `remainingValidityDays = 0`; chỉ khi qua ngày đó thì `isPriceExpired = true` và `overdueDays` mới có giá trị.
  Sale không nhận các mốc ngày/version nội bộ này theo visibility rule hiện có.
- `GET /api/v1/crm/quotations/products/{productId}/pricing-workbench?currency=VND`: dữ liệu drawer gồm nguồn
  đang dùng, NVL và giá mới nhất, chênh lệch với snapshot, Draft/Approved, tiers hiển thị, lịch sử version và
  các báo giá đang chờ. Có thể truyền thêm `sourceType=Formula&sourceId={formulaId}` để preview nguồn do FE
  chọn từ Formula lookup trước khi tạo Draft. Preview vẫn trả nguồn thuộc đúng Product/công ty khi nguồn chưa
  đủ điều kiện, với `selectedSource.isEligible = false`, để màn hình không mất toàn bộ dữ liệu chi tiết. Các
  command tạo/cập nhật phiên bản giá luôn validate lại eligibility và vẫn từ chối nguồn không đủ điều kiện.
  Với Formula legacy chưa có `Formula.CompanyId`, company scope được xác định qua Product liên kết; dữ liệu của
  Product thuộc công ty khác không được trả về.

  Với `SaleUser`, drawer trả giá bán tiêu chuẩn, `selectedSource` rút gọn gồm `sourceType`, `sourceId`,
  `externalId`, `name` và danh sách tiers chỉ đọc. Tier của Sale chỉ có khoảng khối lượng, đơn giá, cờ cần nhập
  thủ công/cờ đã lưu và thứ tự; `marginVsMaterialPercent`/`marginVsCostPercent` luôn `null`. Chi phí, margin tổng,
  NVL, version Draft/Approved, lịch sử và báo giá liên quan vẫn bị trả `null` hoặc danh sách rỗng. Sale không được
  truyền `sourceType/sourceId` để preview nguồn khác và không có quyền mutation bảng giá.

  Drawer luôn trả trực tiếp `manufacturingCost`, `standardSellingPrice` và `profitMarginRate` là ba giá trị
  hiệu lực giống `summary`. Cả ba cùng lấy từ version `Draft`, nếu không có thì version `Approved`; chỉ khi
  chưa có version DB nào mới lấy từ nguồn realtime và luật giá. Vì vậy khi chỉ có `approvedPricing`, ba field
  ngoài cùng phải trùng các giá trị tương ứng của object đó. `selectedSource.pricing` là preview realtime để
  so sánh, không được ghi đè ba giá trị đã persist. FE không được bind input từ `draftPricing`/`approvedPricing`
  vì hai object đó hợp lệ khi null.

  `selectedSource.pricingProfile` luôn cho biết nguồn đang dùng rule `Powder` hay `Compound`. Khi một hoặc
  nhiều NVL chưa có giá hoặc có giá bằng 0, backend dùng 0 cho các dòng đó và vẫn trả `selectedSource.pricing`,
  chi phí sản xuất, giá bán tiêu chuẩn cùng tiers theo policy. `isCurrentMaterialCostComplete = false` và
  `missingMaterialPriceCount > 0` là cảnh báo để FE hiển thị, không chặn lưu hoặc duyệt bảng giá.
  Mỗi phần tử `selectedSource.materials[]` trả thêm `categoryId` snapshot từ dòng Formula/MFG Formula để FE
  có thể phân nhóm hoặc mở lookup theo đúng category đã dùng trong công thức.
- `GET /api/v1/crm/quotations/product-pricing-versions?productId={id}&currency=VND`: xem lịch sử version.
- `GET /api/v1/crm/quotations/products/{productId}/pricing-sources?currency=VND`: lấy Formula/MFG Formula đủ điều kiện;
  chỉ President/Developer được gọi. Khi backend cần tự chọn một nguồn fallback (chưa có version giá hoặc tính giá
  realtime), mọi nguồn đã đủ điều kiện được xếp theo `UpdatedDate` mới nhất; không ưu tiên Formula `Approved`
  hơn Formula `SampleSent` hoặc `Completed` mới hơn.
- `POST /api/v1/crm/quotations/product-pricing-versions`: nguồn phải hợp lệ và có policy `Published` đúng
  company/profile/currency. Backend tự tải material cost realtime, bỏ qua `materialCostSnapshot` và
  `calculatedAt` từ request, rồi tạo toàn bộ tier theo policy. Request có thể gửi `note` để lưu ghi chú
  nội bộ. Màn President gửi `approveImmediately=true` để tạo và duyệt version trong cùng một lần ghi.
- `PUT /api/v1/crm/quotations/product-pricing-versions/{id}`: sửa giá và tiers của một version `Draft`;
  không được đổi nguồn hoặc policy. Có thể cập nhật `note`; không gửi hoặc gửi chuỗi rỗng thì ghi chú sẽ là
  `null`. Backend luôn dùng đúng policy FK của Draft, không tự chuyển sang policy mới.
- `POST /api/v1/crm/quotations/product-pricing-versions/{id}/approve`: duyệt version và chuyển version `Approved`
  trước đó của cùng `Product + Currency` sang `Superseded`. Backend tải lại material cost realtime và tính lần
  cuối bằng policy FK trước khi lưu snapshot/tiers và phát notification.

Khi POST/PUT/approve không thể dùng nguồn giá đã chọn, API trả message tiếng Việt theo nguyên nhân thực tế trong
phạm vi sản phẩm và công ty hiện tại: công thức hoặc sản phẩm đã ngừng hoạt động, công thức chưa đạt trạng thái
được phép, công thức sản xuất chưa đến ngày hiệu lực/đã hết hiệu lực, hoặc chưa có phiên bản được phát hành.
Nếu không tìm thấy nguồn trong phạm vi hiện tại, API chỉ trả lỗi không tìm thấy nguồn phù hợp, không tiết lộ dữ liệu
của công ty khác.

Draft có `FormulaPricingPolicyId = null`, policy đã `Superseded`, inactive, chưa hiệu lực hoặc không còn khớp
company/profile/currency là read-only. PUT/approve trả HTTP `409 Conflict` với yêu cầu tạo/rebase thành version
mới. Publish policy mới không sửa bất kỳ snapshot hoặc version `Approved` cũ nào.

Chỉ `President` và `Developer` được xem lịch sử đầy đủ, tạo, sửa hoặc duyệt bảng giá. Endpoint
`GET /products/{productId}/pricing?currency=VND` cho Sale đọc các tier giá bán và metadata nguồn/duyệt,
nhưng contract này không trả material cost, manufacturing cost hoặc margin. Field
`canApplyToQuotation` true khi bộ `priceTiers` mặc định có ít nhất một
tier và tất cả tier đều có `unitPrice`; nguồn có thể là `ApprovedPricingVersion` hoặc
`SystemCalculated`. Giá `0` là hợp lệ, chỉ `null` mới được xem là thiếu giá.

Khi sản phẩm chưa có Formula đủ điều kiện hoặc chưa có Formula, endpoint vẫn trả
`pricingAvailability`, `warningCode`, `canUseManualCustomerPrice` và
`manualPriceTierTemplates`. Các template luôn lấy từ `FormulaPricingPolicy` đang publish;
FE không được hard-code hay tự thay đổi min/max/inclusive/sortOrder. Nếu được phép nhập giá
khẩn, `priceTiers` được khởi tạo từ các template với giá `0`, FE gửi
  `priceMode = 50` (`ManualAuthorized`). Note dòng là tùy chọn; nếu có, FE nên dùng để ghi căn cứ/người cho phép.
Giá này chỉ được snapshot vào báo giá, không tạo `ProductPricingVersion` và không gắn giả
vào Formula. Backend từ chối nếu các khoảng gửi lên không khớp policy.

Ví dụ tạo Draft mới từ Formula:

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "sourceType": "Formula",
  "sourceId": "00000000-0000-0000-0000-000000000000",
  "currency": "VND",
  "approveImmediately": true,
  "manufacturingCost": 20000,
  "standardSellingPrice": 140000,
  "profitMarginRate": 27.2727,
  "changedField": "StandardSellingPrice",
  "priceTiers": [
    {
      "quantityRangeLabel": "> 5 tấn",
      "minQuantity": 5000,
      "maxQuantity": null,
      "minInclusive": false,
      "maxInclusive": true,
      "unitPrice": 135000,
      "sortOrder": 5
    }
  ]
}
```

`priceTiers` chỉ cần chứa tier President chỉnh hoặc tier policy yêu cầu nhập thủ công. Label, range,
inclusive flags và sort order phải đúng policy; backend tự bổ sung các tier hệ thống còn lại. Gửi tier ngoài
policy hoặc sửa khoảng khối lượng bị từ chối. `priceTiers = []` chỉ hợp lệ khi policy không có tier manual.

Response ghi thành công trả snapshot đã persist, ví dụ rút gọn:

```json
{
  "productPricingVersionId": "00000000-0000-0000-0000-000000000000",
  "formulaPricingPolicyId": "00000000-0000-0000-0000-000000000000",
  "formulaPricingPolicyVersion": 3,
  "hasManualTierAdjustment": true,
  "sourceFormulaId": "00000000-0000-0000-0000-000000000000",
  "formulaExternalIdSnapshot": "VU260800001",
  "materialCostSnapshot": 90000,
  "manufacturingCost": 20000,
  "standardSellingPrice": 140000,
  "profitMarginRate": 27.2727,
  "calculatedAt": "2026-08-21T10:00:00+07:00",
  "status": "Approved",
  "priceTiers": [
    {
      "quantityRangeLabel": "> 5 tấn",
      "minQuantity": 5000,
      "maxQuantity": null,
      "unitPrice": 135000,
      "sortOrder": 5
    }
  ]
}
```

`materialCostSnapshot` và `calculatedAt` trong response là dữ liệu server vừa tính/lưu, không phải giá request
được echo lại. Version được approve luôn trả toàn bộ tier policy đã persist.

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

`sourceIsEligible` chỉ nói Formula/MFG Formula còn hợp lệ để làm nguồn kỹ thuật. Nó **không** có nghĩa là đã đủ
giá NVL hoặc giá đã sẵn sàng gửi khách. Workbench bổ sung `pricingHealthStatus` để FE hiển thị tình trạng định giá
cho người dùng thay vì suy luận từ nhiều field rời rạc:

| `pricingHealthStatus` | Ý nghĩa | `requiresPricingAction` |
|---|---|---|
| `Ready` | Có giá NVL đầy đủ, giá chuẩn còn trong hạn rà soát và không có chênh lệch chi phí vượt ngưỡng. | `false` |
| `AwaitingApproval` | Hệ thống tính được giá nhưng chưa có ProductPricingVersion `Approved`. | `true` |
| `MissingMaterialPrice` | Có ít nhất một NVL chưa có giá hoặc có giá bằng 0. Giá vẫn được tính/lưu/duyệt với dòng đó bằng 0; status chỉ dùng để cảnh báo và vẫn trả hạn rà soát nếu đã có giá Approved. | `false` |
| `MaterialCostChanged` | Chi phí NVL realtime lệch snapshot đã lưu vượt ngưỡng cấu hình. | `true` |
| `CostingStale` | Snapshot chi phí/giá đã lưu quá số ngày cho phép. | `true` |
| `RepricingRequired` | Giá chuẩn đã duyệt quá hạn rà soát. | `true` |
| `NoEligibleSource`, `SourceNoLongerEligible`, `PricingPolicyMissing` | Không có nguồn kỹ thuật hợp lệ, nguồn cũ không còn hợp lệ, hoặc chưa có policy giá áp dụng. | `true` |

Thứ tự ưu tiên khi backend resolve status là: không có nguồn/policy, thiếu giá NVL, chi phí biến động vượt ngưỡng,
giá chuẩn đến hạn rà soát, snapshot chi phí cũ, chờ duyệt, rồi mới `Ready`. Vì vậy Formula có thể trả
`sourceIsEligible = true` đồng thời `pricingHealthStatus = MissingMaterialPrice`; hai field không mâu thuẫn.

`pricingReviewDueDate` là ngày cần rà soát lại giá chuẩn President đã duyệt. FE không tự tính ngày hoặc ngưỡng.
Ngưỡng dùng config `Features:Quotations` (có thể thay đổi theo môi trường, giá trị `0` tắt rule tương ứng):

```json
{
  "Features": {
    "Quotations": {
      "CostingStaleAfterDays": 14,
      "ApprovedPricingReviewAfterDays": 30,
      "MaterialCostChangeThresholdPercent": 5
    }
  }
}
```

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
    "pricingHealthStatus": "AwaitingApproval",
    "requiresPricingAction": true,
    "pricingReviewDueDate": null,
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
| `pricingHealthStatus`/`requiresPricingAction`/`pricingReviewDueDate` | Tình trạng sẵn sàng dùng giá theo rule backend. Dùng để hiện cảnh báo “Thiếu giá NVL”, “Giá thành đã thay đổi”, “Giá thành cũ” hoặc “Cần báo giá lại”; không thay thế `sourceIsEligible`. |
| `draftPricingVersionId`/`approvedPricingVersionId` | Id record đã persist. Cả hai `null` trong ví dụ xác nhận chưa có version DB. |
| `source*` | Nguồn kỹ thuật được chọn để tính giá. Ví dụ dùng Formula `VU260700083 - F001`; `sourceIsEligible` cho biết còn đủ điều kiện định giá, `sourceIsCustomerSelected` là cờ công thức khách hàng chọn. |
| `currentMaterialCost` | Tổng realtime `SUM(material.quantity * latestUnitPrice)`. Không đọc từ snapshot cũ của Formula/ProductPricingVersion. |
| `isCurrentMaterialCostComplete`/`missingMaterialPriceCount` | Cảnh báo số NVL chưa có giá hoặc có giá bằng 0. Các dòng này được tính với đơn giá 0; cost/pricing/tier vẫn được trả và cảnh báo không chặn lưu/duyệt. |
| `storedMaterialCostSnapshot` | Chi phí NVL đã lưu trong Draft/Approved gần nhất. `null` nghĩa là chưa có version để so sánh. |
| `materialCostDifference*` | Chỉ có khi đồng thời có current cost và stored snapshot; dùng so sánh biến động giá NVL, không phải lợi nhuận. |
| `manufacturingCost` | Chi phí sản xuất đã persist: ưu tiên version `Draft`, sau đó version `Approved`; chỉ khi chưa có version nào mới fallback sang realtime. `usedDefaultManufacturingCost` cho biết preview realtime đang dùng mức mặc định của policy hay override. |
| `standardSellingPrice` | Giá bán tiêu chuẩn đã persist: ưu tiên version `Draft`, sau đó version `Approved`; chỉ khi chưa có version nào mới fallback sang giá realtime. |
| `realtimeStandardSellingPrice` | Giá bán do engine tính lại từ NVL, manufacturing cost và pricing policy hiện tại. Chỉ President/Developer nhận field này. |
| `standardSellingPriceDifference` / `standardSellingPriceDifferencePercent` | Realtime trừ giá chuẩn đã lưu và phần trăm trên giá đã lưu. Hai field là `null` nếu chưa có giá lưu hoặc giá lưu `<= 0`. |
| `hasRealtimePriceComparison` | `true` khi đồng thời có giá chuẩn đã lưu lớn hơn 0 và giá realtime để FE hiển thị chênh lệch. Sale luôn nhận `false`. |
| `profitMarginRate` | Tỷ lệ lợi nhuận đã persist theo cùng version với `manufacturingCost` và `standardSellingPrice`: `Draft` trước, `Approved` sau, rồi mới fallback sang realtime. Giá trị realtime (nếu có) nằm trong `selectedSource.pricing.profitMarginRate`. |
| Ba field giá ở top-level | Là giá canonical để FE bind vào ba input trong drawer; chúng phải giống `summary` và phải cùng đến từ một version `Draft` hoặc `Approved` khi version đó tồn tại. Không bind từ `draftPricing`/`approvedPricing` vì hai object có thể `null`. |
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

- Material cost, cost base, standard selling price và tier hệ thống được làm tròn theo
  `roundingRule + roundingIncrement` của policy đã gắn.
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
Tier hệ thống được dựng từ policy; tier thủ công đã lưu được giữ nguyên. FE không phải và không được tự tạo
template tier ngoài policy.

## 7. Quy tắc giá theo khối lượng của dòng sản phẩm


`PriceMode` không còn quyết định cách tính, kiểm tra gửi hay cách in báo giá. Field này vẫn được lưu/trả lại để
phục vụ mục đích khác ở tương lai. Create/replace bắt buộc FE gửi `priceTiers` với ít nhất một tier active;
backend lưu đúng các tier Sale nhập sau khi kiểm tra khoảng khối lượng. Giá chuẩn và tổng tiền của line độc lập
với tier gửi khách.

Refresh price nhận `quotationLineId + productPricingVersionId`, sau đó cũng sao chép snapshot từ backend.
Đổi currency bị từ chối khi báo giá đã có snapshot để tránh trộn hai loại tiền tệ.

`mark-sent` nhận báo giá `Draft`, `PendingApproval` hoặc `Approved`. Backend không bắt buộc
`ProductPricingVersionId`, vì giá chuẩn nội bộ có thể vẫn đang chờ; nhưng mọi line vẫn phải có snapshot tier active
với giá không âm để nội dung thực gửi khách xác định được. PDF cũng từ chối export nếu line active thiếu tier.

Mỗi line có từ 1 đến 100 price tiers. Backend vẫn yêu cầu đúng một tier khớp với `quantity` để bảo đảm bảng giá
không bị hở hoặc mâu thuẫn, nhưng tier khớp không được snapshot vào `QuotationLine.UnitPrice` và không dùng tính
`LineTotal`.

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
  toàn bộ price tiers, ghi chú line, ngày gửi và tiền tệ từ snapshot báo giá.
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
gọi `refresh-prices`. Nếu báo giá đã có InternalMail conversation active theo khóa `CompanyId + RelatedType=Quotation
+ RelatedId=quotationId`, notification được gắn `conversationId` của thread đó để Notification Hub gom chung. Nếu
chưa có thread, backend không tạo conversation hoặc message chỉ để phát notification; notification vẫn hiển thị độc lập.

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

## 14. Contract đọc giá thống nhất

Các API Workbench list/drawer, Quotation Pricing Workspace, Product Pricing Options,
`GET /products/{productId}/pricing`, pricing comparison và PLM Formula detail dùng cùng
`FormulaPricingEngine` và cùng canonical source result. Mapper chỉ map response, không gọi calculator.

Mọi request realtime phải truyền currency. Resolver lấy policy `Published`, active, đúng
`companyId + Product.FormulaPricingProfile + currency` và đã tới `EffectiveFrom`; không suy profile từ
mã sản phẩm/`Additive`, không fallback `VND`, policy hoặc tier hard-code. Cùng product/source/currency sẽ trả
cùng standard selling price và suggested tiers ở list, drawer, workspace và PLM.

Khi không resolve được policy, source trả `pricing = null` và `pricingStatus = PricingPolicyMissing`.
Khi thiếu giá material, source trả `pricing = null` và `pricingStatus = MaterialPriceMissing`.
Policy id/version được trả cùng kết quả để client biết chính xác cấu hình đã dùng.

Visibility theo role:

- Sale chỉ thấy product đã có Sample Request hoặc Quotation active thuộc customer trong `CustomerVisibilityService`
  của họ (khách được phân công/claim, hoặc thuộc group do họ dẫn). Product không liên quan customer trong scope
  không xuất hiện trong Product Pricing Workbench. Sale nhận metadata nguồn, standard selling price, tiers và
  `relatedCustomers`. Mỗi related customer chỉ gồm
  mã/tên khách hàng, số chứng từ liên quan và ngày liên quan gần nhất; `healthSummary` luôn là `null`.
- President/Developer thấy toàn bộ product active cùng company và nhận thêm `healthSummary`, material completeness,
  cost, margin, material/supplier details và history.
- Quotation detail giữ nguyên snapshot nếu đã có; line chưa có snapshot được bổ sung tier gợi ý realtime chỉ để
  hiển thị/nhập liệu với `isSnapshot = false`. PDF tiếp tục chỉ đọc snapshot đã lưu.

## 15. Snapshot bất biến và loại bỏ pricing legacy

- Create/replace/refresh chỉ áp dụng `ProductPricingVersion` active, `Approved`, đúng
  company/product/currency và luôn clone toàn bộ tiers vào `QuotationLinePriceTiers`.
- Quotation detail không cập nhật lại snapshot cũ. Chỉ khi line chưa có tier, response mới kèm tier hệ thống
  `isSnapshot = false` để Sale nhập liệu; thao tác GET không ghi database. Nội dung `mark-sent` và PDF chỉ đọc snapshot.
- Publish policy hoặc approve ProductPricingVersion mới không cập nhật Quotation đã có
  snapshot. Muốn đổi giá phải refresh rõ ràng khi Quotation còn `Draft`.
- `FormulaPricingEngine` là nơi duy nhất tính giá; calculator thuần chỉ nhận policy definition
  lấy từ DB. Workbench, workspace, resolver và options không giữ luật tính riêng.
- Currency là input bắt buộc của policy, pricing version và API realtime; backend không tự
  chọn một currency mặc định cho flow pricing.
- `PATCH /api/v1/plm/formulas/{formulaId}/pricing` đã deprecated và trả `410 Gone` với mã
  `PatchFormulaPricingDeprecated`. Flow ghi chính là ProductPricingVersion.
- `Formula.TotalPrice`, `Formula.ProductionPrice`, `Formula.PresidentPrice` và các field legacy
  vẫn được giữ để đọc dữ liệu lịch sử; không bị xóa và không còn là nguồn/fallback cho giá mới.
- Repo không thêm migration trong phase này.

### Tổ chức source pricing

Pricing Policy tuân theo một use case mỗi folder và một type chính mỗi file:

```text
Commands/
  CreateFormulaPricingPolicy/
    CreateFormulaPricingPolicyCommand.cs
    CreateFormulaPricingPolicyCommandHandler.cs
  UpdateFormulaPricingPolicy/
    UpdateFormulaPricingPolicyCommand.cs
    UpdateFormulaPricingPolicyCommandHandler.cs
  PublishFormulaPricingPolicy/
    PublishFormulaPricingPolicyCommand.cs
    PublishFormulaPricingPolicyCommandHandler.cs
Queries/
  GetFormulaPricingPolicies/
    GetFormulaPricingPoliciesQuery.cs
    GetFormulaPricingPoliciesQueryHandler.cs
  PreviewFormulaPricingPolicy/
    PreviewFormulaPricingPolicyQuery.cs
    PreviewFormulaPricingPolicyQueryHandler.cs
```

Các request/response của Pricing Policy và ProductPricingVersion cũng được tách thành file
theo đúng tên type trong `Dtos/`; không gom command, handler hoặc nhiều DTO pricing vào một file chung.

## 16. Giới hạn hiện tại

Chưa có:

- Submit/approve.
- Accept/reject.
- Cancel/restore.
- Revise/version mới.
- API tự gửi email.
- Tự lưu và gửi bản PDF cố định khi `mark-sent`.
- Endpoint xóa báo giá.

Repo hiện chưa chứa migration cho các cột/bảng báo giá mới. Database chạy API phải được đồng bộ schema tương ứng trước khi sử dụng chức năng price tiers.

## 17. Cảnh báo giá chuẩn hết hiệu lực

`QuotationPricingExpiryReminderWorker` quét mỗi giờ các quotation `Approved` chưa gửi có line đang tham chiếu một
`ProductPricingVersion` `Approved` quá `Features:Quotations:ApprovedPricingReviewAfterDays`. Mốc hết hạn dùng
`ApprovedAt`, fallback `UpdatedDate`, rồi `CreatedDate`, đúng với `pricingReviewDueDate` của Product Pricing
Workbench. Đặt số ngày cấu hình bằng `0` để tắt hoàn toàn rule này.

Khi đến hạn, backend giữ snapshot cũ, chuyển `Approved -> PendingApproval`, ghi status history nhưng không khóa
`mark-sent`; Sale vẫn có thể gửi tier giá khách đã chuẩn bị. Backend đồng thời tạo action message trong đúng
`InternalConversation` có `RelatedType = Quotation` và publish
`TopicNotifications.QuotationPricingExpired = 50` qua `INotificationService`. Topic trả về
`topicCode = crm.quotation.pricing.expired`, `categoryCode = quotation`, `eventGroupCode = pricing-alert`,
severity `Warning`; vì đi qua notification service nên inbox state, SignalR và Web Push outbox dùng luồng chuẩn.

Payload có `contentType = QuotationPricingExpired`, `relatedId`, `relatedExternalId`, `conversationId`, `messageId`,
`productId`, `productCode`, `productPricingVersionId`, `pricingReviewDueDate`, `quotationStatus = PendingApproval` và
`action.code = Quotation.OpenPricingWorkspace`. FE ánh xạ action parameters sang pricing workspace của client;
backend không hard-code route UI. Người nhận là sale phụ trách báo giá, leader sale group, President và Developer
active trong cùng company.

Mỗi cặp `QuotationId + ProductPricingVersionId` chỉ gửi một lần. Dấu chống lặp nằm trong `PayloadJson` JSONB của
message/notification đã có, không thêm bảng/cột/migration. Nếu message lưu thành công nhưng publish notification
bị lỗi, worker lần sau chỉ publish notification còn thiếu thay vì tạo thêm message.

Cùng worker này cũng reconcile tối đa 100 báo giá `PendingApproval` mỗi lượt. Khi President/Developer duyệt version
mới, handler vẫn reconcile ngay; worker chỉ là lớp phục hồi eventual consistency.
## 16.1. Cấu trúc mới trong `Services/`

Phần code nghiệp vụ trong `HRM.Application/Features/CRM/Quotations/Services` đã được gom theo nhóm để dễ bảo trì:

- `Builders/`: tạo/đóng gói đối tượng trong flow báo giá.
- `Mappers/`: chuyển đổi, ánh xạ model nội bộ.
- `Queries/`: đọc dữ liệu, đọc nguồn giá, chọn source.
- `Resolvers/`: xác định chiến lược/nguồn giá áp dụng.
- `Rules/`: quy tắc nghiệp vụ và điều kiện kiểm tra thuần.
- `Services/`: orchestration hoặc use case có side effect nhẹ.
- `Validation/`: validator nghiệp vụ.
- `Models/`: DTO/read-model nội bộ.

Mỗi file đều giữ nguyên namespace `HRM.Application.Features.CRM.Quotations.Services` để tránh đổi hành vi cross file.
