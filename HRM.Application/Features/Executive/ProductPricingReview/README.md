# Executive Product Pricing Review

Nhóm API mới cho drawer duyệt giá của President. Route, request/response DTO và MediatR handler độc lập hoàn toàn
với các API CRM pricing legacy; tầng dưới vẫn dùng chung nguồn giá vật tư, pricing policy, pricing engine,
optimistic concurrency, notification và quotation reconciliation để không tạo hai rule tính giá khác nhau.

## Quyền và scope

Tất cả endpoint yêu cầu policy `Executive.ManageProductPricingReview`, hiện chỉ gồm role `President` và
`Developer`. Handler kiểm tra lại `CompanyId`, `EmployeeId`, Product active/cùng company và quan hệ Product–Material
trước khi trả cost, margin, supplier price hoặc history. `quotationId`, `sourceId`, `materialId` và `versionId`
không được dùng độc lập nếu không thuộc Product/company trên route.

Hiện nhóm API này chỉ hỗ trợ `currency=VND`.

## Endpoint

```text
GET  /api/v1/executive/pricing-review/products/{productId}
GET  /api/v1/executive/pricing-review/products/{productId}/source-options
GET  /api/v1/executive/pricing-review/products/{productId}/sources/{sourceType}/{sourceId}/material-price-preview
GET  /api/v1/executive/pricing-review/products/{productId}/materials/{materialId}/supplier-prices
GET  /api/v1/executive/pricing-review/products/{productId}/materials/{materialId}/price-history
POST /api/v1/executive/pricing-review/products/{productId}/preview
GET  /api/v1/executive/pricing-review/products/{productId}/versions
GET  /api/v1/executive/pricing-review/products/{productId}/related-quotations
GET  /api/v1/executive/pricing-review/products/{productId}/va-lots
POST /api/v1/executive/pricing-review/products/{productId}/versions
PUT  /api/v1/executive/pricing-review/products/{productId}/versions/{versionId}
POST /api/v1/executive/pricing-review/products/{productId}/versions/{versionId}/approve
POST /api/v1/executive/pricing-review/products/{productId}/confirm-current-standard-price
```

`sourceType=VU` ánh xạ nội bộ sang `Formula`; `sourceType=VA` ánh xạ sang `ManufacturingFormula`.
Mã lot VA hiển thị chính là `ManufacturingFormula.ExternalId`, không phải một Production Order riêng.
Riêng nhóm API Executive Pricing Review, VA active cùng company thuộc Product khi được gắn qua
`ProductStandardFormula` hoặc xuất hiện trong `ProductionSelectVersion` của một lệnh sản xuất active thuộc Product.
Quan hệ `SourceBomVersion` không được dùng để đưa VA vào source switcher. Các VA này có `isEligible=true`, không
phụ thuộc status `Checking`, `IsSelect`, `Processing`, `Completed` hoặc việc đã có version `Released`. VA được sắp
theo `MfgProductionOrder.ManufacturingDate` giảm dần, fallback `ProductionSelectVersion.ValidFrom`, rồi
`ManufacturingFormula.UpdatedDate`. Rule nới này không thay đổi eligibility của API quotation legacy.

Các endpoint tab hỗ trợ `pageNumber`, `pageSize` (tối đa 100), `sortBy`, `sortDirection`. Source options bắt buộc
`sourceType=VU|VA`, hỗ trợ `keyword`, `currency=VND`, pagination và giới hạn tối đa 5 nguồn gần nhất mỗi trang. Price history hỗ trợ sort `recordedAt`, `supplierName`, `unitPrice`; versions
hỗ trợ `versionNumber`, `status`, `createdAt`, `updatedAt`; related quotations hỗ trợ `quotationCode`,
`quotationDate`; VA lots hỗ trợ `externalId`, `effectiveFrom`.

## Mở drawer

```http
GET /api/v1/executive/pricing-review/products/{productId}?currency=VND&quotationId={quotationId}&sourceType=VU&sourceId={formulaId}
```

`quotationId` là tùy chọn nhưng nếu truyền phải cùng company và chứa Product. Nếu không truyền, customer header
lấy từ Sample Request active mới nhất của Product. `sourceType` và `sourceId` phải cùng có hoặc cùng vắng mặt.
Khi không truyền nguồn, backend ưu tiên nguồn của Draft hiện hành, sau đó Approved; chỉ khi chưa có version mới
fallback Formula được customer chọn hoặc nguồn eligible đầu tiên.

