# API danh mục EvaluationStandard

Danh mục chỉ tiêu đánh giá dùng chung với cấu hình mặc định; không phải bộ phiên bản IqcSpecification hay kết quả IqcInspectionResult. Backend lấy company/current employee; không nhận company/audit từ FE. ViewIqc hoặc ViewCoa đọc, ManageIqc hoặc ManageCoa tạo/sửa; Authorize và company scope bắt buộc. Quyền hiện có tương ứng `dev-and-qa.iqc.view` / `.manage`.

Base dùng chung: `/api/v1/dev-and-qa/evaluation-standards`. Alias cũ `/api/v1/dev-and-qa/iqc/evaluation-standards` vẫn hoạt động.

| Method | Route | Response |
| --- | --- | --- |
| GET | base | 200 OperationResult<PagedResult<EvaluationStandardDto>> |
| GET | `/{id}` | 200 OperationResult<Dto>, 404 nếu không thuộc company/không có |
| POST | base | 201 OperationResult<Dto>, Location detail |
| PATCH | `/{id}` | 200 OperationResult<Dto> |

Lỗi quyền/company/validation/record khi mutation trả 400 `{success:false,message,data:null}` theo IQC hiện có. Authentication 401; validation model có thể trả ProblemDetails. Không có hard delete; ngưng dùng qua PATCH isActive=false. Các record đã inactive vẫn giữ mã, không tạo trùng code để tránh đổi nghĩa tham chiếu.

## GET list

```http
GET /api/v1/dev-and-qa/iqc/evaluation-standards?pageNumber=1&pageSize=15&isActive=true&keyword=ẩm
```

pageNumber mặc định 1, pageSize mặc định 15/tối đa 100. isActive bỏ = cả active/inactive; dropdown nhập dùng true. Keyword literal tìm Code/Name bằng ILIKE, %/_ được escape. Sort cố định SortOrder, Code, Id tăng dần; sortBy/sortDirection kế thừa query nhưng không áp trong use case này.

```json
{
  "success": true, "message": null,
  "data": {
    "items": [{"id":"11111111-1111-4111-8111-111111111111","code":"MOISTURE","name":"Độ ẩm","description":"Đánh giá độ ẩm nguyên liệu","isActive":true,"sortOrder":1,"createdDate":"2026-10-06T10:00:00","createdBy":"22222222-2222-4222-8222-222222222222","updatedDate":null,"updatedBy":null}],
    "totalCount":1,"pageNumber":1,"pageSize":15,"totalPages":1,"hasPreviousPage":false,"hasNextPage":false
  }
}
```

Khác summary IQC legacy, danh mục dùng data.pageNumber, không phải data.page.

## POST

```json
{
  "code":"MOISTURE",
  "name":"Độ ẩm",
  "description":"Đánh giá độ ẩm nguyên liệu",
  "isActive":true,
  "sortOrder":1
}
```

Code trim, bắt buộc <=50 ký tự, duy nhất không phân biệt hoa/thường trong company. Name trim, bắt buộc <=200. Description tùy chọn; null/trắng thành null. IsActive mặc định true; SortOrder mặc định 0 và không âm. Id tạo server; CreatedBy/Date là employee/time server.

## PATCH

```json
{"name":"Độ ẩm nguyên liệu","sortOrder":2,"clearFields":["description"]}
```

Field bỏ/null giữ nguyên. code/name rỗng bị từ chối. isActive=false và sortOrder=0 là cập nhật hợp lệ. ClearFields cho description, unit, testMethod, inputConfigJson, acceptanceRuleJson, specificationText, exact camelCase; bỏ mặc định []; null/field khác/conflict value+clear bị từ chối. Description gửi string trống được chuẩn hóa null; ưu tiên clearFields. Id route ghi đè Id body. UpdatedBy/Date lấy server, chỉ cập nhật khi dữ liệu thực sự thay đổi; PATCH không đổi trả thành công và không SaveChanges. Không có token expectedUpdatedDate; lần ghi cuối thành công có hiệu lực.

Ngưng dùng:

```json
{"isActive":false}
```

Sau create/patch refetch list/detail. Ngưng sử dụng danh mục không tự sửa/xóa tiêu chuẩn snapshot hoặc kết quả QC đã liên kết. API này không ghi IqcSpecification/IqcInspectionResult, không tự đổi IsSuccessQuality/ImportWarehouseType hay PO.

## Database và verification

Sử dụng entity/config EvaluationStandard hiện có; persistence interface/DbContext giữ cấu trúc API. Bản mở rộng mặc định có script thêm cột được mô tả ở phần rollout dưới. Deploy API cần DB đã có bảng `devandqa.EvaluationStandard` theo configuration (company FK, unique company/code, employee FK theo model hiện tại). Chưa chạy SQL/DB update hay xác nhận môi trường thực có bảng. Test store kiểm tra orchestration/scope, không thay integration PostgreSQL; concurrent trùng code vẫn được chặn cuối cùng bởi unique constraint, xung đột nhiều process đi qua middleware lỗi hiện hành.


## Cấu hình mặc định của chỉ tiêu (mới)

POST/PATCH và mọi response list/detail/mutation thêm các field:

| Field | Kiểu/default POST | Ý nghĩa |
| --- | --- | --- |
| dataType | int, mặc định 1 | Text=1, Decimal=2, Integer=3, Boolean=4, Date=5, SingleSelect=6, MultiSelect=7 |
| unit | string/null | Đơn vị tối đa 50 ký tự |
| testMethod | string/null | Phương pháp kiểm tra |
| inputConfigJson | string/null | JSON object được serialize thành **chuỗi** trong body API |
| acceptanceRuleJson | string/null | JSON object điều kiện đạt mặc định, serialize thành chuỗi |
| specificationText | string/null | Nội dung tiêu chuẩn hiển thị |
| isRequired | bool, false | Yêu cầu nhập kết quả khi Item sử dụng cấu hình này |

Bool null/bỏ trong PATCH giữ nguyên; false cập nhật đúng. Không clear dataType/bool; các field cấu hình string nullable nằm trong whitelist clearFields. Đổi dataType phải gửi hoặc clear cấu hình cũ không phù hợp trong cùng PATCH; backend validate cấu hình sau merge, không tự xóa config. JSON trống thành null; JSON root phải object, không chứa key trùng, tối đa 16000 ký tự. Không tự tạo kết luận đạt từ isRequired hoặc rule ở API danh mục.

### Ví dụ POST chỉ tiêu độ ẩm

```json
{
  "code": "MOISTURE",
  "name": "Độ ẩm",
  "dataType": 2,
  "unit": "%",
  "testMethod": "Sấy mẫu",
  "inputConfigJson": "{\"minValue\":\"0\",\"maxValue\":\"100\",\"decimalPlaces\":3}",
  "acceptanceRuleJson": "{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}",
  "specificationText": "Độ ẩm không vượt quá 0,2%",
  "isRequired": true,
  "sortOrder": 1
}
```

FE dùng JSON.stringify cho object cấu hình khi gửi, JSON.parse khi đọc field *Json. Giao diện nên tạo form theo kiểu dữ liệu, không buộc người dùng tự viết JSON. Giới hạn nhập 0..100 không phải ngưỡng đạt 0,2; kết quả 0,5 là hợp lệ để nhập nhưng không đạt theo rule.

### Schema InputConfig

- Text: `{ "maxLength": 500 }` (số nguyên dương); config có thể null.
- Decimal: `{ "minValue": "0", "maxValue": "100", "decimalPlaces": 3 }`; numeric bounds nhận string invariant hoặc JSON number, decimalPlaces số nguyên 0..28.
- Integer: `{ "minValue": "0", "maxValue": "100" }`; bounds phải nguyên.
- Date: `{ "minDate": "2026-01-01", "maxDate": "2026-12-31" }`.
- Boolean: null hoặc `{}`.
- SingleSelect/MultiSelect: bắt buộc `{ "options": [{ "code": "PASS", "label": "Đạt" }, { "code": "FAIL", "label": "Không đạt" }] }`; code duy nhất/case-sensitive, code và label không trắng.
- Min không vượt max; key không thuộc schema kiểu dữ liệu bị từ chối.

### Schema AcceptanceRule

Rule null nghĩa chưa cấu hình điều kiện; không tự xem là đạt. Operator case-sensitive:

- Equal/NotEqual với `value` đúng kiểu cho Text/Decimal/Integer/Boolean/Date/SingleSelect.
- LessThan/LessThanOrEqual/GreaterThan/GreaterThanOrEqual chỉ cho Decimal/Integer/Date.
- Between cho Decimal/Integer/Date với `min`, `max`, ý nghĩa inclusive hai đầu.
- In/NotIn cho SingleSelect/MultiSelect với `values` là array code hợp lệ; MultiSelect không dùng Equal.

Số dùng dấu chấm, không dấu phân nhóm/dấu phẩy. Boolean rule dùng JSON true/false. Date yyyy-MM-dd. Select dùng code, không dùng label. Ví dụ cấu hình select: `{ "operator": "In", "values": ["PASS"] }`. Những schema này validate mặc định; engine tính kết luận và nhập kết quả chưa được triển khai trong task này.

## Snapshot và rollout

`EvaluationStandardSnapshotFactory.CreateIqcItem/CreateCoaItem` copy Code/Name và cấu hình sang Item mới cùng specificationId. API danh mục không cập nhật Item cũ, không đổi kết quả hoặc PO. Factory đã có nhưng API tạo bộ tiêu chuẩn chưa được triển khai/nối gọi trong task này; khi triển khai cần validate active/company và specification lifecycle trước gọi.

SQL bổ sung cột: `HRM.Infrastructure/DatabaseContext/Migrations/20261007_AddEvaluationStandardDefaults.sql`, có rollback riêng. **Chưa chạy SQL vào DB**. Script yêu cầu bảng EvaluationStandard hiện có; phải áp dụng trước rollout code API mới. Dòng cũ backfill Text=1, isRequired=false, các cấu hình nullable; không suy/đổi tiêu chuẩn cũ. Rollback xóa cấu hình mới nên cần backup trước dùng.

## Phương pháp đo dùng chung

POST/PATCH nhận measurementMethodId nullable. ID phải là phương pháp đang active thuộc company hiện tại. PATCH bỏ field hoặc null giữ nguyên; clearFields chứa measurementMethodId xóa liên kết. Gửi value và clear cùng field bị từ chối. DTO trả measurementMethodId. Xem [API phương pháp đo, template và kết quả](../Shared/Evaluations/API.md).
