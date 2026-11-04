# Bàn giao Codex FE — SampleRequests

Phạm vi: trang summary, trang detail, tạo POST và cập nhật PATCH. Đối chiếu controller/DTO/handler hiện tại; không thay đổi endpoint. Base URL: `/api/v1/plm/sample-requests`. Dùng HTTP client/auth/company context hiện có của FE. Tất cả endpoint yêu cầu đăng nhập; BE kiểm tra visibility và company. Không coi việc đọc được record là quyền sửa mọi field.

## 1. Endpoint và response

| Chức năng | Method / route | Response thành công |
| --- | --- | --- |
| Summary | GET `/summary` | 200, `PagedResult<SampleRequestSummaryDto>` trực tiếp |
| Detail | GET `/{sampleRequestId}` | 200, `SampleRequestDetailDto` trực tiếp |
| Tạo | POST base URL | 201, `{ success, message, data: sampleRequestId }` |
| Cập nhật | PATCH `/{sampleRequestId}` | 200, `{ success, message, data: sampleRequestId }` |
| Options form | GET `/form-options` | Dùng options backend hiện có; xem DTO FormOptions |
| History | GET `/{sampleRequestId}/history` | Dùng khi cần tab lịch sử |

Không tự bọc GET thành `{data: ...}`. Phân biệt wrapper của HTTP client (ví dụ Axios `response.data`) và wrapper BE: GET đọc `response.data.items` / `response.data.hero`; POST/PATCH đọc `response.data.success` và `response.data.data`.

Các JSON dưới đây dùng camelCase. UUID mẫu chỉ minh họa; thay bằng id thật từ options/lookup. Date gửi ISO 8601, giữ chính xác timestamp BE trả về khi dùng chống ghi đè. Không gửi enum label tiếng Việt.

## 2. Summary

```http
GET /api/v1/plm/sample-requests/summary?pageNumber=1&pageSize=15&keyword=TP&sortBy=updateddate&sortDirection=desc
```

| Query | Ý nghĩa |
| --- | --- |
| `pageNumber` | Mặc định 1, nhỏ hơn 1 chuẩn hóa về 1 |
| `pageSize` | Mặc định 15, tối đa 100 |
| `keyword` | Mã yêu cầu, tên/mã khách, mã màu/Product/Formula liên quan; BE lọc visibility |
| `sortBy` | `externalid`, `colourcode`, `productname`, `updateddate`, `createdDate` theo sort hiện tại |
| `sortDirection` | `asc` / `desc` |
| `fromDate`, `toDate` | Lọc ngày tạo yêu cầu; ngày cuối được xử lý đến hết ngày |
| `companyId`, `productId` | UUID tùy chọn; companyId không cấp thêm quyền |
| `color`, `additiveCode` | Lọc màu/phụ gia |
| `status` | Query enum: Pending=0, SampleSent=1, InProgress=2, Completed=3, Cancelled=4, New=5, FormulaUpdateRequested=6; có thể dùng tên enum |

Response minh họa rút gọn (item thực tế có thêm field):

```json
{
  "items": [{
    "sampleRequestId": "11111111-1111-4111-8111-111111111111",
    "externalId": "TP-MAU-001",
    "productName": "Hạt màu đen",
    "colourCode": "BLACK-001",
    "customerName": "Khách hàng mẫu",
    "status": "New",
    "attachments": [],
    "selectedFormula": null,
    "formulaSelects": [], "formulaDevs": [], "formulaStandard": []
  }],
  "totalCount": 1, "pageNumber": 1, "pageSize": 15,
  "totalPages": 1, "hasPreviousPage": false, "hasNextPage": false
}
```

Mỗi row là một Sample Request. Navigate bằng `sampleRequestId`, không dùng `productId`. Cột đề xuất: mã yêu cầu, khách, tên sản phẩm/mã màu, category, trạng thái, Sale, Lab, ngày yêu cầu/dự kiến gửi, trạng thái trễ. Phân trang/sort/search ở server; đổi filter thì reset page về 1. Debounce search, hủy request cũ hoặc bỏ response đã lỗi thời.