Response rút gọn:

```json
{
  "header": {
    "productId": "product-guid",
    "productCode": "LP61097",
    "productName": "HẠT NHỰA MÀU ĐEN-BLACK",
    "customerId": "customer-guid",
    "customerCode": "KH001",
    "customerName": "CÔNG TY ABC",
    "currency": "VND",
    "lastUpdatedAt": "2026-09-09T13:09:00"
  },
  "formulaPricingPolicyId": "pricing-policy-guid",
  "selectedSource": {
    "sourceType": "VU",
    "sourceId": "formula-guid",
    "sourceCode": "VU260900066",
    "sourceName": "F001",
    "displayName": "VU260900066 · F001",
    "sourceNote": "Ghi chú kỹ thuật của công thức",
    "versionNumber": 3,
    "status": "Approved",
    "isEligible": true,
    "isCurrentlyApplied": true
  },
  "currentFormulaUse": {
    "sourceType": "VU",
    "sourceId": "formula-guid",
    "sourceCode": "VU260900066",
    "sourceName": "F001",
    "displayName": "VU260900066 · F001",
    "sourceNote": "Ghi chú kỹ thuật của công thức",
    "status": "Approved",
    "isEligible": true,
    "isCurrentlyApplied": true,
    "createdAt": "2026-09-09T13:09:00"
  },
  "realtimePriceComparison": {
    "currency": "VND",
    "approvedStandardPrice": 53000,
    "realtimeAdjustedStandardPrice": 54250,
    "standardPriceDifference": 1250,
    "standardPriceDifferencePercent": 2.3585,
    "approvedMaterialCostSnapshot": 28750,
    "realtimeMaterialCost": 29428,
    "materialCostDifference": 678,
    "materialCostDifferencePercent": 2.3583,
    "movementStatus": "Increased",
    "isMaterialCostComplete": true,
    "isIncreaseWarning": false,
    "warningThresholdPercent": 5,
    "calculatedAt": "2026-09-17T11:59:14+07:00"
  },
  "overview": {
    "pricingVersionId": "pricing-version-guid",
    "pricingVersionNumber": 3,
    "standardSellingPrice": 53000,
    "materialCost": 28750,
    "manufacturingCost": 5000,
    "profitAmount": 19250,
    "profitMarginPercent": 36.3208,
    "publisherNote": "Áp dụng cho đơn từ 100 kg",
    "totalMaterialCount": 5,
    "reviewRequiredCount": 3,
    "missingPriceCount": 1,
    "stalePriceCount": 2
  },
  "materials": [],
  "editor": {},
  "tabCounts": {}
}
```

`realtimePriceComparison` ở cấp root luôn so sánh giá và `MaterialCostSnapshot` của version `Approved` mới nhất
với chi phí NVL realtime của đúng Formula/VA đã lưu trên version đó. Nó không đổi theo `sourceType/sourceId` mà
drawer đang xem và không ghi dữ liệu vào database. Chưa có giá Approved dương thì field là `null`; nếu nguồn hoặc
giá realtime không đầy đủ, object vẫn có giá Approved nhưng các giá trị không tính được là `null` và
`isMaterialCostComplete=false`. Các field cost tuyệt đối tiếp tục tuân theo pricing visibility hiện hành.

`selectedSource` là nguồn đang được drawer hiển thị/tính giá; `currentFormulaUse` là nguồn hệ thống
đề xuất và hai field có thể khác nhau. Backend xếp tất cả candidate theo một **mốc sự kiện nghiệp vụ**
giảm dần, không ưu tiên VU hay VA theo loại:

`formulaPricingPolicyId` là ID policy `Published` hiện hành mà pricing engine resolve cho
`selectedSource` trong request đó. FE có thể dùng ID này để mở đúng policy đang điều khiển preview/tier;
nó là `null` khi nguồn không resolve được policy phù hợp theo company, profile hoặc currency. Field này
không phải ID policy snapshot của `ProductPricingVersion` lịch sử.

- VU active đã gửi mẫu: dùng `Formula.SentDate`.
- VA trạng thái `Checking` đã được chọn trong `ProductionSelectVersion` của lệnh sản xuất active: dùng
  `MfgProductionOrder.CreatedDate`.
