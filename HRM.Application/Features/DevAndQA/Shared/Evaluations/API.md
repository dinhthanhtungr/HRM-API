# API danh mục, template và kết quả đánh giá động

## Phạm vi và quyền

API trong HRM, không liên quan code Backend. Tất cả controller mới có Authorize. Company lấy từ current user; không nhận CompanyId/audit từ FE. Mutation yêu cầu EmployeeId có thật và thuộc company. Record không thuộc công ty được xử lý như không tìm thấy.

- IQC và template Type=1/3: `dev-and-qa.iqc.view` / `dev-and-qa.iqc.manage`.
- COA và template Type=2: `dev-and-qa.coa.view` / `dev-and-qa.coa.manage`.
- Danh mục dùng chung (EvaluationStandard/MeasurementMethod): có một trong hai quyền xem để đọc; một trong hai quyền quản lý để ghi. Quyền dùng chung không cấp quyền đọc/sửa phiếu của chức năng còn lại.
- COA viewer: Admin, Developer, President, ACCUser, QCUser. COA manager: Admin, Developer, President, QCUser. KHOUser/SaleUser không có quyền COA mặc định.
- Role fallback và catalog capability đã cập nhật; baseline seed/rollback hiện có được bổ sung claim COA, chưa chạy SQL. Với account đã dùng DB permissions, admin cần cập nhật role claims và login/refresh trước khi dùng quyền mới; token cũ không tự nhận quyền.

## Response

Thành công: `{ "success": true, "message": null, "data": ... }`. GET/PATCH/actions trả 200; POST tạo danh mục/template/COA/dòng trả 201. POST khởi tạo IQC trả 200 và idempotent.

Lỗi nghiệp vụ mới: `{ "success": false, "message": "..." }`, HTTP 400 validation, 403 thiếu quyền/context, 404 ngoài company/không tồn tại, 409 trùng hoặc sai trạng thái. Lỗi model binding của ASP.NET có thể trả ProblemDetails 400. Không trả EF entity hoặc exception DB.

List mới có `data.items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`. Query `pageNumber=1&pageSize=50` mặc định; pageSize 1–200, offset không vượt int. Sort có ID tie-breaker. Các filter không gửi là không lọc; danh sách rỗng là không có dữ liệu phù hợp.

## Danh mục chỉ tiêu

Canonical `/api/v1/dev-and-qa/evaluation-standards`; alias cũ `/api/v1/dev-and-qa/iqc/evaluation-standards` giữ nguyên. GET list/detail, POST và PATCH giữ contract cũ, bổ sung `measurementMethodId` nullable trên request/response. Các cấu hình mặc định còn lại xem [EvaluationStandard API](../../IQC/EVALUATION_STANDARDS_API.md).

`measurementMethodId` chỉ nhận phương pháp active cùng company. POST không gửi TestMethod thì lấy tên phương pháp. PATCH đổi method mà không gửi TestMethod sẽ lấy tên phương pháp mới. `clearFields: ["measurementMethodId"]` xóa liên kết, giữ mô tả TestMethod; muốn xóa cả mô tả thêm `testMethod` vào clearFields. Không gửi/null giữ nguyên ID. Không vừa gửi ID vừa clear. Không cập nhật template/phiếu cũ theo catalog.

## Phương pháp đo

Base `/api/v1/dev-and-qa/measurement-methods`:

| Method | Route | Nội dung |
|---|---|---|
| GET | base | List: keyword, isActive, pageNumber, pageSize |
| GET | `/{id}` | Chi tiết |
| POST | base | Tạo phương pháp active, audit server |
| PATCH | `/{id}` | Sửa thông tin/IsActive; không hard delete |

POST `{ "code":"DRYING", "name":"Phương pháp sấy", "description":"Sấy đến khối lượng không đổi", "sortOrder":0 }`.
PATCH `{ "name":"Phương pháp sấy mới", "isActive":false }` hoặc `{ "clearFields":["description"] }`. Code/Name 50/200 ký tự, sortOrder>=0; mã duy nhất theo company, kể cả inactive. PATCH bỏ qua/null giữ nguyên; no-op không ghi audit. Description whitespace chuẩn hóa null. ClearFields chỉ cho phép description, không gửi value cùng lúc.