`sampleRequestCreatedDate/UpdatedDate` thuộc yêu cầu; `productCreatedDate/UpdatedDate` thuộc Product. `labName` hiện lấy người tạo Product, không mặc định diễn giải là người đang xử lý. `isDelayed` là boolean; không suy từ ngày. Màu/phụ gia có value/code và display label. Attachment là metadata, không phải base64; dùng URL/download flow hiện có. Collection rỗng nghĩa là không có dữ liệu được trả; field giá `null` có thể do visibility, không hiển thị thành 0.

### Field summary đầy đủ

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `sampleRequestId` | `Guid` |
| `externalId` | `string?` |
| `productId` | `Guid?` |
| `productName` | `string?` |
| `categoryId` | `Guid?` |
| `categoryExternalId` | `string?` |
| `categoryType` | `string?` |
| `categoryName` | `string?` |
| `colorValue` | `string?` |
| `colorDisplayName` | `string?` |
| `additiveCode` | `string?` |
| `additiveGroupCode` | `string?` |
| `additiveDisplayName` | `string?` |
| `colourCode` | `string?` |
| `status` | `string?` |
| `customerName` | `string?` |
| `customerExternalId` | `string?` |
| `labName` | `string?` |
| `createdBy` | `string?` |
| `endUserName` | `string?` |
| `productCreatedDate` | `DateTime?` |
| `productUpdatedDate` | `DateTime?` |
| `sampleRequestCreatedDate` | `DateTime?` |
| `sampleRequestUpdatedDate` | `DateTime?` |
| `expectedDeliveryDate` | `DateTime?` |
| `requestDeliveryDate` | `DateTime?` |
| `labReceivedDate` | `DateTime?` |
| `isDelayed` | `bool` |
| `labReceivedByEmployeeId` | `Guid?` |
| `labReceivedByName` | `string?` |
| `realDeliveryDate` | `DateTime?` |
| `realPriceQuoteDate` | `DateTime?` |
| `expectedPriceQuoteDate` | `DateTime?` |
| `attachments` | `IReadOnlyList<SampleRequestAttachmentDto>` |
| `selectedFormula` | `SampleRequestSelectedFormulaDto?` |
| `formulaSelects` | `IReadOnlyList<FormulaId>` |
| `formulaDevs` | `IReadOnlyList<FormulaId>` |
| `formulaStandard` | `IReadOnlyList<FormulaId>` |

## 3. Detail

```http
GET /api/v1/plm/sample-requests/11111111-1111-4111-8111-111111111111
```

Trang detail thông thường không cần query thêm. `forSaleOrder`, `customerId`, `orderType` dành cho ngữ cảnh chọn dữ liệu sang Sale Order, không tự bật trên trang detail.

Response minh họa rút gọn:

```json
{
  "hero": {
    "sampleRequestId": "11111111-1111-4111-8111-111111111111",
    "externalId": "TP-MAU-001", "status": "New", "isActive": true,
    "title": "Hạt màu đen", "customerName": "Khách hàng mẫu"
  },
  "quickSummary": {
    "productId": "22222222-2222-4222-8222-222222222222",
    "customerId": "33333333-3333-4333-8333-333333333333",
    "requestType": "External", "expectedQuantity": 1000,
    "expectedPrice": null, "updatedDate": null
  },
  "sampleRequestRequirement": { "package": "Bag", "bagWeight": 25 },
  "technicalRequirement": { "grs": false, "grsConsumerType": null },
  "formulaLookups": [], "attachments": []
}
```

