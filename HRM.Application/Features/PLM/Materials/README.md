# Tra cá»©u NVL vÃ  Product cho cÃ´ng thá»©c

```http
GET /api/v1/plm/formulas/item-lookup
```

Endpoint tráº£ danh sÃ¡ch phÃ¢n trang dÃ¹ng khi thÃªm dÃ²ng vÃ o cÃ´ng thá»©c. Query há»— trá»£ `pageNumber`, `pageSize`,
`keyword`, `itemType` (`Material`, `Product`, `MaterialFailure`, `ProductFailure`) vÃ  `categoryId`.
`MaterialFailure` Ä‘Æ°á»£c tra nhÆ° `Material`, `ProductFailure` Ä‘Æ°á»£c tra nhÆ° `Product`.

Khi tra Product, `keyword` hỗ trợ tên, mã màu/mã Product, `SampleRequest.ExternalId` active và
`Formula.ExternalId` active cùng company.

NVL pháº£i active vÃ  thuá»™c `CurrentUser.CompanyId`. Product pháº£i active, thuá»™c cÃ¹ng cÃ´ng ty, cÃ³ tÃªn/mÃ£ mÃ u
vÃ  cÃ³ Ã­t nháº¥t má»™t Sample Request active á»Ÿ tráº¡ng thÃ¡i `SampleSent` hoáº·c `Completed`. MÃ£ `externalId` cá»§a
Product láº¥y tá»« Sample Request há»£p lá»‡ má»›i nháº¥t; `categoryCode` cá»§a Product tráº£ `KH` Ä‘á»ƒ giá»¯ contract nghiá»‡p vá»¥ cÅ©.

Má»—i item tráº£ `itemId`, `itemType`, mÃ£, tÃªn, category, Ä‘Æ¡n vá»‹/quy cÃ¡ch vÃ  `price`.
GiÃ¡ Ä‘Æ°á»£c táº£i theo batch qua `IMaterialPriceQueryService`, cÃ¹ng nguá»“n vá»›i GET chi tiáº¿t Formula:

- NVL: chá»n giÃ¡ má»›i nháº¥t giá»¯a Purchase Order vÃ  Material - Supplier.
- Product: ưu tiên `ProductPricingVersion.StandardSellingPrice` active/Approved còn trong hạn rà soát.
  Hạn được tính từ `ApprovedAt`, fallback `UpdatedDate`, rồi `CreatedDate`, cộng
  `Features:Quotations:ApprovedPricingReviewAfterDays` (mặc định 30 ngày). Khi đã quá hạn hoặc chưa có giá
  Approved hợp lệ, backend tính lại từ Formula đang chọn/SampleSent và áp luật nội bộ (nghiền, pha loãng,
  category CMB/CMP); chỉ khi không tính được mới fallback giá Merchandise Order mới nhất.
- KhÃ´ng tÃ¬m tháº¥y giÃ¡: `unitPrice = 0`, `source = Unknown`, `latestPriceDate = null`.

Endpoint yÃªu cáº§u policy `PLM.FormulaPricing.Update` vÃ  khÃ´ng nháº­n `companyId` tá»« FE Ä‘á»ƒ trÃ¡nh tra cá»©u chÃ©o cÃ´ng ty.

Pháº§n query vÃ  DTO Ä‘Æ°á»£c tá»• chá»©c trong `Features/PLM/Materials`; controller Formula chá»‰ expose route phá»¥c vá»¥
mÃ n hÃ¬nh lÃªn cÃ´ng thá»©c.

## Xem nhanh NVL vÃ  tá»‡p Ä‘Ã­nh kÃ¨m

### Láº¥y thÃ´ng tin xem nhanh

```http
GET /api/v1/plm/materials/{materialId}/preview
```

API tráº£ thÃ´ng tin NVL vÃ  metadata tá»‡p Ä‘Ã­nh kÃ¨m, khÃ´ng Ä‘á»c ná»™i dung file vÃ  khÃ´ng tráº£ base64. Contract chÃ­nh:

```json
{
  "materialId": "00000000-0000-0000-0000-000000000000",
  "externalId": "NVL-PU-001",
  "customCode": "PU-001",
  "name": "Háº¡t nhá»±a PU",
  "categoryName": "Polyurethane",
  "purchaseAvailability": {
    "status": "Unavailable",
    "isPurchaseAvailable": false,
    "reason": "Nhà cung cấp ngừng sản xuất",
    "effectiveFrom": "2026-09-15T08:00:00",
    "expectedAvailableDate": null
  },
  "lastPurchase": {
    "purchaseOrderId": "00000000-0000-0000-0000-000000000000",
    "purchaseOrderCode": "PO26070001",
    "supplierName": "CÃ´ng ty ABC",
    "unitPrice": 50000,
    "quantity": 1000,
    "purchaseDate": "2026-07-20T09:00:00"
  },
  "totalOnHandKg": 1250.5,
  "attachmentCount": 1,
  "attachments": [
    {
      "attachmentId": "00000000-0000-0000-0000-000000000000",
      "slot": "Photo",
      "fileName": "material.jpg",
      "sizeBytes": 204800,
      "isImage": true,
      "isPdf": false,
      "contentUrl": "/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content",
      "downloadUrl": "/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content?mode=download",
      "createdDate": "2026-07-29T08:00:00"
    }
  ]
}
```

`purchaseAvailability` là contract đọc chuẩn dùng chung cho material preview, formula item lookup và từng dòng
Material trong formula detail. Material chưa có bản ghi trạng thái vẫn trả object với `status = Available`,
`isPurchaseAvailable = true` và các field thời gian/lý do bằng `null`; FE không cần tự áp fallback.

`lastPurchase` láº¥y má»™t dÃ²ng Purchase Order Detail há»£p lá»‡ gáº§n nháº¥t, káº¿t há»£p Header vÃ  Supplier trong cÃ¹ng má»™t
EF query. Purchase Order pháº£i active, thuá»™c cÃ´ng ty hiá»‡n táº¡i vÃ  khÃ´ng á»Ÿ tráº¡ng thÃ¡i `Canceled`/`Cancelled`.
`quantity` Æ°u tiÃªn sá»‘ lÆ°á»£ng thá»±c nháº­n, náº¿u chÆ°a cÃ³ thÃ¬ dÃ¹ng sá»‘ lÆ°á»£ng yÃªu cáº§u. NVL chÆ°a tá»«ng mua tráº£
`lastPurchase = null`.

`unitPrice` chá»‰ tráº£ cho user thuá»™c nhÃ³m `PLM.FormulaPriceViewers`; user khÃ´ng cÃ³ quyá»n xem giÃ¡ váº«n nháº­n
thÃ´ng tin láº§n mua nhÆ°ng `unitPrice = null`. NgÃ y mua dÃ¹ng `PurchaseOrder.CreateDate`, Ä‘á»“ng bá»™ vá»›i nguá»“n
Purchase Order cá»§a service giÃ¡ hiá»‡n táº¡i.


### Xem hoáº·c táº£i ná»™i dung tá»‡p

```http
GET /api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content
GET /api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content?mode=download
```

Máº·c Ä‘á»‹nh API stream inline vÃ  báº­t HTTP range processing Ä‘á»ƒ trÃ¬nh duyá»‡t xem áº£nh/PDF theo nhu cáº§u.
`mode=download` tráº£ file dÆ°á»›i dáº¡ng táº£i xuá»‘ng.

Cáº£ hai endpoint yÃªu cáº§u user Ä‘Ã£ Ä‘Äƒng nháº­p. Backend chá»‰ tráº£ dá»¯ liá»‡u khi NVL active thuá»™c
`CurrentUser.CompanyId`; endpoint ná»™i dung cÃ²n báº¯t buá»™c attachment active vÃ  thuá»™c Ä‘Ãºng
`AttachmentCollectionId` cá»§a NVL. TrÆ°á»ng há»£p sai cÃ´ng ty, sai quan há»‡ cha-con, NVL/tá»‡p khÃ´ng active
hoáº·c file váº­t lÃ½ khÃ´ng tá»“n táº¡i Ä‘á»u tráº£ `404`, trÃ¡nh Ä‘á»ƒ lá»™ sá»± tá»“n táº¡i cá»§a dá»¯ liá»‡u cÃ´ng ty khÃ¡c.