## Template chung

Base `/api/v1/dev-and-qa/evaluation-templates`:

| Method | Route | Nội dung |
|---|---|---|
| GET | base | List: type, status, pageNumber, pageSize; chỉ hiện type có quyền |
| GET | `/{id}` | Chi tiết và Items |
| GET | `/default/{type}` | Template mặc định đã phát hành/có hiệu lực; không có trả 404 |
| POST | base | Tạo phiên bản Draft |
| PATCH | `/{id}` | Chỉ sửa Draft; Published/Retired chỉ Developer hoặc QCUser được sửa |
| POST | `/{id}/publish` | Body `{ "isDefault":true }`; phát hành, có thể thay mẫu mặc định |
| POST | `/{id}/retire` | Ngừng mẫu đã phát hành, bỏ cờ mặc định; giữ phiếu cũ |

Type: IQC=1, COA=2, ProductEvaluation=3. Status: Draft=0, Published=1, Retired=2. Type=3 chỉ có template, chưa có API phiếu đánh giá sản phẩm.

Tạo mẫu:

```json
{
  "code":"IQC_DEFAULT",
  "name":"Mẫu IQC chung",
  "type":1,
  "version":1,
  "items":[
    {
      "evaluationStandardId":"<guid chỉ tiêu>",
      "configuration":{
        "unit":"%",
        "isRequired":true,
        "acceptanceRuleJson":"{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}"
      }
    }
  ]
}
```

Nếu không gửi configuration, sao chép mặc định từ catalog. Tên/mã chỉ tiêu và method code phía server lấy từ danh mục; FE không gán snapshot name/code/ID. Items tối đa 200 và không trùng chỉ tiêu. Draft có thể chưa có dòng nhưng publish yêu cầu ít nhất một dòng, chỉ tiêu/method còn active và cấu hình hợp lệ.

PATCH `items` là thay toàn bộ danh sách của template được phép sửa; bỏ qua/null giữ danh sách cũ, `[]` bỏ hết chỉ tiêu, không xóa template. Mỗi dòng gửi lại được dựng từ catalog + configuration gửi trong request. FE khi chỉnh phải gửi đầy đủ các override cần giữ. Published/Retired: Developer hoặc QCUser có capability quản lý đúng loại được PATCH Name/Items; chỉ Developer được tạo template hoặc đổi Code. Role khác chỉ sửa nội dung Draft. Giữ trạng thái/phiên bản và snapshot phiếu cũ. Unique company/type/code/version. Publish đặt EffectiveFrom và PublishedDate theo server, chưa hỗ trợ lên lịch. Publish/đổi mẫu mặc định trong transaction; retirement không sửa snapshot phiếu.

## Cấu hình trên từng dòng

`configuration` trong request hỗ trợ: dataType, unit, measurementMethodId, testMethod, inputConfigJson, acceptanceRuleJson, specificationText, isRequired, sortOrder, clearFields.

Các JSON là **chuỗi chứa JSON object**, FE dùng JSON.stringify; không gửi object trực tiếp. Schema/enum/điều kiện đạt theo EvaluationStandard API. Clear whitelist: unit, measurementMethodId, testMethod, inputConfigJson, acceptanceRuleJson, specificationText. Null/bỏ qua giữ nguyên; whitespace string chuẩn hóa null. Cờ false và sortOrder=0 là giá trị thật. Đổi DataType phải cập nhật/clear cấu hình không tương thích. Khi clear method ID, mã snapshot cũng được clear, mô tả TestMethod được giữ trừ khi clear riêng.

Response configuration có thêm measurementMethodCode (snapshot), không có ClearFields. Configuration response của phiếu là dữ liệu đã lưu, không phải catalog realtime. IsRequired quy định thiếu dữ liệu; Các chỉ tiêu đã chọn được trả đầy đủ, không có cờ ẩn riêng cho báo cáo hoặc COA.

## IQC

Base `/api/v1/dev-and-qa/iqc/{voucherDetailId}/evaluation`:

| Method | Route | Nội dung |
|---|---|---|
| GET | base | Snapshot đã lưu hoặc preview mẫu IQC mặc định, chưa có đánh giá vẫn trả 200 |
| POST | base | Body `{}` dùng default IQC, hoặc `{ "evaluationTemplateId":"<guid>" }` |
| POST | `/results` | Chọn thêm một chỉ tiêu từ danh mục và có thể nhập Value |
| PATCH | `/results/{resultId}` | Sửa Value/configuration của đúng dòng trên phiếu |

GET kiểm tra voucherDetail thuộc company trước; ngoài company/không tồn tại vẫn trả 404. Có QC và evaluation: trả snapshot với isStored=true. Chưa có QC hoặc chưa có evaluation: trả preview template IQC mặc định đã Published/có hiệu lực, isStored=false, id=null, qcInputByQCId=null nếu chưa lưu QC, results[].id=Guid.Empty, value=null và hasConfigurationSnapshot=false. Không có mẫu mặc định thì results=[], template metadata=null, không chặn mở/lưu QC. GET không ghi DB. FE dùng SourceTemplateItemId/StandardId làm key preview, không dùng ID rỗng làm ResultId và không tự gọi POST khởi tạo khi mở màn hình.

Luồng ưu tiên: GET evaluation để hiển thị, nhập dữ liệu rồi POST/PATCH QC với evaluation tùy chọn bên dưới. Endpoint POST evaluation vẫn giữ để caller chủ động khởi tạo trước, cần QC đã lưu; nếu đã có evaluation thì trả dữ liệu cũ, không reset. POST results cũng cần QC đã lưu nhưng tự tạo đầu evaluation nếu chưa có; không cần template/POST initialize riêng. Endpoint riêng đọc/khởi tạo/chọn thêm dùng QC mới nhất theo CreatedDate/Id.

### Lịch sử QC cùng mặt hàng (API riêng, phân trang)

`GET /api/v1/dev-and-qa/iqc/{voucherDetailId}/evaluation/history?pageNumber=1&pageSize=3`

Quyền ViewIqc, company từ current user. Kiểm tra dòng phiếu hiện tại trong company trước, ngoài company/không tồn tại trả 404; không cần đã lưu QC. Cùng mặt hàng được xác định bằng WarehouseVoucherDetail.ProductCode chính xác, không theo tên/tiền tố hoặc template. Mã mặt hàng rỗng trả trang rỗng, không tìm toàn company.

Chỉ lấy evaluation có ít nhất một dòng result đã lưu, bỏ mọi QC của voucherDetailId đang mở. Nếu phiếu đang mở đã có QC, lịch sử có CreatedDate <= ngày QC mới nhất của phiếu đang mở; nếu chưa có QC thì lấy các phiếu khác hiện có. Sort QCCreatedDate giảm dần, QCInputByQCId rồi EvaluationId giảm dần để phân trang ổn định. Không lọc supplier; cùng mã mặt hàng có thể có nhiều nhà cung cấp. Ngày là ngày tạo QC, không phải ngày PATCH hoặc ngày đo riêng; endpoint không cung cấp lịch sử từng lần chỉnh một phiếu QC.

Mỗi item là một phiếu QC/evaluation, chứa toàn bộ các result của phiếu đó. PageSize mặc định 3, hợp lệ 1–50; PageNumber >=1 và offset không vượt int. Page vượt cuối trả items=[] với totalCount thật; không có lịch sử trả 200/items=[]/totalCount=0. Không gộp các QC khác nhau thành một lô. Phiếu chỉ có field QC cố định hoặc evaluation không có result không được tính là cột lịch sử.

```json
{
  "success": true,
  "data": {
    "items": [{
      "qcInputByQCId": "<qc-id>",
      "voucherDetailId": 32001,
      "qcCreatedDate": "2026-10-03T10:00:00",
      "lotNumber": "BLACK-6250KG",
      "materialExternalId": "MATERIAL-A",
      "qtyKg": 6250,
      "unit": "kg",
      "evaluation": {
        "id": "<evaluation-id>",
        "qcInputByQCId": "<qc-id>",
        "isStored": true,
        "results": [{
          "id": "<result-id>",
          "evaluationStandardId": "<standard-id>",
          "criterionCode": "MOISTURE",
          "criterionName": "Độ ẩm",
          "value": "0.15",
          "hasConfigurationSnapshot": true,
          "configuration": { "unit": "%", "testMethod": "Sấy" }
        }]
      }
    }],
    "totalCount": 8, "pageNumber": 1, "pageSize": 3,
    "totalPages": 3, "hasPreviousPage": false, "hasNextPage": true
  }
}
```

