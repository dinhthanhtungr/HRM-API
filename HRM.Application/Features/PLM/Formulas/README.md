# Giá công thức

## API ghi công thức

Các API ghi công thức nằm dưới:

```http
POST   /api/v1/plm/formulas
PUT    /api/v1/plm/formulas/{formulaId}
PATCH  /api/v1/plm/formulas/{formulaId}/status
DELETE /api/v1/plm/formulas/{formulaId}
```

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

Backend cho phép gửi Formula đang `Approved` hoặc gửi lại Formula đang `SampleSent`. Mỗi lần gửi luôn tạo một Trial mới, lưu `DeliveredSampleQuantityKg`, tự gán `SentBy` là employee hiện tại, `SentDate` và `UpdatedDate` là thời điểm xử lý. Notification cho các participant liên quan có kèm khối lượng mẫu.
Endpoint này chỉ nhận trạng thái `Approved` hoặc `SampleSent`; không nhận `Completed`.
Formula chỉ được hoàn thành khi Sale ghi nhận Trial `Approved` qua action phản hồi khách.
Trong một lần lưu, backend đổi `Formula.Status = SampleSent`, đổi `SampleRequest.Status = SampleSent`, tạo Trial có
`TrialNo = max + 1` và snapshot khách hàng/sản phẩm/mã màu. `SampleRequest.FormulaId` chưa được gán ở bước này;
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

## Giá bán tiêu chuẩn và tỷ lệ lợi nhuận

API tra cứu và response PATCH chỉ trả hai field giá bán:

- `standardSellingPrice`: dùng `Formula.PresidentPrice` nếu DB có giá; nếu chưa có
  thì mặc định bằng `realtimeMaterialCost + manufacturingCost`.
- `profitMarginRate`:
  `(standardSellingPrice - costBase) / costBase * 100`.

`manufacturingCost` dùng `Formula.ProductionPrice` nếu lớn hơn 0; nếu không dùng
mặc định `10.000` cho Powder hoặc `20.000` cho Compound. Field này vẫn có giá trị
khi công thức thiếu giá NVL realtime.

`Formula.ProfitMarginPrice` là field cũ và không còn tham gia contract hoặc rule tính giá này.

`pricingUpdatedDate` là thời điểm snapshot pricing trên Formula được cập nhật gần nhất. FE gửi lại giá trị này qua
`expectedUpdatedDate` khi PATCH để tránh ghi đè thay đổi mới hơn của người khác.
Backend chuẩn hóa hai timestamp về precision microsecond của PostgreSQL trước khi lưu, trả response và so sánh.
Việc này giữ kiểm tra concurrent update nhưng tránh conflict giả do .NET có precision tick cao hơn database.

## PATCH giá snapshot

```http
PATCH /api/v1/plm/formulas/{formulaId}/pricing
```

Request chỉ cần gửi các field thay đổi:

```json
{
  "manufacturingCost": 12000,
  "standardSellingPrice": 125000,
  "expectedUpdatedDate": "2026-07-23T10:30:00"
}
```

Mapping lưu trữ:

- `materialCost` -> `Formula.TotalPrice`, chỉ là snapshot đối chiếu.
- `manufacturingCost` -> `Formula.ProductionPrice`.
- `standardSellingPrice` -> `Formula.PresidentPrice`.
- `profitMarginRate` không có cột riêng. Backend dùng tỷ lệ này để tính
  `standardSellingPrice` rồi lưu vào `Formula.PresidentPrice`.

Mỗi giá FE gửi được làm tròn 2 chữ số thập phân, không được âm và phải nằm trong precision `decimal(16,2)`.
`profitMarginRate` được làm tròn 4 chữ số và phải nằm trong `0..100`.
Field không gửi hoặc `null` được hiểu là bỏ qua; endpoint hiện không dùng `null` để xóa giá nullable.
Nếu `expectedUpdatedDate` khác `Formula.UpdatedDate`, API từ chối và FE phải tải lại dữ liệu.
Với dữ liệu legacy có `Formula.UpdatedDate = null`, backend bỏ qua concurrency check cho lần ghi đó và set lại
`UpdatedDate` sau khi lưu thành công; từ lần lưu sau concurrency check chạy bình thường.
Không gửi đồng thời `standardSellingPrice` và `profitMarginRate`.

PATCH xử lý:

- Sửa `standardSellingPrice`: lưu giá vào `Formula.PresidentPrice`, response tự tính lại tỷ lệ.
- Sửa `profitMarginRate`: tính
  `standardSellingPrice = costBase * (1 + profitMarginRate / 100)` rồi lưu vào `Formula.PresidentPrice`.
- Sửa `manufacturingCost`: lưu `Formula.ProductionPrice`; response tự tính lại tỷ lệ.
- Sửa `materialCost`: chỉ ghi snapshot `Formula.TotalPrice`.

PATCH tải lại toàn bộ giá item của Formula trước khi tính. Nếu Formula rỗng hoặc
có ít nhất một item thiếu giá mới nhất:

- PATCH `standardSellingPrice`, `manufacturingCost` và `materialCost` vẫn được phép lưu.
- PATCH `profitMarginRate` bị từ chối vì không có cost base realtime để tính giá bán.
- `manufacturingCost` trong response vẫn dùng giá DB hoặc mặc định theo profile.
- `profitMarginRate` và `pricing` trả `null`; backend không dùng `materialCost`
  snapshot để tạo kết quả tính tạm.

Endpoint yêu cầu policy `PLM.FormulaPricing.Update`, dành cho `Admin`, `Developer` và `President`.
Handler lọc Formula và Product active theo company hiện tại, đồng thời cập nhật `UpdatedDate` và `UpdatedBy`.
PATCH không thay đổi chi phí realtime, NVL, giá nhà cung cấp hoặc báo giá đã snapshot.

## Response giá chuẩn từ backend

`PATCH /api/v1/plm/formulas/{formulaId}/pricing` trả `200 OK` với các giá snapshot vừa lưu và field
`pricing`. FE dùng trực tiếp response này để thay block giá của Formula, không cần gọi lại GET.
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

Rule nằm tại `FormulaPriceCalculator` trong Application và được dùng chung cho PLM/CRM:

1. Profile là `Compound` khi `Product.Additive = C` hoặc mã sản phẩm kết thúc bằng `C`; trường hợp còn lại là `Powder`.
2. Nếu `Formula.ProductionPrice` không có hoặc không lớn hơn 0, chi phí sản xuất mặc định là `20.000`
   cho Compound và `10.000` cho Powder.
3. `costBase = realtimeMaterialCost + manufacturingCost`; không dùng
   `Formula.TotalPrice`.
4. `standardSellingPrice = Formula.PresidentPrice ?? costBase`.
5. `profitMarginRate = (standardSellingPrice - costBase) / costBase * 100`.
6. Suggested price tiers lấy `standardSellingPrice` làm giá nền.
7. Compound áp dụng offset lần lượt `+100.000`, `+50.000`, `+25.000`, `+10.000`, `0`, `-500`, `-1.000`.
8. Powder áp dụng offset lần lượt `+50.000`, `+20.000`, `0`, `-1.000`, `-2.000`, `-3.000`;
   bậc trên 5 tấn cần báo giá thủ công nên trả `unitPrice = null` và `requiresManualPrice = true`.

Giá gợi ý không được âm và được làm tròn 6 chữ số thập phân. Tỷ suất được làm tròn 4 chữ số.
Đây là giá preview; khi lập báo giá, giá được chọn vẫn phải được lưu snapshot vào dòng/bậc giá báo giá.

## Ghi chú riêng cho API chi tiết công thức

`GET /api/v1/plm/formulas/{formulaId}` không trả `materialCost` snapshot. API này trả `realtimeMaterialCost`,
`isRealtimeMaterialCostComplete`, `missingMaterialPriceCount`, `manufacturingCost`, `standardSellingPrice`,
`profitMarginRate` và `pricing`.

Nếu một dòng NVL/sản phẩm không có latest price hoặc latest price là `null`, backend coi giá đó là `0` để tính
`realtimeMaterialCost` và toàn bộ block `pricing`. Dòng thiếu giá vẫn trả `hasLatestPrice = false`,
`latestUnitPrice = 0`, `latestTotalPrice = 0` để FE cảnh báo thiếu giá nguồn. Rule thiếu giá bằng `0` này chỉ áp dụng
cho API chi tiết công thức, không đổi contract của API tra cứu báo giá.

Các dòng `materials[]` của API chi tiết công thức trả thêm `hasLatestPrice`, `latestUnitPrice`, `latestTotalPrice`,
`latestPriceDate`, `latestPriceSource` và `supplierPrices`. User không thuộc `ApplicationRoleSets.PLM.FormulaPriceViewers`
không nhận các giá nhạy cảm; các field tổng giá/pricing trả `null` và `supplierPrices` rỗng.

## Ghi chú riêng cho luồng lưu

Màn hình FE có thể chỉ có một nút `Lưu`, nhưng backend vẫn tách contract lưu thành hai API:

- `PUT /api/v1/plm/formulas/{formulaId}` chỉ lưu thông tin công thức và material. API này không nhận và không ghi đè `manufacturingCost`, `standardSellingPrice`, `profitMarginRate` hoặc `materialCost` ở cấp Formula. Nếu FE gửi `materials`, snapshot NVL `Formula.TotalPrice` được tính lại từ tổng `materials[].quantity * materials[].unitPrice`. Nếu FE gửi `materials: []`, snapshot NVL về `0`. Nếu FE không gửi `materials`, backend giữ nguyên material và snapshot NVL cũ.
- `PATCH /api/v1/plm/formulas/{formulaId}/pricing` chỉ lưu giá sản xuất và giá bán tiêu chuẩn. API này không nhận `materialCost`; snapshot NVL chỉ đổi khi lưu material của công thức.

Vì vậy FE phải tách payload submit theo dirty state dù UI chỉ hiển thị một nút lưu.