## GiÃ¡ nguyÃªn váº­t liá»‡u theo nhÃ  cung cáº¥p

## API cáº­p nháº­t giÃ¡

```http
POST /api/v1/plm/material-suppliers/{materialsSupplierId}/price
```

Request:

```json
{
  "newPrice": 116000,
  "expectedCurrentPrice": 110000,
  "currency": "VND"
}
```

- `newPrice` được phép bằng 0 cho NVL gia công, không được âm và được làm tròn 4 chữ số thập phân theo precision của `MaterialsSupplier.CurrentPrice`.
- `expectedCurrentPrice` lÃ  tÃ¹y chá»n. Náº¿u giÃ¡ hiá»‡n táº¡i Ä‘Ã£ khÃ¡c giÃ¡ FE Ä‘á»c trÆ°á»›c Ä‘Ã³, API tá»« chá»‘i Ä‘á»ƒ trÃ¡nh ghi Ä‘Ã¨ cáº­p nháº­t cá»§a ngÆ°á»i khÃ¡c.
- `currency` lÃ  tÃ¹y chá»n; khÃ´ng truyá»n thÃ¬ giá»¯ currency hiá»‡n táº¡i, cÃ³ truyá»n thÃ¬ tá»‘i Ä‘a 10 kÃ½ tá»± vÃ  Ä‘Æ°á»£c chuáº©n hÃ³a uppercase.
- API chá»‰ cáº­p nháº­t liÃªn káº¿t Material - Supplier active, cÃ³ Material vÃ  Supplier cÃ¹ng company vá»›i current user.

TrÆ°á»›c khi thay giÃ¡, handler thÃªm má»™t dÃ²ng `Material.PriceHistory` chá»©a `OldPrice`, currency cÅ©, thá»i Ä‘iá»ƒm vÃ  employee thá»±c hiá»‡n.
GiÃ¡ má»›i, audit trÃªn `MaterialsSupplier` vÃ  lá»‹ch sá»­ giÃ¡ Ä‘Æ°á»£c ghi trong cÃ¹ng má»™t `SaveChangesAsync`. Gá»­i láº¡i Ä‘Ãºng giÃ¡/currency
hiá»‡n táº¡i bá»‹ tá»« chá»‘i vÃ  khÃ´ng táº¡o lá»‹ch sá»­ rá»—ng.

Endpoint yÃªu cáº§u policy `PLM.MaterialSupplierPrice.Update`, hiá»‡n dÃ nh cho `Admin`, `Developer`, `President` vÃ  `Purchaser`.
CÃ¡c role chá»‰ cÃ³ quyá»n xem giÃ¡ khÃ´ng Ä‘Æ°á»£c phÃ©p cáº­p nháº­t.

API `GET /api/v1/crm/quotations/product-pricing-options` tráº£ `supplierPrices` trong tá»«ng NVL Ä‘á»ƒ FE láº¥y Ä‘Ãºng
`materialsSupplierId`, hiá»ƒn thá»‹ danh sÃ¡ch nhÃ  cung cáº¥p vÃ  gá»­i báº£n ghi Ä‘Æ°á»£c chá»n vÃ o endpoint cáº­p nháº­t nÃ y.

Sau khi cáº­p nháº­t thÃ nh cÃ´ng, API tra cá»©u bÃ¡o giÃ¡ sáº½ Ä‘á»c giÃ¡ má»›i nhÆ° nguá»“n `MaterialSupplier` á»Ÿ láº§n táº£i tiáº¿p theo.
Thao tÃ¡c nÃ y khÃ´ng tá»± Ä‘á»™ng ghi Ä‘Ã¨ cÃ¡c giÃ¡ tá»•ng há»£p Ä‘ang lÆ°u trÃªn `Formula` vÃ  khÃ´ng tá»± Ä‘á»™ng Ä‘á»•i bÃ¡o giÃ¡ Ä‘Ã£ táº¡o.

### Ghi chÃº material preview