- VA active có trạng thái `Checking` và được gắn với Product: dùng
  `ManufacturingFormula.CreatedDate`.

Khi trùng mốc, `SourceId` lớn hơn được dùng làm tie-breaker ổn định. `currentFormulaUse.createdAt` vẫn
là ngày tạo của chính Formula/VA; đây không phải mốc được dùng để xếp hạng. `currentFormulaUse=null`
nghĩa là không có candidate nào trong ba nhóm trên.
`selectedSource.sourceNote` và `currentFormulaUse.sourceNote` là `Formula.Note` đối với VU hoặc
`ManufacturingFormula.Note` đối với VA; chuỗi rỗng chỉ được trả là `null`. Note chỉ có trong detail của
nguồn đang xem/nguồn ưu tiên, không được trả hàng loạt từ source switcher.

### Trạng thái giá chuẩn chờ xác nhận lại

Response `GET /products/{productId}` có thêm `standardPriceState`. Đây là trạng thái nghiệp vụ của giá chuẩn, khác với `ProductPricingVersion.Status` (lifecycle của từng record). Giá Approved cũ vẫn được trả để tham khảo, nhưng `state=PendingReapproval` và `requiresPricingAction=true` khi Sale gửi yêu cầu báo giá mới hơn lần duyệt gần nhất, một Formula còn active được Lab xác nhận (`CheckDate`) sau lần xác nhận giá Approved gần nhất, `pricingReviewDueDate` đã qua, hoặc chi phí NVL realtime của source Approved tăng ít nhất ngưỡng cấu hình. `pricingAttentionSources` trả một hay nhiều giá trị `SaleQuotationRequested`, `LabFormulaConfirmed`, `ReviewExpired`, `MaterialCostIncreased`; `hasFormulaConfirmationPending` và `isReviewExpired` được giữ để FE cũ tương thích. Đọc notification không thay đổi các giá trị này; request Sale được đóng khi thu hồi hoặc khi có Approved version mới hơn.

President có thể duyệt một Draft mới với bất kỳ VU/VA hợp lệ, hoặc giữ giá cũ bằng:

```http
POST /api/v1/executive/pricing-review/products/{productId}/confirm-current-standard-price
```

```json
{
  "idempotencyKey": "client-generated-guid",
  "expectedApprovedPricingVersionId": "approved-version-guid",
  "expectedApprovedPricingVersionUpdatedAt": "2026-09-13T10:00:00",
  "internalNote": "Đã rà soát, giữ nguyên giá chuẩn"
}
```

Endpoint không sửa record Approved cũ: nó tạo một Approved version mới y hệt (kể cả tiers) với mốc `ApprovedAt` mới và supersede bản cũ. Đây là audit trail đồng thời đóng trạng thái chờ, không cần migration/bảng request mới. Nếu đang có Draft thì backend từ chối xác nhận giữ giá để không âm thầm hủy công việc đang làm.

`materials[].currentUnitPrice`, `materialAmount` và `priceDate` là dữ liệu realtime từ canonical latest-price
selector, không phải snapshot của ProductPricingVersion. `MissingPrice` nghĩa là selector không tìm được giá;
`StalePrice` nghĩa là `priceDate` cũ hơn 30 ngày. Danh sách rỗng nghĩa là nguồn không có item, không đồng nghĩa
với đã có giá đầy đủ.

Mỗi `materials[]` còn trả `price` để giải thích chính đơn giá realtime đó mà không thay thế các field phẳng cũ:

```json
{
  "price": {
    "latestPriceDate": "2026-09-15T15:44:46.309554",
    "unitPrice": 39629.6296,
    "source": "PurchaseOrder",
    "calculation": {
      "ruleCode": "GRINDING",
      "displayText": "Giá hạt nhựa gốc 35,000 đ/kg + chi phí nghiền 5,000 đ/kg = 40,000 đ/kg.",
      "baseItemName": "Hạt nhựa LLDPE MI=50",
      "baseUnitPrice": 35000,
      "basePriceSource": "PurchaseOrder",
      "fixedCostPerKg": 5000,
      "calculatedUnitPrice": 40000,
      "isComplete": true
    }
  }
}
```