Ví dụ response rút gọn; evaluation giữ đầy đủ contract IqcEvaluationDto/configuration. Giá trị và cấu hình lấy từ snapshot lịch sử, không từ catalog/template hiện tại; legacy fallback kiểm tra nguồn Specification thuộc company. Ngày lấy QC.CreatedDate, LotNumber lấy dòng phiếu kho hiện tại vì QC chưa có cột snapshot mã lô. Mỗi item bổ sung qtyKg (decimal) từ WarehouseVoucherDetail.QtyKg của phiếu lịch sử và unit="kg", đồng nhất API chi tiết IQC. Đây là khối lượng của dòng phiếu kho hiện tại, không phải khối lượng mẫu thử hoặc snapshot tại thời điểm QC; 0 là giá trị nguồn, không thay bằng null. LotNumber=null nghĩa là nguồn chưa nhập, không tự suy ra từ mã khác. Value=null hoặc không có chỉ tiêu tương ứng hiển thị `—`; "0"/"false" là kết quả thật. FE ghép hàng bằng EvaluationStandardId, phải lưu ý unit/dataType/testMethod khác giữa các lần; không tự tính cảnh báo đạt/không đạt. Các trường QC cũ không được suy diễn thành số đo động.

### Lưu QC và kết quả bổ sung cùng transaction

`POST /api/v1/dev-and-qa/iqc` và `PATCH /api/v1/dev-and-qa/iqc/{voucherDetailId}` bổ sung field evaluation. Ví dụ POST:

```json
{
  "voucherDetailId": 32066,
  "note": "QC nhập kết quả bổ sung",
  "evaluation": {
    "evaluationTemplateId": "<template-id từ preview>",
    "results": [{
      "evaluationStandardId": "<standard-id>",
      "sourceTemplateItemId": "<template-item-id từ preview>",
      "value": "0.15"
    }]
  }
}
```

- Không gửi/null evaluation, Results=null hoặc []: lưu QC như cũ, không tạo/xóa/reset đánh giá. Khi chưa có đánh giá, gửi các dòng chưa nhập (mọi Value=null) cũng không tạo đầu/dòng đánh giá.
- Có số đo bổ sung hợp lệ: tạo QC và evaluation/results trong cùng transaction; lỗi dữ liệu bổ sung thì không lưu QC, attachment hoặc đồng bộ PO. Không cần lưu QC trước ở luồng này.
- TemplateId tùy chọn: có ID thì server kiểm tra cùng company/loại IQC/Published/hiệu lực rồi copy toàn bộ dòng; chỉ các Value/config gửi lên được áp dụng. Không có ID thì chỉ thêm các chỉ tiêu được chọn từ danh mục, không tự lấy mẫu mặc định.
- Không nhận mã/tên/cấu hình snapshot nguồn từ FE; server resolve từ template/danh mục. Một phiếu tối đa 200 dòng; không trùng StandardId hoặc CriterionCode.
- Sửa dòng đã lưu: gửi EvaluationStandardId và ResultId của đúng phiếu, Value hoặc ClearValue=true, Configuration tùy chọn. Value=null giữ giá trị cũ; false/0 phải serialize thành chuỗi "false"/"0" theo DataType. ClearValue không được đi cùng Value khác null.
- SourceTemplateItemId nếu gửi phải khớp nguồn dòng. Preview không gửi ResultId. Dòng chọn thêm không gửi SourceTemplateItemId. Không gửi dòng nghĩa là giữ nguyên, không xóa; không đổi template sau khi phiếu có đánh giá.
- POST QC lặp giữ semantics legacy: trả QC đã có, bỏ qua payload mới (kể cả evaluation); dùng PATCH để thêm/sửa trên QC đã tồn tại. PATCH giữ lựa chọn QC đầu tiên của legacy; dữ liệu trùng QC trong một voucherDetail cần xử lý riêng, không tự gộp hoặc chuyển kết quả giữa các QC.
- Mutation trả DTO QC hiện hành; gọi GET evaluation sau lưu để nhận ID dòng/snapshot thật. Không dùng preview sau lưu như dữ liệu đã persist.