`GET /api/v1/plm/materials/{materialId}/preview` khÃ´ng tráº£ `previewAttachmentId` ná»¯a. FE tá»± chá»n file preview tá»« `attachments`, Æ°u tiÃªn `isImage`, sau Ä‘Ã³ `isPdf`, rá»“i file cÃ²n láº¡i náº¿u cáº§n.

Response tráº£ thÃªm `totalOnHandKg`: tá»•ng tá»“n kho thÆ°á»ng cá»§a NVL theo `Material.ExternalId`, `StockType.RawMaterial`, company hiá»‡n táº¡i vÃ  ká»‡ active. Field nÃ y dÃ¹ng cÃ¹ng nguá»“n `WarehouseShelfStock` vá»›i API tá»“n kho Warehouse, nhÆ°ng chá»‰ tráº£ tá»•ng nhanh cho cá»­a sá»• preview material.

`totalOnHandKg` của material preview không cộng tồn ở kệ cân trộn `CT.0.1` và không cộng kệ inactive. Rule loại kệ cân trộn chỉ áp dụng cho preview material, không đổi API tồn kho Warehouse chung.

## Danh sách NVL rà soát giá cho Kế hoạch

```http
GET /api/v1/plm/material-price-reviews?pageNumber=1&pageSize=15&keyword=PU
GET /api/v1/plm/material-price-reviews?priceStatus=MissingPrice
GET /api/v1/plm/material-price-reviews?priceStatus=NeedsReview&staleAfterDays=30
GET /api/v1/plm/material-price-reviews?priceStatus=UpToDate&staleAfterDays=30
GET /api/v1/plm/material-price-reviews/materials/{materialId}/suppliers
```

Response rút gọn của danh sách:

```json
{
  "items": [
    {
      "materialId": "00000000-0000-0000-0000-000000000000",
      "externalId": "NVL-001",
      "customCode": "VA-001",
      "name": "Hạt nhựa PU",
      "type": "Nguyên liệu",
      "unit": "kg",
      "currentPrice": 110000,
      "lastPriceUpdatedAt": "2026-09-10T09:00:00",
      "priceSource": "MaterialSupplier",
      "reviewStatus": "UpToDate",
      "usageSource": "RecentSampleRequestFormula",
      "lastUsedAt": "2026-09-08T08:00:00",
      "supplierCount": 2
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 15
}
```

Response rút gọn khi expand:

```json
{
  "materialId": "00000000-0000-0000-0000-000000000000",
  "suppliers": [
    {
      "materialsSupplierId": "00000000-0000-0000-0000-000000000001",
      "supplierId": "00000000-0000-0000-0000-000000000002",
      "supplierCode": "NCC-001",
      "supplierName": "Nhà cung cấp ABC",
      "currentPrice": 110000,
      "currency": "VND",
      "isPreferred": true,
      "lastPriceUpdatedAt": "2026-09-10T09:00:00"
    }
  ]
}
```

API danh sách trả mỗi NVL active một lần. Nguồn `RecentSampleRequestFormula` được ưu tiên trước: NVL active trong
Formula active đang gắn với Sample Request active của công ty hiện tại, có `CreatedDate` trong hai tháng gần nhất.
Trong nhóm này, thứ tự lấy theo ngày tạo Sample Request gần nhất giảm dần. Các NVL còn lại lấy từ
ManufacturingFormula active cùng công ty, theo `ManufacturingFormula.CreatedDate` gần nhất giảm dần. Nếu một NVL
có ở cả hai nguồn thì chỉ trả một dòng thuộc nguồn Sample Request. `lastUsedAt` là ngày nguồn dùng để sắp xếp,
không phải ngày nhập kho hoặc ngày thay đổi master NVL.

`priceStatus` là bộ lọc server-side và được áp dụng trước phân trang: `MissingPrice` là chưa có nguồn giá hợp lệ;
`NeedsReview` là có giá nhưng không có ngày giá hoặc ngày giá cũ hơn ngưỡng; `UpToDate` là giá còn trong ngưỡng.
`staleAfterDays` mặc định 30 ngày và được giới hạn từ 1 đến 365. Không truyền `priceStatus` thì trả tất cả trạng thái.
Mỗi item trả thêm `reviewStatus` theo cùng rule để FE hiển thị badge mà không tự tính lại.