`price.calculation` là dữ liệu chỉ để tooltip/explainability: `DIRECT_PRICE` là giá NVL trực tiếp;
`GRINDING`, `DILUTED_PIGMENT`, `COLOR_MASTERBATCH`, `COMPOUND` là các luật nội bộ; `UNRESOLVED_INTERNAL_RULE`
đi kèm `isComplete=false` khi không tìm được NVL/BTP gốc duy nhất cùng company. Field này dùng cùng giá đã đưa
vào `currentUnitPrice`, `materialAmount` và `overview.materialCost`; FE không được dùng nó để ghi đè giá.

Trong endpoint `material-price-preview`, dữ liệu tương tự nằm tại
`materials[].currentPrice.calculation`, vì object giá của endpoint đó đã có tên `currentPrice` từ contract cũ.

Preview không còn chặn request bằng validation tổng hợp chỉ vì `profitMarginPercent` được FE gửi kèm như một
giá trị suy ra. `changedField` xác định giá trị nào đang điều khiển phép tính; canonical pricing engine vẫn
kiểm tra các monetary input thực tế mà nó sử dụng. Khi `changedField=ProfitMarginPercent`, FE phải gửi margin
trong khoảng từ `0` (bao gồm) đến dưới `100`.

Metadata phân loại được trả theo chính dòng detail của nguồn đang mở: `categoryId` từ
`FormulaMaterial.CategoryId` đối với VU hoặc `ManufacturingFormulaMaterial.CategoryId` đối với VA;
`categoryName` là tên category gốc được tra bằng ID đó. Hai field mới `categoryGroup` và `categoryGroupName` mới
là khóa/nhãn chuẩn để FE chia đúng bốn section: `Pigment/Bột màu`, `Additive/Phụ gia`, `Resin/Nhựa`,
`Other/Khác`. Nhiều `categoryId` gốc có thể cùng một `categoryGroup`; FE không được group bằng `categoryId`.
`materialType` được chuẩn hóa thành đúng hai loại FE cần dùng: `MaterialFailure -> Material` và
`ProductFailure -> Product`. Dòng Material trả `materialId` và `productId=null`; dòng Product trả `productId`
và `materialId=null`. Thành phẩm lõi vẫn giữ nguyên mã, tên, số lượng, đơn giá và thành tiền đã resolve như
Product thường. FE chỉ mở supplier price/price history khi `materialId` có giá trị. Riêng `groupName` không tồn tại
trên detail nên lấy tên group active đầu tiên theo thứ tự chữ cái từ Material master và chỉ áp dụng cho Material.

`overview`/`editor` ưu tiên aggregate đã persist ở Draft, rồi Approved; material rows vẫn realtime để President
thấy tác động của giá vật tư mới. Khi không có version, các giá editor fallback từ pricing engine của nguồn.
`overview.publisherNote` và `editor.publisherNote` là ghi chú công khai của cùng PricingVersion đang cung cấp
giá chuẩn trong drawer; `null` nghĩa là version chưa có ghi chú công khai. Field này khác
`editor.internalNote`, là ghi chú nội bộ của bản giá. Các API CRM/SaleOrder hiển thị giá chuẩn Approved đọc cùng
`ProductPricingVersion.PublisherNote`, nên sau khi duyệt chúng sẽ thấy đúng ghi chú đã chốt cùng giá.
`editor.priceTiers` luôn lấy từ tier active của pricing policy hiện hành được resolve cho `selectedSource`;
backend không dùng `ProductPricingVersion.PriceTiers` snapshot làm dữ liệu editor. Vì vậy sau khi policy bật/tắt,
thêm hoặc bỏ tier, lần mở drawer tiếp theo sẽ trả đúng bộ tier mới. Tier editor luôn có
`pricingTierId=null`, `isStored=false`; `unitPrice` có thể `null` và `requiresManualPrice=true` khi policy yêu cầu
President nhập giá thủ công. Tier snapshot chỉ được giữ và trả trong lịch sử `PricingVersion`, không dùng để tính
hoặc submit giá mới. Nếu đã có Draft/Approved, `unitPrice` của từng tier được tính từ
`editor.standardSellingPrice` của version đang mở cộng/trừ offset tier của policy; không dùng giá mặc định do
policy tự suy ra. Khi chưa có version, editor dùng giá realtime do policy tính từ chi phí NVL, chi phí sản xuất và
margin mặc định. Sau khi người dùng đổi giá chuẩn, margin hoặc chi phí sản xuất, FE phải dùng response preview để
thay toàn bộ tier bằng kết quả mới từ backend.

