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
- Product: giÃ¡ Merchandise Order má»›i nháº¥t.
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