| Nhóm | FE sử dụng |
| --- | --- |
| `hero` | Header: id, mã, trạng thái, tên Product/khách, mã màu, company/branch, ngày tạo |
| `quickSummary` | Id liên kết, số lượng/giá, category, số lượng formula/attachment và `updatedDate` |
| `sampleRequestRequirement` | Card yêu cầu: số lượng, đóng gói, các mốc gửi mẫu/báo giá, nhận Lab/trễ |
| `technicalRequirement` | Card kỹ thuật Product và ghi chú yêu cầu |
| `formulaLookups` | Formula active cùng Product: id, mã, tên, status, totalPrice; giá có thể null theo quyền |
| `attachments` | Metadata attachment; dùng uploader/downloader hiện có |

Một số số liệu xuất hiện nhiều card. Khi tạo form, id lấy từ `quickSummary`, trạng thái/mã màu lấy `hero`, field yêu cầu lấy `sampleRequestRequirement`, field kỹ thuật lấy `technicalRequirement`. `quickSummary.updatedDate` là timestamp gửi lại trong PATCH `expectedUpdatedDate`. Không lấy ngày Product hay ngày row summary thay cho timestamp detail.

### Field của từng card


#### Hero

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `sampleRequestId` | `Guid` |
| `externalId` | `string` |
| `status` | `string` |
| `title` | `string?` |
| `productCode` | `string?` |
| `colourCode` | `string?` |
| `colourName` | `string?` |
| `colourNameLabel` | `string?` |
| `customerExternalId` | `string?` |
| `customerName` | `string?` |
| `managerByName` | `string?` |
| `branchName` | `string?` |
| `companyName` | `string?` |
| `createdDate` | `DateTime` |
| `expectedDeliveryDate` | `DateTime?` |
| `isActive` | `bool` |

#### QuickSummary

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `productId` | `Guid` |
| `customerId` | `Guid` |
| `companyId` | `Guid` |
| `branchId` | `Guid` |
| `managerBy` | `Guid` |
| `selectedDevelopmentFormulaId` | `Guid?` |
| `attachmentCollectionId` | `Guid` |
| `categoryId` | `Guid` |
| `categoryName` | `string?` |
| `requestType` | `string` |
| `expectedQuantity` | `double?` |
| `sampleQuantity` | `double?` |
| `expectedPrice` | `decimal?` |
| `weight` | `double?` |
| `developmentFormulaCount` | `int` |
| `manufacturingFormulaCount` | `int` |
| `productionOrderCount` | `int` |
| `attachmentCount` | `int` |
| `updatedDate` | `DateTime?` |

#### RequirementCard

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `requestType` | `string` |
| `expectedQuantity` | `double?` |
| `expectedPrice` | `decimal?` |
| `sampleQuantity` | `double?` |
| `numberDeliverySampleDate` | `int?` |
| `package` | `string` |
| `bagWeight` | `int` |
| `customerProductCode` | `string?` |
| `requestDeliveryDate` | `DateTime?` |
| `labReceivedDate` | `DateTime?` |
| `isDelayed` | `bool` |
| `labReceivedByEmployeeId` | `Guid?` |
| `labReceivedByName` | `string?` |
| `expectedDeliveryDate` | `DateTime?` |
| `realDeliveryDate` | `DateTime?` |
| `requestTestSampleDate` | `DateTime?` |
| `responseDeliveryDate` | `DateTime?` |
| `expectedPriceQuoteDate` | `DateTime?` |
| `realPriceQuoteDate` | `DateTime?` |