## Supplier price và history

`supplier-prices` trả `MaterialsSupplier.CurrentPrice`. `isCurrentStandardPrice=true` đánh dấu nhà cung cấp gắn
với nguồn giá mà canonical latest-price selector đang dùng để tính NVL. Selector lấy PO mới nhất (bỏ PO hủy) và
supplier mới nhất (`UpdatedDate`, fallback `CreateDate`; hòa thì ưu tiên `IsPreferred`), rồi chọn giá hợp lệ có
ngày mới hơn. Khi PO thắng, cờ được map theo `PurchaseOrder.SupplierId` sang supplier tương ứng trong response;
khi supplier thắng, cờ map theo supplier link đó. Khi ngày bằng nhau, PO thắng. Trong cùng nguồn và cùng ngày,
backend chọn ID record lớn hơn; nếu vẫn hoàn toàn hòa, chọn giá lớn hơn. Cờ này là dữ liệu realtime, không phải
snapshot được ProductPricingVersion khóa lại.

`price-history[].unitPrice` lấy từ `PriceHistory.OldPrice`: đây là mức giá cũ được ghi khi supplier price thay đổi.
`recordedAt=null` nghĩa là record legacy không có `CreateDate`.

## Preview và công thức margin

Preview luôn validate lại Product/source/company/currency và từng supplier price. FE không thể gửi một `unitPrice`
tùy ý: giá phải bằng `MaterialsSupplier.CurrentPrice`; `expectedUpdatedAt` được dùng phát hiện giá NCC vừa bị user
khác cập nhật.

API Executive dùng **profit margin trên giá bán/doanh thu**:

```text
costBase = materialCost + manufacturingCost
profitAmount = standardSellingPrice - costBase
profitMarginPercent = profitAmount / standardSellingPrice * 100
```

Pricing engine dùng cùng công thức profit margin này. `ProductPricingVersion.ProfitMarginRate`,
`FormulaPricingPolicy.DefaultProfitMarginRate` và `profitMarginPercent` của Executive đều có chung semantics;
không còn bước chuyển đổi sang markup tại boundary. Khi người dùng nhập margin, giá bán được tính bằng
`costBase / (1 - profitMarginPercent / 100)`. Tiền tính toán làm tròn theo published pricing policy; phần trăm
làm tròn 4 chữ số, `MidpointRounding.AwayFromZero`.

Nếu bất kỳ item nguồn nào thiếu giá, preview/create/update/approve bị từ chối; drawer vẫn trả material status để
UI cho President biết item cần rà soát.

## Lưu, cập nhật và duyệt

`POST /versions` yêu cầu `idempotencyKey` là GUID. GUID này đồng thời được dùng làm `ProductPricingVersionId`, vì vậy
retry qua restart vẫn trả lại cùng version thay vì tạo bản ghi trùng. `expectedCurrentVersionId` và
`expectedCurrentVersionUpdatedAt` bảo vệ khỏi ghi đè khi drawer đã cũ. `sourceVersionNumber` nếu được gửi phải
khớp version nguồn hiện tại.

`PUT /versions/{versionId}` chỉ sửa Draft, có `expectedUpdatedAt`. Route Product/version đều được kiểm tra company
scope. `POST .../approve` chỉ duyệt Draft, tính lại material cost realtime, supersede Approved cũ, reconcile trạng
thái yêu cầu báo giá và publish notification pricing-approved bằng cơ chế CRM hiện có. SignalR/Web Push không đổi:
notification vẫn đi qua outbox hiện hữu.

Request `POST /versions` và `PUT /versions/{versionId}` nhận `publisherNote` để lưu ghi chú công khai cùng bản giá;
response create/update/approve và `GET /versions` trả lại field này. `POST .../approve` có thể gửi `publisherNote`
để ghi đè ghi chú ngay lúc duyệt; nếu không gửi thì giữ ghi chú đã lưu ở Draft. Chuỗi rỗng được chuẩn hóa thành
`null`. `publisherNote` chỉ mô tả giá chuẩn đã duyệt khi version trở thành `Approved`; Draft vẫn có thể chuẩn bị
ghi chú trước để duyệt sau.