Mỗi dòng trả `externalId`, `customCode`, `name`, `type`, `unit`, `currentPrice`, `lastPriceUpdatedAt`, `priceSource`, `reviewStatus`,
`usageSource`, `lastUsedAt` và `supplierCount`. `currentPrice`, `lastPriceUpdatedAt`, `priceSource` dùng đúng
`IMaterialPriceQueryService`: so giá Purchase Order hợp lệ mới nhất với giá Material - Supplier hợp lệ mới nhất
và chọn ứng viên có ngày mới hơn; khi bằng ngày thì ưu tiên Purchase Order. Chưa có giá trả `currentPrice = 0`,
`lastPriceUpdatedAt = null`, `priceSource = Unknown`. Giá `0` có nguồn hợp lệ vẫn là giá hợp lệ, không phải thiếu giá.

API suppliers chỉ trả liên kết Material - Supplier active cùng công ty, ưu tiên nhà cung cấp `isPreferred`, sau đó
theo tên. FE dùng `materialsSupplierId`, `currentPrice` và `currency` để gọi API cập nhật giá hiện có:

```http
POST /api/v1/plm/material-suppliers/{materialsSupplierId}/price
```

Nút Done gửi giá người dùng vừa nhập. Nút xác nhận "Giá OK" gửi lại chính `currentPrice` hiện tại làm `newPrice`
và đồng thời gửi `expectedCurrentPrice`. Backend chấp nhận cả trường hợp giá/currency không đổi, tạo một snapshot
`PriceHistory`, làm mới `MaterialsSupplier.UpdatedDate` và trả `priceChanged = false`. Nếu giá hoặc currency đổi,
response trả `priceChanged = true`. Cơ chế `expectedCurrentPrice` tiếp tục ngăn ghi đè khi người khác vừa cập nhật.

Cả hai API rà giá và API cập nhật yêu cầu policy `PLM.MaterialSupplierPrice.Update`. Role `PLPUUser` được phép dùng
cùng `Purchaser`, `Admin`, `Developer` và `President`. Mọi query đều lấy `CompanyId` từ current user; FE không được
truyền company để tránh xem hoặc sửa dữ liệu chéo công ty.

### Bổ sung và điều chỉnh nhà cung cấp cho NVL

Dropdown nhà cung cấp dùng API:

```http
GET /api/v1/plm/material-suppliers/lookup?materialId={materialId}&keyword=nhua&pageSize=50
```

API chỉ trả nhà cung cấp active thuộc công ty hiện tại. `keyword` tìm không phân biệt hoa thường theo mã hoặc tên;
`pageSize` mặc định 50, tối đa 100. Khi truyền `materialId`, backend kiểm tra NVL active cùng công ty và loại các NCC
đang được gắn active với NVL đó. Response chỉ chứa thông tin cơ bản phục vụ dropdown:

```json
[
  {
    "supplierId": "00000000-0000-0000-0000-000000000002",
    "supplierCode": "NCC_1011",
    "supplierName": "Công ty TNHH Cơ Khí Nhựa Việt Úc"
  }
]
```

Gắn NCC vào NVL và lưu giá ban đầu:

```http
POST /api/v1/plm/material-suppliers
Content-Type: application/json

{
  "materialId": "00000000-0000-0000-0000-000000000001",
  "supplierId": "00000000-0000-0000-0000-000000000002",
  "currentPrice": 0,
  "currency": "VND",
  "isPreferred": false,
  "minDeliveryDays": null
}
```

`materialId`, `supplierId` và `currentPrice` là bắt buộc; giá `0` hợp lệ và khác với chưa có nguồn giá.
`currency` bỏ trống mặc định `VND`. Backend từ chối cặp NVL - NCC active bị trùng; nếu cặp này từng bị inactive,
record cũ được kích hoạt lại thay vì tạo thêm record mới. Nếu đặt `isPreferred = true`, các NCC ưu tiên khác của
cùng NVL được bỏ cờ để giữ tối đa một NCC ưu tiên.

Điều chỉnh liên kết đã có:

```http
PATCH /api/v1/plm/material-suppliers/{materialsSupplierId}
Content-Type: application/json

{
  "newPrice": 52000,
  "expectedCurrentPrice": 50000,
  "currency": "VND",
  "isPreferred": true,
  "minDeliveryDays": 3,
  "isActive": true
}
```

PATCH chỉ thay đổi field được gửi; body rỗng bị từ chối. `supplierId` và `materialId` là định danh của liên kết nên
không được đổi bằng PATCH. `expectedCurrentPrice` là khóa cạnh tranh lạc quan: nếu giá hiện tại đã khác, FE phải tải
lại trước khi lưu. Khi request có `newPrice` hoặc `currency`, backend luôn lưu snapshot giá/tiền tệ cũ vào
`PriceHistory`, kể cả xác nhận lại cùng giá; response trả `priceHistoryId` và `priceChanged`. `minDeliveryDays = null`
hiện được hiểu là không đổi, API chưa hỗ trợ xóa giá trị này.

`isActive = false` đánh dấu không còn mua NVL từ riêng nhà cung cấp của liên kết này; liên kết sẽ không còn xuất hiện
trong danh sách nguồn cung/giá active và backend tự bỏ `isPreferred`. `isActive = true` kích hoạt lại liên kết nếu NVL
và nhà cung cấp vẫn active trong cùng công ty. Không được gửi đồng thời `isActive = false` và `isPreferred = true`.
Field không gửi là không đổi. Response POST/PATCH trả `isActive` là trạng thái hiện tại của liên kết.

Ba API lookup/POST/PATCH dùng chung policy `PLM.MaterialSupplierPrice.Update` và không nhận `CompanyId` từ client.
Sau POST/PATCH, FE có thể dùng DTO trả về để cập nhật dòng ngay hoặc gọi lại
`GET /api/v1/plm/material-price-reviews/materials/{materialId}/suppliers`; `supplierCount` ở danh sách tổng sẽ tăng
sau lần tải lại.

## Nền dữ liệu tình trạng mua và NVL thay thế

Khi NVL chuyển từ `Available` sang `Unavailable`, backend hiện chỉ lưu trạng thái và không phát notification,
SignalR hoặc Web Push. Topic `plm.material.purchase_unavailable` vẫn được giữ trong catalog để có thể bật lại sau,
nhưng command cập nhật tình trạng mua không publish topic này.

Schema PostgreSQL `Material` có hai entity nền cho luồng Kế hoạch thông báo tình trạng mua và Lab tự chọn NVL thay thế:

- `MaterialPurchaseAvailability`: quan hệ một-một với `Material`, lưu trạng thái hiện tại `Available` hoặc
  `Unavailable`, lý do, ngày hiệu lực, ngày dự kiến mua lại và audit người tạo/cập nhật.
- `MaterialReplacement`: quan hệ có hướng từ `SourceMaterial` sang `ReplacementMaterial`. Một cặp NVL chỉ có một
  record; các trường hợp áp dụng được lưu có cấu trúc trong cột `ApplicableContext` kiểu `jsonb`. Entity còn lưu
  ghi chú kỹ thuật, tỷ lệ thay thế, mức ưu tiên, cờ khuyến nghị và trạng thái active.

`MaterialReplacement` chỉ cung cấp phương án tham khảo; không biểu diễn việc hệ thống tự thay NVL trong công thức.
Hai NVL trong một quan hệ phải khác nhau, tỷ lệ thay thế nếu có phải lớn hơn `0`, và mức ưu tiên phải lớn hơn `0`.

### Kế hoạch cập nhật tình trạng mua

```http
PUT /api/v1/plm/materials/{materialId}/purchase-availability
Content-Type: application/json

{
  "status": "Unavailable",
  "reason": "Nhà cung cấp ngừng sản xuất",
  "effectiveFrom": "2026-09-15T08:00:00",
  "expectedAvailableDate": null,
  "note": "Lab chọn NVL thay thế trước khi lên công thức mới"
}
```