Chọn thêm:

```json
{
  "evaluationStandardId":"<guid>",
  "configuration":{"unit":"%","isRequired":false},
  "value":"0.15"
}
```

PATCH `{ "value":"0.18" }`; xóa số đo `{ "clearValue":true }`; sửa yêu cầu riêng `{ "configuration":{ "specificationText":"Độ ẩm ≤ 0,3%", "acceptanceRuleJson":"{\"operator\":\"LessThanOrEqual\",\"value\":\"0.3\"}" } }`.

Không vừa gửi Value khác null vừa clearValue=true. Value null/bỏ qua giữ nguyên; chuỗi rỗng/whitespace không hợp lệ, dùng clearValue để xóa. Kiểm tra Value với cấu hình cuối cùng trước khi sửa entity. Dòng bắt buộc vẫn có thể chưa nhập trong quá trình làm IQC; không tự cập nhật IsSuccessQuality, quyết định nhập kho, PO hay stock.

## COA mới

Base `/api/v1/dev-and-qa/coa-certificates`:

| Method | Route | Nội dung |
|---|---|---|
| GET | base | List keyword theo số COA/mã sản phẩm/lô, pageNumber/pageSize |
| GET | `/{id}` | Chi tiết và tất cả kết quả |
| POST | base | Tạo Draft từ template COA |
| PATCH | `/{id}` | Sửa đầu phiếu Draft |
| POST | `/{id}/results` | Chọn thêm chỉ tiêu; payload như IQC |
| PATCH | `/{id}/results/{resultId}` | Sửa số đo/cấu hình trên Draft |
| POST | `/{id}/issue` | Phát hành khi có dòng và đã nhập mọi dòng bắt buộc |
| POST | `/{id}/cancel` | Hủy, giữ dữ liệu; gọi lại trên Cancelled không ghi thêm |

POST:

```json
{
  "productId":"<guid sản phẩm>",
  "certificateNo":"COA-001",
  "batchNo":"LOT-001",
  "manufacturingDate":"2026-10-07",
  "expiryDate":"2027-10-07",
  "notes":"Ghi chú",
  "evaluationTemplateId":"<guid template COA, có thể bỏ qua>"
}
```

Server xác nhận Product cùng company/active, lấy mã/tên sản phẩm để snapshot, không nhận productCode/productName từ FE. CertificateNo/BatchNo bắt buộc, tối đa 100 ký tự; số COA unique theo company; expiryDate không trước manufacturingDate. ProductId không phải cột mới trên CoaCertificate, chỉ dùng resolve nguồn khi tạo.

PATCH header hỗ trợ certificateNo, batchNo, manufacturingDate, expiryDate, notes; null/bỏ qua giữ nguyên; clearFields chỉ notes/manufacturingDate/expiryDate. Không đổi sản phẩm/template sau tạo. Issued/Cancelled không sửa header/results. Issue kiểm tra cấu hình/Value và đủ trường bắt buộc, ghi IssuedBy/IssuedDate; **không tự kết luận tất cả chỉ tiêu đạt**. Value=null được phép ở dòng không bắt buộc. Status Draft=0, Issued=1, Cancelled=2.

Đây là CoaCertificate mới; không thay thế ProductInspection/COA PDF legacy và chưa có PDF exporter cho các phiếu mới.

## Ý nghĩa dữ liệu và Value

Ví dụ response GET IQC rút gọn:

```json
{
  "success":true,
  "data":{
    "id":"<evaluation-id>",
    "isStored":true,
    "qcInputByQCId":"<qc-id>",
    "evaluationTemplateId":"<template-id>",
    "templateCode":"IQC_DEFAULT",
    "templateName":"Mẫu IQC chung",
    "templateVersion":1,
    "results":[{
      "id":"<result-id>",
      "evaluationStandardId":"<standard-id>",
      "sourceTemplateItemId":"<template-item-id>",
      "externalId":"MOISTURE",
      "criterionCode":"MOISTURE",
      "criterionName":"Độ ẩm",
      "hasConfigurationSnapshot":true,
      "value":"0.15",
      "configuration":{
        "dataType":2,
        "unit":"%",
        "measurementMethodId":null,
        "measurementMethodCode":null,
        "testMethod":"Sấy",
        "inputConfigJson":null,
        "acceptanceRuleJson":"{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}",
        "specificationText":"Độ ẩm ≤ 0,2%",
        "isRequired":true,
        "sortOrder":0
      }
    }]
  }
}
```