`GET /products/{productId}/versions` còn trả `realtimePriceComparison` cho từng version `Approved` hoặc
`Superseded`. Object này dùng chung `StandardPriceRealtimeComparisonQueryService` với Product Pricing Workbench
và Sample Request Pricing Overview: giá/snapshot của chính version lịch sử được so với chi phí NVL realtime của
đúng Formula/VA đã gắn. Draft và Cancelled trả `null`; kết quả chỉ là read-model, không cập nhật version lịch sử.
Các source trong một trang được resolve theo batch và các field chi phí tuyệt đối vẫn tuân theo pricing visibility.

Do schema hiện tại chỉ snapshot aggregate, `materialPriceSelections` được validate và dùng tính
`MaterialCostSnapshot` nhưng ID supplier được chọn **chưa được persist theo từng dòng**. `sourceVersionNumber` cũng
là optimistic validation tại thời điểm save, chưa phải snapshot column. Muốn so sánh lịch sử đến từng vật tư/NCC
cần một migration riêng và phải được duyệt trước.

`null` ở các field giá nghĩa là chưa có dữ liệu tính/persist phù hợp; `0` là giá trị nghiệp vụ thực sự bằng 0.
`isCurrentlyApplied=true` chỉ khi nguồn VU/VA đó được gắn với `ProductPricingVersion` active có trạng thái
`Approved` và `StandardSellingPrice > 0`. Draft, `Formula.IsSelect` và thời hạn hiệu lực của
`ProductStandardFormula` không làm source mang badge “Đang áp dụng”. Khi chưa có giá chuẩn đã được duyệt,
tất cả source đều trả `isCurrentlyApplied=false`; `currentFormulaUse` vẫn cho biết công thức nghiệp vụ được ưu tiên.

## Source switcher: chi phí NVL và chênh lệch

```http
GET /api/v1/executive/pricing-review/products/{productId}/source-options
    ?sourceType=VU
    &keyword=VU2609
    &currency=VND
    &pageNumber=1
    &pageSize=5
```

Mỗi item giữ nguyên contract cũ và bổ sung `materialPricing`. Backend batch-load material và latest price của
tối đa năm source thuộc page hiện tại, cộng thêm tối đa một nguồn công thức chuẩn đang áp dụng; không query theo
từng source hoặc từng material.

Với `sourceType=VU`, endpoint chỉ trả công thức active có một trong bốn trạng thái:
`Approved`, `Completed`, `SampleSent`, `PendingSaleConfirmation`. Trong đó
`PendingSaleConfirmation` là trạng thái được lưu khi Lab yêu cầu Sale cập nhật công thức. Các trạng thái
`Draft`, `Cancelled`, `Rejected` không xuất hiện trong source switcher. Đây là rule riêng của Executive;
API pricing legacy của CRM không bị thay đổi.

```json
{
  "sourceType": "VU",
  "sourceId": "source-guid",
  "sourceCode": "VU260900066",
  "sourceName": "F001",
  "displayName": "VU260900066 · F001",
  "versionNumber": 3,
  "status": "SampleSent",
  "isEligible": true,
  "isCurrentlyApplied": false,
  "updatedAt": "2026-09-09T13:09:00",
  "materialPricing": {
    "materialCount": 5,
    "calculatedMaterialCost": 28750,
    "baselineMaterialCost": 27500,
    "differenceAmount": 1250,
    "differencePercent": 4.5455,
    "comparisonStatus": "Increased",
    "missingPriceCount": 0,
    "stalePriceCount": 2,
    "hasSourcePriceSnapshot": true,
    "sourceSnapshotMaterialCost": 27000,
    "canCompare": true
  }
}
```

`baselineMaterialCost` là **tổng chi phí NVL hiện tại** của công thức đang gắn với `ProductPricingVersion` active,
`Approved`, có `StandardSellingPrice > 0`, version mới nhất theo `productId + companyId + currency`. Backend resolve
lại toàn bộ giá hiện tại của nguồn chuẩn bằng canonical latest-price selector, cùng cách với source đang hover.
`differenceAmount = calculatedMaterialCost - baselineMaterialCost`; do đó delta chỉ phản ánh khác biệt giữa hai
công thức dưới cùng một bộ giá hiện tại, không phản ánh biến động giá kể từ lần chốt giá. Draft và source đang hover
không được dùng làm baseline. Không có Approved/source chuẩn, nguồn chuẩn không resolve được, hoặc bất kỳ bên nào
thiếu giá hiện tại thì source vẫn trả bình thường nhưng `canCompare=false`, `differenceAmount=null`,
`differencePercent=null`, `comparisonStatus=Unavailable`.

