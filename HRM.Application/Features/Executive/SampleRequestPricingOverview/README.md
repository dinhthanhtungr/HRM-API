# Sample Request Pricing Overview

## API

`GET /api/v1/executive/sample-request-pricing-overview`

API chỉ đọc dành cho màn hình tổng hợp của President. Mỗi item luôn đại diện cho một
`SampleRequest`; hai yêu cầu mẫu dùng chung Product vẫn là hai item độc lập. Endpoint yêu cầu policy
`Executive.ViewSampleRequestPricingOverview`, hiện chỉ nhận role `President` hoặc `Developer`, đồng thời
handler kiểm tra lại role, `CompanyId` và `EmployeeId` từ authenticated context.

Query hỗ trợ `pageNumber`, `pageSize` (mặc định 12, tối đa 100), `keyword`, `sampleStatuses` hoặc
alias `status`, `fromDate`, `toDate`, `customerId`, `productId`, `saleEmployeeId`, `categoryId`, `color`, `additiveCode`, `currency`,
`sortBy`, `sortDirection` và `searchType`. Currency mặc định và duy nhất hiện được hỗ trợ là `VND`. `sortBy` nhận
`latestActivityAt`, `createdDate`, `updatedDate`, `requestCode`/`externalId`, `productCode`,
`productName`, `colourCode`, `customerName`; mặc định là
`createdDate desc` để Yêu cầu mẫu mới tạo nằm trước. Keyword dùng PostgreSQL `ILIKE` và tìm theo mã yêu cầu, mã/tên Product,
colour code, mã/tên khách hàng, người tạo, Lab tạo Product, mã Formula liên quan giống Summary và mã báo giá (`BBG`).
Khi tìm mã báo giá, backend ưu tiên dòng báo giá liên kết đúng `SampleRequestId`. Với dữ liệu legacy chưa lưu
`SampleRequestId`, backend fallback theo cùng Customer và `ProductId`; dòng/báo giá inactive hoặc khác company không được tính.
Các filter, count, sort và pagination này chạy trước trong database. Với keyword có dạng mã định danh
như `TL41179C`, backend kiểm tra trước match chính xác theo mã Sample Request, Product, colour,
Customer hoặc Formula. Có match thì không chạy nhánh tìm chứa toàn văn/Quotation; không có match
mới fallback sang tìm `ILIKE` đầy đủ để giữ nguyên khả năng tìm theo tên hoặc một phần mã.

`searchType` thu hẹp keyword vào đúng nguồn định danh, dùng `prefix match` để người dùng có thể gõ dần:

- `quotation`: chỉ tìm `Quotation.ExternalId` (`BBG...`), rồi lấy Sample Request qua Quotation Line.
- `customer`: chỉ tìm `Customer.ExternalId` (`KH_...`).
- `sampleRequest`: chỉ tìm `SampleRequest.ExternalId` (`TP_...`).
- `product`: chỉ tìm `Product.Code` hoặc `Product.ColourCode` (`TL...`).
- `formula`: chỉ tìm `Formula.ExternalId` (`VU...`).
- `all`: tìm tự do như contract cũ.

Không truyền `searchType` vẫn tương thích FE cũ. Backend tự suy ra `BBG`, `KH_`, `TP_`, `TL`, `VU`; các keyword
khác dùng `all`. FE nên gửi `searchType` rõ ràng để không phụ thuộc quy ước prefix.

Filter nâng cao `view` dùng enum riêng `SampleRequestPricingOverviewView`; các giá trị cũ giữ nguyên tên và numeric
value để tương thích client hiện tại:

- `All`: không lọc pricing; giữ toàn bộ danh sách Sample Request mà user được phép xem.
- `NeedsPricing`: Product cần BGD xử lý giá: đã có quotation request định giá nhưng chưa có Approved pricing
  version, Sale gửi request mới hơn lần Approved gần nhất, hoặc đã có giá chuẩn nhưng đang `PendingReapproval`.
  `PendingReapproval` còn xảy ra khi lần duyệt giá gần nhất đã quá `ApprovedPricingReviewAfterDays`, hoặc Lab xác
  nhận Formula active mới sau mốc `ApprovedAt ?? UpdatedDate ?? CreatedDate` của bản giá chuẩn mới nhất, hoặc chi
  phí NVL realtime của source Approved tăng ít nhất `MaterialCostChangeThresholdPercent`. Có
  `recommendedFormula` hoặc thay đổi cấu thành công thức không tự làm Product khớp `NeedsPricing`; FE dùng riêng
  `view=FormulaChanged` cho trường hợp đó. Request đã thu hồi hoặc cũ hơn version Approved mới nhất không còn được
  tính. `pricingAttentionSources` chỉ rõ một hay nhiều nguyên nhân hiện có:
  `SaleQuotationRequested`, `LabFormulaConfirmed`, `ReviewExpired`, `MaterialCostIncreased`. BGD chỉ cần duyệt lại
  giá đang chọn hoặc tạo/duyệt giá theo nguồn khác; không bắt buộc phải dùng Formula Lab vừa xác nhận.
- `Draft`: Product có Draft pricing version active bằng currency đang chọn.
- `Approved`: Product có Approved pricing version active bằng currency đang chọn.
- `MaterialCostChanged`: Formula/VA đã lưu trên bản giá `Approved` mới nhất có chi phí NVL realtime tăng so với
  `MaterialCostSnapshot` của chính bản Approved đó. View này không áp dụng ngưỡng 5%; mọi mức tăng dương đều khớp.
  `NeedsPricing` vẫn chỉ coi biến động NVL là nguyên nhân cần xử lý khi mức tăng đạt
  `MaterialCostChangeThresholdPercent`.
- `ProductionMaterialCostChanged`: VA `Checking` được chọn ở lệnh sản xuất mới nhất của Product có chi phí NVL
  realtime tăng ít nhất ngưỡng trên so với `MaterialCostSnapshot` của bản Approved mới nhất. Không có VA sản xuất
  `Checking`, snapshot bằng 0, thiếu giá realtime, hoặc chi phí không tăng thì không khớp.
- `FormulaChanged`: Product có `pricing.recommendedFormula` khác cấu thành source của giá Approved hiện hành:
  thêm/bớt item, đổi quantity hoặc đổi unit. Chỉ thay giá realtime trên cùng cấu thành không khớp filter này.
  Product bắt buộc phải có `ProductPricingVersion` active, `Approved`, đúng company/currency và có Formula/VA source
  làm baseline; Product chưa có giá chuẩn không khớp filter này và được xử lý ở luồng định giá lần đầu.

`Draft` và `Approved` có thể cùng khớp nếu Product đồng thời có cả hai version hiện hành. Vì entity gốc là
Sample Request, mọi Sample Request dùng chung Product khớp filter vẫn được trả thành item riêng.

Chưa public `pricingStatuses` và `requiresPricingAction` làm filter. Các giá trị này phụ thuộc pricing source,
giá vật tư realtime và `ProductPricingHealthEvaluator`. Riêng hai view cost-change ở trên tính chính xác trước
pagination bằng batch source/material-price resolver; không lọc sau pagination nên `totalCount` và cardinality đúng.

Ví dụ rút gọn:

```json
{
  "items": [
    {
      "sampleRequestId": "4f69589f-72dc-46fd-b421-c685037daf12",
      "requestCode": "TP_30398",
      "createdDate": "2026-09-07T00:00:00",
      "status": "InProgress",
      "latestActivityAt": "2026-09-07T11:20:00",
      "product": {
        "productId": "4b44fa02-23cf-4af5-ae7c-6687701268e5",
        "code": "TP21119",
        "name": "HẠT PHỤ GIA - ADDITIVE MB",
        "colourCode": "Tint",
        "colorValue": null,
        "colorDisplayName": null,
        "categoryId": null,
        "categoryCode": "AMB",
        "categoryName": "Additive masterbatch",
        "additiveCode": null,
        "additiveDisplayName": null,
        "labEmployeeId": "2d3fb111-b5e8-42db-91c1-21551f28c602",
        "labName": "Trần Lab"
      },
      "customer": {
        "customerId": "6e99d8aa-e54a-42da-aa69-81619d689ec3",
        "code": "KH_3824",
        "name": "NHỰA HÒA THÁI",
        "saleEmployeeId": "6a6c9296-4ae1-4324-a2c5-e49ca2a7ee24",
        "saleName": "Nguyễn Sale"
      },
      "delivery": { "requestedDate": null, "expectedDate": null },
      "pricing": {
        "currency": "VND",
        "standardSellingPrice": 55000,
        "publisherNote": "Áp dụng cho đơn từ 100 kg",
        "currentMaterialCost": 33000,
        "manufacturingCost": 10000,
        "profitMarginRate": 27.91,
        "profitMarginPercent": 21.8182,
        "pricingAttentionSources": [
          "SaleQuotationRequested",
          "LabFormulaConfirmed",
          "ReviewExpired",
          "MaterialCostIncreased"
        ],
        "realtimePriceComparison": {
          "currency": "VND",
          "approvedStandardPrice": 244514,
          "realtimeAdjustedStandardPrice": 244514,
          "standardPriceDifference": 0,
          "standardPriceDifferencePercent": 0,
          "approvedMaterialCostSnapshot": 187727,
          "realtimeMaterialCost": 187727,
          "materialCostDifference": 0,
          "materialCostDifferencePercent": 0,
          "movementStatus": "Unchanged",
          "isMaterialCostComplete": true,
          "isIncreaseWarning": false,
          "warningThresholdPercent": 5,
          "calculatedAt": "2026-09-17T11:59:14.7456778+07:00"
        },
        "materials": [
          {
            "formulaMaterialId": "3f29c14f-95bb-4b79-91f6-1b64f280622a",
            "itemId": "53430574-1806-4d1e-98ed-af1b45d9d9f3",
            "itemType": "Material",
            "itemCode": "NVL_001",
            "itemName": "Hạt nhựa PP",
            "quantity": 1.2,
            "unit": "KG",
            "availabilitySummary": {
              "status": "Unavailable",
              "isPurchaseAvailable": false,
              "reason": "Nhà cung cấp tạm ngừng bán",
              "effectiveFrom": "2026-09-18T00:00:00+07:00",
              "expectedAvailableDate": "2026-10-01T00:00:00+07:00"
            }
          }
        ],
        "pricingStatus": "Approved",
        "pricingHealthStatus": "Ready",
        "requiresPricingAction": false,
        "draftPricingVersionId": null,
        "approvedPricingVersionId": "a05acfff-b8d4-4863-8c9c-7721338ca7bf",
        "recommendedFormula": {
          "sourceType": "VA",
          "sourceId": "01a0c741-e6db-7b5a-829b-bc37a6de8084",
          "sourceCode": "VA260900349",
          "sourceName": "F001",
          "displayName": "VA260900349",
          "status": "Checking",
          "isEligible": true,
          "isCurrentlyApplied": false,
          "createdAt": "2026-09-10T08:30:00+07:00",
          "candidateAt": "2026-09-18T09:00:00+07:00",
          "candidateDateSource": "ProductionOrderCreatedDate",
          "currency": "VND",
          "realtimeMaterialCost": 41481,
          "manufacturingCost": 3000,
          "profitMarginRate": 1.2838,
          "realtimeStandardSellingPrice": 45059,
          "priceCalculationFormula": "(41481 + 3000) / (1 - 0.012838) = 45059 VND",
          "isRealtimeMaterialCostComplete": true,
          "missingMaterialPriceCount": 0,
          "calculatedAt": "2026-09-22T20:32:00+07:00"
        },
        "displayedFormula": {
          "sourceType": "Formula",
          "sourceId": "d006bb1e-7b39-43d8-9d79-9e129a0d39ae",
          "code": "CT_1001",
          "name": "Formula 1001",
          "status": "Approved",
          "priceKind": "Standard"
        }
      },
      "conversation": {
        "conversationId": "be547fec-da04-491a-b143-7b742099b266",
        "totalMessageCount": 12,
        "unreadCount": 2,
        "lastMessageAt": "2026-09-07T11:20:00"
      },
      "actions": {
        "canOpenSampleRequest": true,
        "canOpenPricingDetail": true,
        "canManagePricing": true,
        "canOpenConversation": true
      }
    }
  ],
  "pageNumber": 1,
  "pageSize": 12,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

## Nguồn dữ liệu và semantics

### Lịch sử giá bán Merchandise Order

Mỗi card có thêm `latestMerchandiseOrder`, là `null` khi Product chưa có dòng bán hợp lệ trong
phạm vi customer/company của người gọi. Object này lấy từ một `MerchandiseOrderDetail`: `quantity`
là `ExpectedQuantity` của đúng dòng, `unitPrice` là `UnitPriceAgreed` (giá bán), còn `currency` lấy
từ Merchandise Order. `orderedAt` hiện map từ `MerchandiseOrder.CreateDate` vì schema chưa có
`confirmedAt`/`orderedAt`; không có dữ liệu Purchase Order, Goods Receipt hay giá mua trong contract này.
Mã/tên item và unit ưu tiên Product hiện tại, snapshot của line chỉ là fallback cho mã/tên legacy.
`saleEmployeeId` và `saleName` là Sale phụ trách đã được snapshot trên chính Merchandise Order
(`ManagerById`/`ManagerByNameSnapshot`), không phải assignment hiện tại của Customer.
`orderType` của `latestMerchandiseOrder` luôn là `Merchandise`; card giá bán gần nhất cố ý không dùng đơn
`SampleRequest`, `Complaint` hoặc `Internal` để đơn mẫu/khiếu nại giá 0 không ghi đè giá bán hàng hóa gần nhất.

`GET /api/v1/executive/merchandise-order-price-history` dùng cùng policy Executive Overview và cùng
customer/company visibility. `itemId` (`ProductId`) là bắt buộc; API không nhận `itemCode` làm filter.
Filter hỗ trợ `customerId`, `currency`, `fromDate`, `toDate`, phân trang 1..100 và sort `orderedAt`,
`unitPrice`, `quantity`. Lịch sử nhận `OrderType` là `Merchandise`, `SampleRequest` hoặc `Complaint` và status
`Approved`, `Processing`, `Delivering`, `Delivered`, `Completed` được coi là sale history. `Internal`,
`New`, `Pending`, `Paused`, `Cancelled` bị loại. Dòng `Complaint` có thể có `unitPrice = 0` và được giữ nguyên để
FE thể hiện đúng lịch sử xử lý khiếu nại. Với hai endpoint, bản ghi
gần nhất là `CreateDate desc`, sau đó order id và line id desc để có thứ tự ổn định.

- `requestCode`, ngày tạo, status, Product, Customer, Category, additive và delivery project trực tiếp từ
  cùng entity/navigation và cùng date/status semantics của Sample Request Summary. `product.colorValue` và
  `product.colorDisplayName` đều lấy từ `Product.ColourName`, giữ `null` khi tên màu chưa có; `product.colourCode`
  vẫn là mã màu riêng. Status là canonical code.
- `customer.saleEmployeeId`/`customer.saleName` là Sale phụ trách của chính Sample Request, lấy từ
  `SampleRequest.ManagerBy`; đây cũng là nguồn của filter `saleEmployeeId`, không phải người tạo yêu cầu.
  `product.labEmployeeId`/`product.labName` là nhân viên Lab đã tạo Product, lấy từ `Product.CreatedBy`.
  Thông tin nhân viên chỉ được trả khi navigation thuộc cùng company; thiếu hoặc lệch company thì hai field
  tương ứng là `null`.
- Pricing load theo tập `productId` của trang và currency. Version hiện hành chọn đúng như Workbench:
  Draft/Approved active có `Version` lớn nhất, sau đó `UpdatedDate ?? CreatedDate` mới nhất; Draft được ưu
  tiên làm stored pricing. Source đã lưu được resolve bằng source id; nếu chưa có version thì dùng
  `ProductPricingWorkbenchSourceSelector`. Giá, margin, chênh lệch, health và action đi qua chính
  `ProductPricingWorkbenchMapper`, `ProductPricingHealthEvaluator` và `ProductPricingWorkbenchVisibility`.
- `pricing.profitMarginRate` và alias `pricing.profitMarginPercent` đều có cùng semantics profit margin trên
  giá bán. Giá trị realtime được tính từ
  `(standardSellingPrice - currentMaterialCost - manufacturingCost) / standardSellingPrice * 100`, cùng
  shared calculator với pricing-review drawer. Thiếu bất kỳ đầu vào nào hoặc giá bán không dương thì
  `profitMarginPercent` trả `null`.
- `pricing.publisherNote` là ghi chú công khai của `ProductPricingVersion` Approved đang cung cấp giá chuẩn;
  nó đi cùng `standardSellingPrice` và `approvedPricingVersionId`. Field là `null` khi chưa có bản Approved
  hoặc người duyệt không ghi chú; Draft và giá system-calculated không được dùng làm nguồn ghi chú public.
- `pricing.realtimePriceComparison` dùng chung `StandardPriceRealtimeComparisonQueryService` với Product Pricing
  Workbench. Read-model luôn lấy bản `Approved` mới nhất làm mốc, rồi tính lại giá chuẩn từ NVL realtime,
  chi phí sản xuất đã duyệt và biên lợi nhuận trên giá bán đã lưu ở bản Approved:
  `giá = (NVL realtime + chi phí sản xuất Approved) / (1 - margin Approved%)`.
  Nó không ghi đè giá chuẩn và không tạo pricing version. Field là `null`
  khi không có giá Approved dương hoặc current user không được xem giá Approved. Các field chi phí tuyệt đối trong
  object tiếp tục được che theo pricing capability; `calculatedAt` là thời điểm batch comparison được tính.
- `pricing.materials` là danh sách dòng phẳng của đúng Formula/VA trong `displayedFormula`; API không bung cây
  thành phần con. `availabilitySummary` chỉ có trên dòng `Material` hoặc `MaterialFailure` đã resolve được NVL
  cùng company; dòng `Product`/`ProductFailure` không serialize field này. NVL chưa có record trạng thái riêng được hiểu là
  `Available` theo `MaterialPurchaseAvailabilityRules`; `Unavailable` trả thêm lý do và ngày dự kiến mua lại nếu có.
  Danh sách này đi cùng quyền xem material cost; khi capability không cho phép thì trả mảng rỗng.
- Không có version/source hợp lệ vẫn giữ item Sample Request. Các số giá và version id là `null`, status là
  `NoEligibleSource`, health là `NoEligibleSource`, `requiresPricingAction = true`.
- `pricing.displayedFormula` là chính source mà Workbench đang dùng để tạo giá hiển thị, không query/chọn lại
  Formula theo rule riêng. Khi có Draft hoặc Approved pricing version, source gắn với version được trả và
  `priceKind = Standard`. Khi chưa có version nhưng pricing engine chọn được source fallback để preview,
  `priceKind = SystemCalculated`; các pricing version id vẫn là `null`. `sourceType` phân biệt `Formula` và
  `ManufacturingFormula`, vì vậy FE không được giả định mọi `sourceId` đều là `FormulaId`. Không có source hợp
  lệ thì `displayedFormula = null`.
- `pricing.recommendedFormula` là công thức hệ thống đề xuất sử dụng và độc lập với `displayedFormula`.
  Backend dùng cùng `SuggestedPricingFormulaQueryService` với `currentFormulaUse` của Pricing Review drawer,
  xếp tất cả candidate theo `candidateAt desc`, rồi `sourceId desc` khi trùng mốc; không ưu tiên VU hay VA theo loại:
  VU active đã gửi mẫu dùng `Formula.SentDate` (`candidateDateSource=SentDate`); VA `Checking` được chọn trong
  lệnh sản xuất active dùng `MfgProductionOrder.CreatedDate` (`ProductionOrderCreatedDate`).
  `ProductStandardFormula` không tham gia chọn recommendation; không có candidate VU hoặc VA sản xuất thì field là `null`.
- Khi đã có Formula/VA gắn với giá chuẩn Approved, candidate chỉ được trả thành `recommendedFormula` nếu cấu thành
  khác công thức chuẩn: thêm/bớt item, thay quantity hoặc thay unit. Nếu cùng source hoặc cấu thành giống nhau thì
  `recommendedFormula=null`; biến động giá realtime của cùng NVL không được xem là thay đổi công thức. Khi chưa có
  source Approved để làm baseline, candidate mới nhất vẫn được trả để hỗ trợ thiết lập giá lần đầu. Nếu bản Approved
  có source nhưng một trong hai source không còn resolve được thì backend không suy đoán recommendation.
- `displayName` của source `VA` chỉ dùng `sourceCode`; `sourceName` vẫn được trả ở field riêng. Source `VU`
  tiếp tục dùng dạng `sourceCode · sourceName`.
- `recommendedFormula.realtimeMaterialCost` là tổng chi phí NVL realtime của chính candidate, không phải giá bán
  đề xuất và không phụ thuộc pricing policy. `isRealtimeMaterialCostComplete=false` hoặc
  `missingMaterialPriceCount>0` nghĩa là ít nhất một dòng NVL/Product con chưa resolve đủ giá. Giá trị cost là
  `null` khi dữ liệu chưa đầy đủ hoặc current user không có quyền xem material cost.
- `recommendedFormula.realtimeStandardSellingPrice` dùng chung `StandardSellingPriceCalculator` với phần so sánh
  giá realtime: `(realtimeMaterialCost + manufacturingCost Approved) / (1 - profitMarginRate Approved / 100)`.
  `manufacturingCost` và `profitMarginRate` lấy từ bản Approved hiện hành, không lấy từ candidate. Nếu Approved
  chưa lưu margin thì backend suy ra margin từ giá chuẩn và cost snapshot Approved bằng cùng helper. Khi chưa có
  Approved, NVL candidate chưa đầy đủ, chi phí sản xuất thiếu hoặc margin không hợp lệ, giá và
  `priceCalculationFormula` là `null`. Các thành phần và chuỗi công thức được che độc lập theo pricing capability;
  FE không tự tính lại giá.
- Conversation chỉ match `RelatedType = SampleRequest` và `RelatedId = sampleRequestId` trong cùng company.
  Chỉ đếm message chưa soft-delete; không load body, payload, attachment hoặc participant graph.
  `unreadCount` dùng cùng rule inbox: message không do employee hiện tại gửi và có read-state `IsRead=false`
  của employee đó. Không có conversation trả id/ngày `null`, count `0`. `canOpenConversation` true khi employee
  hiện tại là participant active, hoặc khi current user là President/Developer đọc conversation Sample Request
  cùng company qua quyền Executive Overview; ngoại lệ không áp dụng cho conversation Internal hoặc Quotation.
- Cost, material cost, margin, realtime difference và pricing version id chỉ xuất hiện vì policy endpoint
  giới hạn President/Developer, cũng là tập `CanManage` canonical của Workbench. Projection không nhận
  `companyId` từ client.

## Query và index

Base Sample Request được scope/filter/count/sort/page trong SQL. Khi không sort theo `latestActivityAt`,
API không tạo correlated subquery pricing/conversation activity cho toàn bộ tập lọc; thời điểm activity
trả về vẫn được hợp nhất từ dữ liệu của card sau pagination. Sau đó có query set-based cho pricing
versions/sources/quotation requests và một query aggregate conversation cho các id trên trang; không có query
theo từng card.

Với Product chưa có pricing version, card chỉ resolve một Formula/VA fallback mới nhất trước rồi mới tải
NVL và giá realtime của nguồn đó. API không còn tải NVL/giá của mọi nguồn hợp lệ chỉ để chọn fallback.

`pricingHealthStatus = MaterialCostChanged` xảy ra khi `currentMaterialCost` tăng ít nhất
`MaterialCostChangeThresholdPercent` so với `storedMaterialCostSnapshot`; ngưỡng mặc định là `5%`.
Giá NVL giảm hoặc tăng dưới ngưỡng không trả status này. Khi cần lọc toàn bộ dữ liệu, FE dùng
`view=MaterialCostChanged` hoặc `view=ProductionMaterialCostChanged`, không tự lọc item của một trang.
Hai view cost-change và view `FormulaChanged` có batch resolver riêng để lọc chính xác trước pagination.
`NeedsPricing` chỉ chạy batch cost-change theo ngưỡng và không chạy so sánh cấu thành công thức. `All`, `Draft`
và `Approved` giữ SQL paging fast path; FE dùng `FormulaChanged` khi cần danh sách đổi cấu thành.

Trong Executive Pricing Review, thiếu giá realtime của NVL không chặn Preview, tạo Draft hoặc Duyệt và áp dụng.
Backend dùng `SourceUnitPrice` snapshot của Formula/VA khi có; nếu snapshot cũng không có thì item đó đóng góp `0`
vào chi phí NVL. `missingPriceCount` vẫn được trả để FE cảnh báo rằng chi phí đã lưu chưa đầy đủ.

Các index tương đương đã có: Product Pricing
`(CompanyId, ProductId, Currency, Status, IsActive)`, conversation
`(CompanyId, RelatedType, RelatedId)`, message `(InternalConversationId, SentAt)` và read-state
`(EmployeeId, IsRead)` cùng composite primary key `(InternalMessageId, EmployeeId)`. Chưa thêm migration/index.
Nên xác minh execution plan thực tế trước khi bổ sung composite index Sample Request
`(CompanyId, IsActive, Status, CreatedDate)` với include/use kế tiếp cho `ProductId`, `CustomerId`, `ManagerBy`;
không tự tạo vì repo chưa có migration được user duyệt và cần tránh index trùng trong database thật.

## NVL của công thức nguồn giá chuẩn thay đổi sau xác nhận

Khi Lab lưu thay đổi NVL/định lượng của Formula `Approved` đang là nguồn của giá chuẩn VND Approved hiện hành, backend ghi lại `CheckBy`/`CheckDate` như một lần xác nhận lại công thức và gửi đúng luồng yêu cầu duyệt lại giá chuẩn. Vì `CheckDate` mới lớn hơn lần duyệt giá gần nhất, Sample Request của Product khớp `view=NeedsPricing`; `pricingAttentionSources` có `LabFormulaConfirmed`. Duyệt giá mới sau mốc này sẽ giải quyết yêu cầu theo rule hiện có. Đây là trường hợp tái xác nhận tường minh trong luồng lưu công thức; chỉ có candidate `recommendedFormula` hoặc lưu NVL không đổi vẫn không đủ để vào queue này.