#### TechnicalRequirementCard

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `infoType` | `string?` |
| `saleComment` | `string?` |
| `additionalComment` | `string?` |
| `requirement` | `string?` |
| `additive` | `string?` |
| `additiveLabel` | `string?` |
| `usageRate` | `double?` |
| `deltaE` | `string?` |
| `expiryType` | `string?` |
| `storageCondition` | `bool?` |
| `labComment` | `string?` |
| `procedure` | `string?` |
| `recycleRate` | `double?` |
| `taicalRate` | `double?` |
| `application` | `string?` |
| `productUsage` | `string?` |
| `polymerMatchedIn` | `string?` |
| `endUser` | `string?` |
| `foodSafety` | `bool?` |
| `rohsStandard` | `bool?` |
| `reachStandard` | `bool?` |
| `maxTemp` | `double?` |
| `weatherResistance` | `string?` |
| `lightCondition` | `string?` |
| `visualTest` | `string?` |
| `returnSample` | `bool?` |
| `isRecycle` | `bool` |
| `grs` | `bool` |
| `grsConsumerType` | `GRSConsumerType?` |
| `otherComment` | `string?` |

## 4. POST tạo yêu cầu

Payload là object phẳng. Có hai luồng Product:

- Chọn Product có sẵn: gửi `productId` hợp lệ, category canonical của company; các field tạo Product không phải cơ chế PATCH Product có sẵn.
- Tạo Product mới: bỏ `productId`, gửi `categoryId` từ `/form-options` cùng thông tin Product. Category hợp lệ: CMP/CMB/AMB/ADD/PIG; không hardcode UUID. BE tạo Product và mã cần thiết theo rule.

`customerId` phải active cùng company và có assignment/claim phù hợp. `branchId` lấy từ context/option hợp lệ. `companyId` có trong DTO nhưng BE lấy company của current user khi lưu. Không có `managerBy` trong create; backend tự resolve người quản lý từ assignment/claim.

Ví dụ chọn Product đã có:

```json
{
  "customerId": "33333333-3333-4333-8333-333333333333",
  "productId": "22222222-2222-4222-8222-222222222222",
  "branchId": "44444444-4444-4444-8444-444444444444",
  "requestType": "External",
  "expectedQuantity": 1000,
  "sampleQuantity": 2,
  "package": "Bag",
  "bagWeight": 25,
  "requestDeliveryDate": "2026-10-20T00:00:00",
  "saleComment": "Khách cần đối chiếu mẫu màu",
  "initialLabMessage": "Nhờ Lab phối mẫu theo yêu cầu",
  "initialLabRecipientEmployeeIds": []
}
```

Ví dụ tạo Product mới: thay `productId` bằng `categoryId`, `productName`, `colourName`, `productRequirement` và các field cần thiết; ví dụ `grs: true, grsConsumerType: 0`. Payload mẫu không thay validation theo category/nghiệp vụ của backend.

Backend tạo message/notification đầu tiên cho người nhận bắt buộc, trừ yêu cầu nội bộ. `initialLabMessage` tối đa 2000 ký tự; trống thì BE dựng nội dung. `initialLabRecipientEmployeeIds` thêm người nhận, không loại required recipients. `initialLabSilentWatcherEmployeeIds`: bỏ/null = BE chọn watcher mặc định; `[]` = chủ động không thêm watcher. RequestType nội bộ hoặc khách KH_VIETAUS không tạo thread/notification; ẩn bước chọn người nhận trong trường hợp đó.

Response 201:

```json
{ "success": true, "message": null, "data": "11111111-1111-4111-8111-111111111111" }
```

Sau thành công: navigate detail bằng `data`, GET detail, invalidate summary. Khóa nút khi đang gửi để tránh double submit. Không retry POST tự động khi mất kết nối vì luồng tạo này không có idempotencyKey trong contract.