`calculatedMaterialCost` dùng canonical latest-price selector và cùng công thức realtime với pricing drawer:
`round(sum(quantity × currentUnitPrice))`. Nếu bất kỳ item nào không resolve được giá, tổng hoàn chỉnh và delta là
`null`; backend không trả subtotal dưới tên tổng. Giá `0` đã resolve từ một nguồn giá hợp lệ khác với không tìm thấy
giá và vẫn được tính là dữ liệu đầy đủ. `differencePercent=null` khi baseline bằng `0`, nhưng vẫn có thể so sánh
`differenceAmount` và `comparisonStatus`.

Trong `source-options`, `sourceSnapshotMaterialCost` vẫn là diagnostic tổng
`FormulaMaterial.TotalPrice` hoặc `ManufacturingFormulaMaterial.TotalPrice` đã persist trên source. Field này
không được dùng bởi lazy comparison bên dưới. Lazy comparison không đọc `UnitPrice/TotalPrice` persisted của source;
nó so sánh hai công thức bằng cùng bộ giá hiện tại.

## Lazy material-price preview

```http
GET /api/v1/executive/pricing-review/products/{productId}
    /sources/{sourceType}/{sourceId}/material-price-preview
    ?currency=VND
    &limit=8
    &sortBy=absoluteDifference
    &sortDirection=desc
```

`sourceType` dùng chung `VU|VA`; `sourceId` là `FormulaId` với VU và `ManufacturingFormulaId` với VA, đúng ID trả
từ `source-options`. Backend kiểm tra Product/source/company và eligibility; riêng VA giữ rule Executive hiện tại:
ManufacturingFormula active cùng company được gắn qua `ProductStandardFormula` hoặc đã xuất hiện trong
`ProductionSelectVersion` của lệnh sản xuất active thuộc Product đều hợp lệ; không suy ra quan hệ Product từ BOM.

Endpoint so sánh công thức Giám đốc đang xem với công thức chuẩn được gắn trên `ProductPricingVersion` active,
`Approved`, version mới nhất của cùng `productId + companyId + currency`. `SourceFormulaId` xác định chuẩn VU;
`SourceManufacturingFormulaId` xác định chuẩn VA và được ưu tiên nếu dữ liệu bất thường có cả hai id. Không lấy
`FormulaMaterial.UnitPrice`, `ManufacturingFormulaMaterial.UnitPrice` hoặc `MaterialCostSnapshot` làm baseline
phép so sánh.

Cả hai công thức được batch-load và tính bằng cùng bộ giá hiện tại trong một request. Với Material, bộ chọn lấy giá
hợp lệ mới hơn giữa Purchase Order và Material Supplier; với Product, thứ tự là ProductPricingVersion Approved,
chi phí công thức đang áp dụng, rồi Merchandise Order. Vì dùng cùng giá hiện tại, chênh lệch phản ánh việc thêm/bớt
item và thay đổi định lượng, không trộn biến động giá lịch sử vào biến động công thức.

Các dòng được ghép bằng `ItemType` đã normalize (`MaterialFailure -> Material`, `ProductFailure -> Product`) và
`ItemId`; chỉ fallback sang mã item khi mất quan hệ id. Dòng trùng item trong một công thức được gộp số lượng và trả
tất cả `formulaMaterialIds`. Mặc định dòng thiếu giá hiện tại đứng đầu, sau đó sort theo trị tuyệt đối của chênh lệch
thành tiền và mã item; `limit` tối đa 20.
Mỗi dòng comparison dùng cùng identity contract với drawer: Material có `materialId`, Product có `productId`, field
ID còn lại là `null`; `itemType` chỉ trả `Material` hoặc `Product`.

