# Giá công thức

## API ghi công thức

Các API ghi công thức nằm dưới:

```http
POST   /api/v1/plm/formulas
PUT    /api/v1/plm/formulas/{formulaId}
PATCH  /api/v1/plm/formulas/{formulaId}/status
DELETE /api/v1/plm/formulas/{formulaId}
```

## Danh sách công thức khi lên đơn hàng

```http
GET /api/v1/plm/formulas?productId={productId}&isMerchadiseOrder=true
```

Khi `isMerchadiseOrder=true`, backend chỉ trả `formulaDevs`: các Formula active của Product đã được
khách hàng chốt trong một Sample Request active có `Status = Completed` và `SampleRequest.FormulaId`
trỏ tới Formula đó. `formulaSelects` và `formulaStandard` trả danh sách rỗng vì đây là Manufacturing Formula,
không phải loại Formula được lưu vào dòng Merchandise Order.

Khi cờ không gửi hoặc bằng `false`, API giữ nguyên hành vi cũ và trả cả ba nhóm Formula. Tên query hiện tại
giữ nguyên `isMerchadiseOrder` để tương thích client đang dùng.

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

`itemType` nhận các giá trị enum `Material`, `Product`, `MaterialFailure`, `ProductFailure`.
Với item material, `itemId` là `MaterialId`; với item product, `itemId` là `ProductId`.
Nếu không gửi `categoryId`, backend lấy category từ material/product được chọn. `unitPrice` không gửi thì mặc định `0`.
Khi có danh sách `materials`, `Formula.TotalPrice` được tính lại bằng tổng `quantity * unitPrice`.

`PATCH /status` cho chuyển sang:

```text
Approved   -> Formula.Status = Approved, Formula.CheckBy/CheckDate = current employee/time.
SampleSent -> Formula.Status = SampleSent, Formula.SentBy/SentDate = current employee/time.
Completed  -> Formula.Status = Completed, Formula.IsSelect = true, SampleRequest.Status = Completed.
```

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

API Formula detail/preview lấy kết quả realtime từ `FormulaPricingEngine`, với policy Published
đúng company/profile/currency. Manufacturing cost mặc định, profit margin, rounding và tiers đều
đến từ policy DB; không suy profile từ product code/Additive và không fallback khi thiếu policy.

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
`PricingPolicyMissing`; khi thiếu giá NVL trả `pricing = null` cùng `MaterialPriceMissing`.
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
luật hard-code. Thiếu ít nhất một giá material thì `pricing = null`, `pricingStatus = MaterialPriceMissing`.
Giá được chọn để lập báo giá vẫn phải lưu snapshot vào dòng/bậc giá báo giá.

## Ghi chú riêng cho API chi tiết công thức

`GET /api/v1/plm/formulas/{formulaId}?currency=VND` yêu cầu currency và không trả `materialCost` snapshot. API này trả `realtimeMaterialCost`,
`isRealtimeMaterialCostComplete`, `missingMaterialPriceCount`, `manufacturingCost`, `standardSellingPrice`,
`profitMarginRate`, policy id/version, `suggestedPriceTiers`, `pricingStatus` và `pricing`.

Nếu một dòng NVL/sản phẩm không có latest price hoặc latest price là `null`, dòng đó trả
`hasLatestPrice = false`, `latestUnitPrice = 0`, `latestTotalPrice = 0` để FE cảnh báo; nhưng engine đánh dấu
material cost không đầy đủ và không tạo block `pricing`.

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

POST lưu yêu cầu `PLM.Formula.Manage`; POST khôi phục yêu cầu đồng thời `PLM.Formula.Manage` và `PLM.FormulaPricing.Update` vì action này ghi lại cả giá snapshot. Current user phải có `EmployeeId`. Khôi phục không sửa version cũ: backend thay header/material active của Formula bằng snapshot đã chọn rồi tạo một version mới với `ChangeReason = Restored from version ...`. Do contract FormulaVersion không snapshot `ExternalId`, `ProductId`, `EffectiveDate`, `IsSelect` và các field audit trạng thái, thao tác restore giữ nguyên các field đó trên Formula hiện tại.