### Field POST đầy đủ

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `customerId` | `Guid` |
| `productId` | `Guid?` |
| `branchId` | `Guid` |
| `companyId` | `Guid` |
| `formulaId` | `Guid?` |
| `status` | `string` |
| `requestType` | `string` |
| `expectedQuantity` | `double?` |
| `expectedPrice` | `decimal?` |
| `sampleQuantity` | `double?` |
| `numberDeliverySampleDate` | `int?` |
| `package` | `string` |
| `bagWeight` | `int` |
| `customerProductCode` | `string?` |
| `requestDeliveryDate` | `DateTime?` |
| `expectedDeliveryDate` | `DateTime?` |
| `requestTestSampleDate` | `DateTime?` |
| `expectedPriceQuoteDate` | `DateTime?` |
| `infoType` | `string?` |
| `otherComment` | `string?` |
| `saleComment` | `string?` |
| `additionalComment` | `string?` |
| `initialLabMessage` | `string?` |
| `initialLabRecipientEmployeeIds` | `IReadOnlyList<Guid>` |
| `initialLabSilentWatcherEmployeeIds` | `IReadOnlyList<Guid>?` |
| `colourCode` | `string?` |
| `productName` | `string?` |
| `colourName` | `string?` |
| `additive` | `string?` |
| `usageRate` | `double?` |
| `deltaE` | `string?` |
| `productRequirement` | `string?` |
| `expiryType` | `string?` |
| `storageCondition` | `bool?` |
| `application` | `string?` |
| `productUsage` | `string?` |
| `polymerMatchedIn` | `string?` |
| `productCode` | `string?` |
| `endUser` | `string?` |
| `foodSafety` | `bool?` |
| `rohsStandard` | `bool?` |
| `reachStandard` | `bool?` |
| `maxTemp` | `double?` |
| `weatherResistance` | `string?` |
| `lightCondition` | `string?` |
| `visualTest` | `string?` |
| `returnSample` | `bool?` |
| `labComment` | `string?` |
| `procedure` | `string?` |
| `recycleRate` | `double?` |
| `taicalRate` | `double?` |
| `isRecycle` | `bool?` |
| `grs` | `bool?` |
| `grsConsumerType` | `GRSConsumerType?` |
| `categoryId` | `Guid?` |
| `weight` | `double?` |
| `unit` | `string?` |
| `productOtherComment` | `string?` |

## 5. PATCH cập nhật

PATCH gửi object phẳng chỉ gồm field đổi; không gửi lại toàn bộ object detail. Route id là authoritative; không cần `sampleRequestId` trong body.

```http
PATCH /api/v1/plm/sample-requests/11111111-1111-4111-8111-111111111111
```

```json
{
  "expectedUpdatedDate": "2026-10-06T09:30:12.1234567",
  "expectedQuantity": 1200,
  "saleComment": "Khách tăng số lượng",
  "productRequirement": "Đối chiếu màu theo mẫu mới",
  "clearFields": ["sample_request.expected_price", "product.weight"]
}
```

`expectedUpdatedDate` phải copy nguyên giá trị `quickSummary.updatedDate` từ lần GET detail gần nhất; ví dụ timestamp trên chỉ minh họa. Nếu bản ghi đã bị người khác sửa, BE trả 400 với message yêu cầu reload (không phải 409). Giữ draft trên UI, cho người dùng reload/so sánh; không tự ghi đè hoặc retry bỏ timestamp. Nếu detail updatedDate null, không tạo timestamp giả; cơ chế hiện tại chỉ so khi cả hai timestamp có giá trị.

| Giá trị request | Kết quả |
| --- | --- |
| Bỏ field | Giữ nguyên |
| `null` | Không dùng để xóa field nullable |
| Giá trị số/bool/date | Cập nhật; `false` và `0` là value, không phải absent |
| String rỗng/trắng | Contract direct PATCH chuẩn hóa xóa về null đối với field string nullable; ưu tiên clearFields tường minh |
| `clearFields` | Xóa theo fieldCode whitelist và quyền; không dùng camelCase property name |

Không vừa gửi value vừa clear cùng field. Field không nullable như package, bagWeight, isRecycle, categoryId không clear được. GUID liên kết phải hợp lệ, không dùng Guid.Empty để xóa.

### Mapping từ detail sang payload