Endpoint dùng policy `PLM.MaterialPurchaseAvailability.Manage`; role hiện hành gồm `Purchaser`, `PLPUUser`,
`Admin`, `Developer` và `President`. Backend lấy company/employee từ current user và chỉ cập nhật NVL active cùng
company; id sai hoặc khác company trả `404`. `Unavailable` bắt buộc có `reason`; ngày dự kiến mua lại nếu có không
được trước ngày hiệu lực. Với `Available`, backend xóa ngày hiệu lực và ngày dự kiến mua lại.

Response trả trạng thái canonical đã lưu cùng `notificationPublished = false`; hiện mọi lần cập nhật đều không
phát notification. `GET /api/v1/plm/material-price-reviews` trả thêm `purchaseStatus`, lý do, ngày hiệu lực và
ngày dự kiến mua lại; NVL chưa có record được hiểu là `Available`.

Formula item lookup vẫn trả NVL `Unavailable` để Lab thấy lý do và có thể nhận biết bản ghi cũ, nhưng trả trạng thái
trong object `purchaseAvailability`. FE phải disable lựa chọn khi `purchaseAvailability.isPurchaseAvailable = false`.
Với item Product, `purchaseAvailability = null`. Check này trong `FormulaWriteService` đang được tắt tạm bằng cờ
`EnforceMaterialPurchaseAvailabilityOnFormulaWrite = false`; đổi thành `true` để bật lại. Các công thức/version lịch sử
không bị tự động thay hoặc xóa. Thay đổi này không kèm migration;
database phải có hai bảng đã thiết kế trước khi gọi API mới.

`GET /api/v1/plm/materials/{materialId}/preview` cũng trả object `purchaseAvailability`. Vì endpoint này luôn trả
NVL nên object không nullable; NVL chưa có record tình trạng được hiểu là `Available` và
`purchaseAvailability.isPurchaseAvailable = true`. Các field lý do/ngày trả `null` khi Kế hoạch chưa khai báo.

### Phương án NVL thay thế

```http
GET  /api/v1/plm/materials/{materialId}/replacement-options
GET  /api/v1/plm/materials/{materialId}/replacements
POST /api/v1/plm/materials/{materialId}/replacements
PUT  /api/v1/plm/materials/replacements/{materialReplacementId}
```

`replacement-options` là endpoint cho Lab và PLPU. Nó chỉ trả phương án `isActive = true` mà NVL thay thế vẫn active
và không ở trạng thái `Unavailable`; thứ tự là `isRecommended` trước rồi tới `priority` tăng dần. Mỗi item trả mã,
tên, tình trạng mua, `applicableContext`, ghi chú kỹ thuật, tỷ lệ, mức ưu tiên và cờ khuyến nghị; **không trả giá**.
Nếu không có phương án phù hợp hoặc NVL nguồn không thuộc company hiện tại, response là danh sách rỗng.

Danh sách `replacements` và hai mutation dành cho PLPU/Kế hoạch. Danh sách quản trị vẫn trả phương án inactive hoặc
NVL thay thế đang `Unavailable` để người quản lý điều chỉnh. POST tạo một cặp nguồn-thay thế; nếu cặp cũ đã inactive,
backend kích hoạt lại và trả `200` thay vì tạo bản ghi trùng. PUT thay toàn bộ dữ liệu nghiệp vụ của phương án;
`technicalNote = null` nghĩa là xóa ghi chú, còn `applicableContext` không gửi sẽ được lưu là `{}`. Hai NVL phải active,
cùng company, khác nhau; `replacementRatio` nếu có phải lớn hơn 0, `priority` lớn hơn 0, và `applicableContext` phải là
JSON object. Cờ `isRecommended` không bị giới hạn một lựa chọn duy nhất vì schema hiện hành không đặt ràng buộc đó.

Policy `PLM.MaterialReplacement.View` cho Lab, PLPU và nhóm quản lý giá/NCC xem phương án; policy
`PLM.MaterialReplacement.Manage` chỉ cho nhóm PLPU/Kế hoạch, Purchasing, Admin, Developer và President ghi hoặc xem
danh sách quản trị. Tất cả query/write lấy company và employee từ current user, không nhận company từ FE; sai ID hoặc
khác company trả danh sách rỗng (read) hoặc `404` (write).