- Results.id là ID dòng phiếu dùng PATCH; template Items.id chỉ là ID dòng mẫu, không dùng PATCH kết quả.
- IsStored phân biệt snapshot đã lưu và preview. SourceTemplateItemId=null nghĩa là dòng chọn thêm hoặc legacy, không xác định trạng thái lưu.
- Với IsStored=true, HasConfigurationSnapshot=true nghĩa là cấu hình đã lưu trên Result; false là legacy fallback từ đúng SpecificationItem trong company. Với IsStored=false, toàn bộ dòng là preview chưa lưu, không phải dữ liệu legacy. PATCH hợp lệ nâng dòng legacy thành snapshot; dữ liệu legacy thiếu nguồn/ngoài company bị từ chối.
- TemplateCode/Name/Version là snapshot trên đầu phiếu, có thể null ở dữ liệu cũ dùng Specification. Ngừng template không sửa các giá trị này.
- Value luôn là string: số dùng dấu chấm (chuẩn hóa invariant), Integer không có phần lẻ, Boolean `"true"`/`"false"`, Date `"yyyy-MM-dd"`, SingleSelect là code, MultiSelect là chuỗi JSON array code không trùng. Giới hạn 16000 ký tự. Kiểm tra min/max/decimalPlaces, khoảng ngày, maxLength và options theo cấu hình nhập.
- Miền nhập khác điều kiện đạt: số 0,5 vẫn có thể lưu hợp lệ trong miền 0–100 dù yêu cầu đạt ≤0,2. Chưa có kết luận đạt/không đạt tự động từng dòng.
- Ngày tạo/cập nhật/phát hành là thời gian server theo convention DateTime của repo; ngày sản xuất/hết hạn chỉ là ngày, không múi giờ. UpdatedDate chỉ ghi khi danh mục đổi thật. Entity COA/Result hiện không có cột UpdatedBy/UpdatedDate, không giả lập audit đó trong DTO.

## Triển khai và giới hạn

Không tạo migration mới hoặc chạy SQL trong task API. DB phải có schema entity mới trước khi dùng các endpoint; build/tests không chứng minh schema đã cập nhật. Claim COA cần cập nhật qua cơ chế permission hiện hành nếu account đã migrate.

Các mutation dùng lock theo company và transaction Serializable. Unique constraint là lớp bảo vệ cuối; lỗi SaveChanges xung đột trả 409, lỗi serialization từ transaction có thể cần retry/refetch theo middleware hiện hành. Test orchestration dùng store fake, chưa chạy HTTP/PostgreSQL integration.

## Cập nhật contract 2026-10-08

Đã bỏ hai cờ hiển thị riêng cho báo cáo/COA khỏi danh mục, template, request/response configuration và EF model. FE bỏ hai toggle và không gửi các field cũ. Các dòng đã chọn vẫn được trả đầy đủ theo SortOrder; không có cờ ẩn riêng. Không thay đổi quyền/company scope hoặc quy tắc bắt buộc nhập.

Script SQL lịch sử được giữ nguyên, không tạo hoặc chạy migration trong thay đổi này. Nếu database đã có các cột cũ, chúng không còn được EF ánh xạ; không cần xóa cột để chạy API mới khi các default hiện có được giữ nguyên.

## Tên tiếng Anh