| Detail | PATCH |
| --- | --- |
| `hero.colourCode` | `colourCode` |
| `hero.colourName` | `colourName` |
| `quickSummary.updatedDate` | `expectedUpdatedDate` |
| `technicalRequirement.requirement` | `productRequirement` |
| `technicalRequirement.otherComment` | `productOtherComment` |
| `technicalRequirement.saleComment/additionalComment` | `saleComment/additionalComment` |
| `technicalRequirement` các field kỹ thuật khác | Field cùng tên ở root payload |
| `sampleRequestRequirement` các field yêu cầu | Field cùng tên ở root payload |

`productName` phải lấy dữ liệu Product phù hợp; `hero.title` là display title, không tự dùng làm giá trị canonical cho form sửa tên nếu chưa kiểm tra mapping. Summary và detail không phải DTO để POST/PATCH nguyên xi.

### Quyền và lifecycle

Không tự cho phép tất cả field chỉ vì DTO có property. Sale/Leader không PATCH hoặc clear trực tiếp foodSafety/rohsStandard/reachStandard; dùng `/data-change-requests` và Lab duyệt. Lab receipt/delay chỉ dành cho Lab, Developer hoặc President theo backend. Các field giá có visibility riêng, null không phải 0 và không được tự gửi lại.

Không dùng dropdown status để tự chuyển mọi trạng thái. SampleSent/Completed/Cancelled liên quan gửi mẫu, chọn công thức hoặc feedback có rule riêng; dùng các action nghiệp vụ backend. Chọn formula phải cùng Product và đúng lifecycle. Đổi mã màu có endpoint `/colour-code` và ràng buộc riêng; xem README mục đổi mã màu trước khi gửi mẫu.

PATCH ghi audit; luồng thông báo direct patch là bước riêng sau PATCH thành công qua `POST /{id}/direct-patch-notifications`. Endpoint này chỉ tạo message/notification, không lưu field; không gửi thông báo trước PATCH hoặc giả lập PATCH bằng notification. Xem command Messaging và README để làm payload changes/idempotencyKey. Lifecycle có side effect riêng do BE thực hiện; FE không tự phát notification chung cho mọi lần save.

Sau PATCH thành công: GET lại detail, lấy timestamp mới, invalidate summary; trả GUID không phải detail đã cập nhật.

### Field PATCH đầy đủ

| JSON field | Kiểu C# (nullable nếu có ?) |
| --- | --- |
| `sampleRequestId` | `Guid` |
| `customerId` | `Guid?` |
| `managerBy` | `Guid?` |
| `productId` | `Guid?` |
| `formulaId` | `Guid?` |
| `expectedUpdatedDate` | `DateTime?` |
| `status` | `string?` |
| `requestType` | `string?` |
| `expectedQuantity` | `double?` |
| `expectedPrice` | `decimal?` |
| `sampleQuantity` | `double?` |
| `numberDeliverySampleDate` | `int?` |
| `package` | `string?` |
| `bagWeight` | `int?` |
| `customerProductCode` | `string?` |
| `requestDeliveryDate` | `DateTime?` |
| `labReceivedDate` | `DateTime?` |
| `isDelayed` | `bool?` |
| `expectedDeliveryDate` | `DateTime?` |
| `realDeliveryDate` | `DateTime?` |
| `requestTestSampleDate` | `DateTime?` |
| `responseDeliveryDate` | `DateTime?` |
| `expectedPriceQuoteDate` | `DateTime?` |
| `realPriceQuoteDate` | `DateTime?` |
| `infoType` | `string?` |
| `otherComment` | `string?` |
| `saleComment` | `string?` |
| `additionalComment` | `string?` |
| `colourCode` | `string?` |
| `productName` | `string?` |
| `colourName` | `string?` |
| `additive` | `string?` |
| `usageRate` | `double?` |
| `deltaE` | `string?` |
| `productRequirement` | `string?` |
| `expiryType` | `string?` |
| `storageCondition` | `bool?` |
| `application` | `string?` |
| `productUsage` | `string?` |
| `polymerMatchedIn` | `string?` |
| `productCode` | `string?` |
| `endUser` | `string?` |
| `foodSafety` | `bool?` |
| `rohsStandard` | `bool?` |
| `reachStandard` | `bool?` |
| `maxTemp` | `double?` |
| `weatherResistance` | `string?` |
| `lightCondition` | `string?` |
| `visualTest` | `string?` |
| `returnSample` | `bool?` |
| `labComment` | `string?` |
| `procedure` | `string?` |
| `recycleRate` | `double?` |
| `taicalRate` | `double?` |
| `isRecycle` | `bool?` |
| `grs` | `bool?` |
| `grsConsumerType` | `GRSConsumerType?` |
| `categoryId` | `Guid?` |
| `weight` | `double?` |
| `unit` | `string?` |
| `productOtherComment` | `string?` |
| `clearFields` | `IReadOnlyList<string>?` |