```json
{
  "viewedSource": {
    "sourceType": "VA",
    "sourceId": "manufacturing-formula-guid",
    "sourceCode": "VA260300387",
    "sourceName": "F003",
    "displayName": "VA260300387 · F003",
    "versionNumber": 3,
    "status": "Checking"
  },
  "standardSource": {
    "sourceType": "VU",
    "sourceId": "standard-formula-guid",
    "sourceCode": "VU260200123",
    "sourceName": "F001",
    "displayName": "VU260200123 · F001",
    "versionNumber": 4,
    "status": "SampleSent",
    "isSameAsViewedSource": false
  },
  "approvedPricingVersion": {
    "pricingVersionId": "pricing-version-guid",
    "pricingVersionNumber": 4,
    "approvedMaterialCostSnapshot": 71702,
    "currency": "VND"
  },
  "summary": {
    "priceBasis": "CurrentResolvedPrice",
    "calculatedAt": "2026-09-12T10:30:00",
    "standardFormulaMaterialCost": 71702,
    "viewedFormulaMaterialCost": 74980,
    "differenceAmount": 3278,
    "differencePercent": 4.5717,
    "comparisonStatus": "Increased",
    "standardMaterialCount": 7,
    "viewedMaterialCount": 8,
    "matchedMaterialCount": 6,
    "addedMaterialCount": 2,
    "removedMaterialCount": 1,
    "quantityChangedCount": 3,
    "unchangedCount": 3,
    "missingPriceCount": 0,
    "unitMismatchCount": 0,
    "canCompare": true,
    "unavailableReason": null
  },
  "materials": [
    {
      "itemType": "Material",
      "materialId": "item-guid",
      "productId": null,
      "materialCode": "NVL_NH_158",
      "materialName": "Hạt nhựa PP MI cao",
      "categoryId": "category-guid",
      "categoryCode": "NH",
      "categoryName": "Hạt nhựa",
      "categoryGroup": "Resin",
      "categoryGroupName": "Nhựa",
      "unit": "Kg",
      "currentPrice": {
        "unitPrice": 37037,
        "priceSource": "PurchaseOrder",
        "priceDate": "2026-09-10T08:00:00"
      },
      "standardFormula": {
        "formulaMaterialIds": ["standard-line-guid"],
        "isPresent": true,
        "quantity": 0.7,
        "amount": 25926
      },
      "viewedFormula": {
        "formulaMaterialIds": ["viewed-line-guid"],
        "isPresent": true,
        "quantity": 0.75,
        "amount": 27778
      },
      "quantityDifference": 0.05,
      "amountDifference": 1852,
      "differencePercent": 7.1434,
      "status": "QuantityChanged"
    }
  ],
  "totalCount": 8,
  "returnedCount": 8
}
```

Status dòng là `Unchanged`, `QuantityChanged`, `AddedToViewedFormula`, `RemovedFromViewedFormula`,
`MissingCurrentPrice`, `UnitMismatch` hoặc `Unavailable`. Chênh lệch dòng dùng `viewedAmount - standardAmount`;
chênh lệch tổng dùng `viewedFormulaMaterialCost - standardFormulaMaterialCost`. `approvedMaterialCostSnapshot`
chỉ là tham chiếu audit của bản đã duyệt, không tham gia phép tính mới.

Nếu chưa có bản Approved có source, `standardSource=null`, `canCompare=false`, `unavailableReason=NoStandardFormula`;
vẫn trả composition và giá hiện tại của công thức đang xem. Nếu bản Approved có source id nhưng source không còn
resolve được thì reason là `StandardSourceUnavailable`. Thiếu bất kỳ giá hiện tại nào làm tổng tương ứng bằng
`null`, không trả subtotal dưới tên tổng, và reason là `MissingCurrentPrice`. Unit không tương thích trả
`UnitMismatch`. `totalCount` là tổng item hợp nhất của hai công thức; `returnedCount` là số item sau `limit`.
Preview là read-only, không lưu pricing version và không thay đổi source.

Mỗi dòng comparison lấy `categoryId` trực tiếp từ dòng công thức (`FormulaMaterial` hoặc
`ManufacturingFormulaMaterial`), sau đó tra `categoryCode`/`categoryName` trong `Category` cùng company.
FE group bằng `categoryGroup`, không suy đoán từ mã material và không group bằng category gốc. Bốn nhóm ổn định
theo thứ tự hiển thị là `Pigment/Bột màu`, `Additive/Phụ gia`, `Resin/Nhựa`, `Other/Khác`. Dòng chỉ có trong
công thức đang xem dùng category của công thức đang xem; dòng đã bị loại khỏi công thức đang xem dùng category
của công thức chuẩn.