EvaluationStandard và MeasurementMethod: POST/PATCH nhận `nameEn` nullable, tối đa 200 ký tự; GET list/detail trả `nameEn` và tìm kiếm hỗ trợ tên tiếng Anh.
PATCH bỏ qua/null giữ nguyên; chuỗi trắng chuẩn hóa null; `clearFields: ["nameEn"]` xóa tên. Không gửi value đồng thời clear.
Dòng template và kết quả IQC/COA trả `criterionNameEn`, được copy từ danh mục khi tạo dòng và từ template khi khởi tạo phiếu. Lịch sử IQC trả tên từ snapshot; dữ liệu cũ chưa có tên trả null. FE dùng tên tiếng Anh nếu có, nếu không fallback `criterionName`/`name`. Không tự dịch, không tự đồng bộ tên mới lên template/phiếu cũ. Template Published/Retired chỉ Developer hoặc QCUser được chỉnh sửa.

Triển khai: cần cột nullable `nameen` varchar(200) trên EvaluationStandard/MeasurementMethod và `criterionnameen` varchar(200) trên EvaluationTemplateItem/IqcSpecificationItem/CoaSpecificationItem/IqcInspectionResult/CoaCertificateResult. Chưa tạo migration hoặc cập nhật DB; phải đồng bộ schema trước khi chạy phiên bản code này. Không backfill giả tên tiếng Anh.

## Thứ tự tự động

FE không gửi `sortOrder` trên các request danh mục/cấu hình. Backend bỏ qua field cũ nếu client vẫn gửi; response vẫn trả sortOrder để hiển thị.
Tạo EvaluationStandard/MeasurementMethod: lấy max sortOrder của cùng danh mục trong company (kể cả inactive) + 1, bắt đầu từ 1. PATCH giữ số cũ. Tính trong transaction và khóa mutation hiện có; khóa là trong process, chưa bảo đảm số duy nhất khi chạy nhiều instance. Đây là thứ tự hiển thị, không phải mã định danh/sequence DB.
Tạo hoặc PATCH danh sách Items của template Draft: đánh số 1..N theo thứ tự mảng; không cần gửi số từng dòng. Published/Retired chỉ Developer hoặc QCUser được sửa. Template header không có field SortOrder; Version vẫn do caller cung cấp theo contract hiện tại.
Khởi tạo IQC/COA giữ thứ tự template. Chọn thêm chỉ tiêu: nối cuối phiếu bằng max + 1; PATCH giá trị/cấu hình giữ thứ tự. Dữ liệu cũ không được đánh số lại hoặc backfill.

## Liên kết dòng template nguồn

IqcInspectionResult/CoaCertificateResult giữ SourceTemplateItemId như GUID tham khảo, không có navigation hoặc FK tới EvaluationTemplateItem trong EF model. API vẫn trả/kiểm tra ID nguồn theo contract hiện hành; không thay payload. Đọc snapshot không cần dòng template nguồn tồn tại. Nguồn bị xóa thì ID chỉ còn là dấu vết, không chứng minh nguồn còn tồn tại.
Các FK EvaluationStandard, MeasurementMethod, template header và Specification legacy được giữ nguyên. Dữ liệu legacy chưa có snapshot vẫn đọc từ SpecificationItem; thay đổi này không bỏ validation nguồn legacy. Published/Retired cho Developer/QCUser PATCH nội dung theo quyền quản lý/type/company.
DB đang chạy vẫn giữ FK cũ cho tới khi được cập nhật schema: cần bỏ riêng các constraint FK sourcetemplateitemid của IqcInspectionResult và CoaCertificateResult, giữ cột/dữ liệu. Chưa tạo migration hoặc chạy SQL trong task này.

Quyền template: chỉ Developer có capability quản lý đúng loại được tạo hoặc đổi Code. QCUser có capability quản lý đúng loại sửa Name/Items ở Draft/Published/Retired. Type/Version/Status không có trong PATCH; Retired giữ trạng thái, snapshot phiếu đã lưu không đổi. Items null/bỏ qua giữ danh sách, gửi mảng thay toàn bộ, [] bỏ hết; ID dòng giữ theo EvaluationStandardId, không trùng/tối đa 200, thứ tự 1..N theo mảng. Configuration override/clearFields giữ contract hiện có. Publish/retire giữ gate cũ.

Rollout thay đổi này: triển khai lại backend. Không thêm/đổi permission claim; QCUser đã có manage đúng loại và role trong token thì không cần đăng nhập lại. Nếu token thiếu role/capability cần cập nhật claims hiện có rồi login/refresh; không cấp chéo loại hoặc company.