### Whitelist clearFields


SampleRequestClearFields:

```text
sample_request.expected_quantity
sample_request.expected_price
sample_request.sample_quantity
sample_request.number_delivery_sample_date
sample_request.customer_product_code
sample_request.request_delivery_date
sample_request.lab_received_date
sample_request.expected_delivery_date
sample_request.real_delivery_date
sample_request.request_test_sample_date
sample_request.response_delivery_date
sample_request.expected_price_quote_date
sample_request.real_price_quote_date
sample_request.info_type
sample_request.other_comment
sample_request.sale_comment
sample_request.additional_comment
sample_request.formula_id
```

ProductClearFields:

```text
product.colour_code
product.name
product.colour_name
product.additive
product.usage_rate
product.delta_e
product.requirement
product.expiry_type
product.storage_condition
product.lab_comment
product.procedure
product.recycle_rate
product.taical_rate
product.application
product.product_usage
product.polymer_matched_in
product.code
product.end_user
product.food_safety
product.rohs_standard
product.reach_standard
product.max_temp
product.weather_resistance
product.light_condition
product.visual_test
product.return_sample
product.grs_consumer_type
product.weight
product.unit
product.other_comment
```

GRSConsumerType: 0 = PostConsumer, 1 = PreConsumer; null = chưa xác định/đã clear, không tương đương false. `grs` là boolean riêng. Trạng thái response là chuỗi tên enum. Số lượng/ngày trống hiển thị chưa có; số 0 vẫn là dữ liệu thực.

## 6. Lỗi và acceptance checklist cho FE

- 401: dùng cơ chế hết phiên của app.
- Detail 403: `sample_request_forbidden`; hiển thị không có quyền.
- Detail 404: `sample_request_not_found`; không retry bằng productId.
- Detail 422: `sample_request_invalid_relationship`; hiển thị record liên kết không hợp lệ.
- POST/PATCH 400 nghiệp vụ: `{success:false,message,data}`; hiển thị message. Validation model binding có thể trả ProblemDetails với `errors`, không cố đọc chỉ một format.
- Lỗi mạng/500: giữ form; chỉ retry read an toàn, không tự retry mutation gây side effect.

Codex FE cần thực hiện: summary filter/sort/pagination/loading/empty/error; mở detail theo sampleRequestId; render các card từ response; form tạo hỗ trợ Product có sẵn/mới; form sửa chỉ gửi diff; clearFields whitelist; giữ timestamp chính xác; quyền field theo app hiện có; refetch sau save; giá null không thành 0; không double submit; không thực hiện status transition ngoài action nghiệp vụ.

Nguồn đối chiếu: `HRM.Api/Controllers/PLM/SampleRequestsController.cs`, command Create/Patch trong `Commands/Requests`, DTO trong `Dtos`, và [README nghiệp vụ](README.md), [PATCH contract](Commands/Requests/PatchSampleRequest/README.md). Tài liệu này mô tả contract source, chưa xác nhận response trên môi trường DB chạy thật. Không cần đổi API vì việc sắp xếp folder.
