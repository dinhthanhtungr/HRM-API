# Bảng cơ lý NVL — IQC và LabTest

## API cơ bản đã mở lại

Chỉ mở tạo/xem/sửa/danh sách mẫu test tối giản và lưu tệp cùng biểu mẫu.
Xem [BASIC_API.md](BASIC_API.md) để dùng contract đang hoạt động.
API Basic hỗ trợ Material có sẵn hoặc mẫu tự do; lưu FK nullable và snapshot mã/tên, không cần phiếu kho.
Schema bổ sung phải triển khai riêng trước code; chi tiết tại BASIC_API.md, chưa tạo/apply migration.
Controller cũ, grid/import/xóa và 9 trường mở rộng tiếp tục tạm dừng.
BatchNumber/WeightKg đã mở riêng để nhập tay thông tin mẫu; xem BASIC_API.md về schema cần có.
Không bỏ toàn bộ Compile Remove để mở lại chức năng cũ; chỉ các handler/service Basic được include.
Không tạo migration hoặc thay đổi DB trong lần mở này.

## Contract Excel cũ: tạm dừng

Phần API Excel cũ `/api/v1/dev-and-qa/lab-tests` (grid/import/xóa và metadata mở rộng) đã tạm dừng theo yêu cầu.
Controller không compile; handlers/services bị loại khỏi compilation và DI. Sau deploy/restart các route này không còn được đăng ký.
Source và các test workflow/import vẫn giữ để khôi phục sau; tests của phần API tạm dừng không được compile,
model tests của hai entity cơ bản vẫn chạy. Không tác động API IQC hiện có.

9 trường mở rộng còn lại trên LabTest và mapping/index import đã comment. Hai entity cơ bản, configuration và DbSet
vẫn giữ; kết quả dùng Value theo kiểu chỉ tiêu IQC. ObservedText đã bỏ khỏi entity và EF mapping. Không tạo migration, xóa cột/bảng hoặc thay đổi dữ liệu DB.
Phần contract bên dưới là tài liệu chức năng đang tạm dừng, không mô tả API hiện đang phục vụ.
Khi mở lại: đồng bộ schema đã duyệt, khôi phục các field/mapping, bỏ Compile Remove của LabTests trong Application/tests,
bật controller và DI, chạy lại workflow/import tests trước deploy. Frontend tab grid/import cần ẩn khi API tạm dừng.

## Phạm vi

Một bảng đọc kết hợp IQC có kết quả chỉ tiêu đã lưu và phiếu LabTest độc lập. LabTest dùng chung EvaluationStandard, MeasurementMethod và template IQC; mỗi phiếu giữ snapshot riêng. Import tạo LabTest, không tạo hoặc tự ghép/cập nhật QC nhập kho. Không tự suy kết luận QC, quyết định nhập kho hoặc PO từ kết quả test.

Backend có API; frontend đã triển khai ở `F:/UserInterface/hrm-mobile`, tab `/iqc/incoming?tab=mechanical-tests`.
[FE_HANDOFF.md](FE_HANDOFF.md) mô tả bảng tương tự Excel và import. CSV chỉ thể hiện dữ liệu cột, không có style/merge/ảnh nhúng của workbook gốc.

## Quyền và scope

Mọi endpoint có Authorize. Đây là dữ liệu kỹ thuật nội bộ cùng workflow IQC: đọc dùng `dev-and-qa.iqc.view`, tạo/sửa/xóa/import dùng `dev-and-qa.iqc.manage`, qua EvaluationAccessService và CurrentUserPermissionService hiện có. Không thêm permission/role mapping mới. Fallback role hiện hành của IQC áp dụng; LabUser/QCUser có quyền thao tác, SaleUser không có, KHOUser chỉ xem. Token explicit permissions phải có claim tương ứng. Không có quyền thì từ chối endpoint, không trả bản rút gọn.

Company lấy từ current user; mutation/preview import yêu cầu employee thuộc company. Phiếu/template/standard/phương pháp/material/nhân viên đều được scope độc lập. Audit CreatedBy/UpdatedBy do server đặt; PreparedByName là tên người test trong nguồn, không phải identity/audit.

## API

Base: `/api/v1/dev-and-qa/lab-tests`. Response `{success,message,data}`. GET/POST/PATCH/DELETE/import trả DTO, không trả EF entity. POST tạo trả 201, thao tác khác 200. Validation 400, quyền/context 403, không tìm thấy trong company 404, xung đột 409; chưa đăng nhập 401. Model validation có thể trả ProblemDetails.

| Method | Route | Nội dung |
| --- | --- | --- |
| GET | `/` | Chỉ LabTest, gồm phiếu chưa nhập kết quả; loại xóa mềm |
| GET | `/grid` | LabTest và IQC có dòng kết quả đã lưu |
| GET | `/{id}` | Chi tiết LabTest và snapshot |
| POST | `/` | Tạo phiếu, mã do backend sinh |
| PATCH | `/{id}` | Sửa metadata/kết quả, giữ field bị bỏ qua |
| DELETE | `/{id}` | Xóa mềm; gọi lặp không đổi audit |
| POST | `/import/preview` | Multipart đọc CSV/XLSX, không ghi DB |
| POST | `/import` | JSON commit dữ liệu đã review, atomic theo request |

List/grid nhận `pageNumber=1`, `pageSize=30` (1..200), `source` (1=IQC, 2=LabTest), `keyword` (tối đa 200), `fromDate`, `toDate` dạng yyyy-MM-dd. `/` luôn source=2. Keyword tìm mã/tên/batch. Phân trang và sắp xếp ngày giảm dần, source, ID thực hiện tại DB; chỉ tải results của trang được chọn.

Grid trả `data.page` là PagedResult (`items,totalCount,pageNumber,pageSize,...` theo helper repo), `data.columns` là chỉ tiêu có trên trang. Khóa cột `standard:<guid>`; legacy không có standard dùng `legacy:<code>`. Columns cung cấp name/nameEn/unit/datatype/sortOrder để dựng tiêu đề, nhưng cấu hình trong từng cell là canonical của phiếu; cùng standard qua thời gian có thể khác tên/unit/datatype. Sort cột theo sortOrder rồi code/key. FE giữ lựa chọn cột qua các trang, ghép theo key, không dựa vị trí trong mảng.

Mỗi row có source/id/voucherDetailId/testDate/materialCode/itemCode/sampleName/supplierCustomerName/batchNumber/resin/application/preparedByName/note/weightKg/importedPhotoName/canEdit/canDelete/cells. Cell có resultId/standardId/value/observedText/configuration/attachmentCollectionId.

- IQC: ID là QCInputByQCId, ngày lấy CreatedDate QC. materialCode lấy Material.ExternalId khi khớp đúng một material trong company với ProductCode; thiếu/trùng material trả null, không chọn bản đầu tiên. ItemCode giữ ProductCode, tên/lô/khối lượng lấy dòng phiếu kho, NCC/KH từ snapshot CSName, người test từ employee đã scope. Resin/Application chưa có nguồn tương ứng trả null. Chỉ snapshot đã lưu; không đưa preview vào bảng.
- LabTest: materialCode là Code dạng NVL_TEST_00000001, không phải một mã Material; itemCode là mã hàng gốc. Một Code là một phiếu test. TestDate tách biệt thời gian audit. Nullable trống là chưa nhập; 0 là số đo/khối lượng thật.
- Value là giá trị đã normalize theo kiểu/cấu hình; ObservedText là quan sát dạng khoảng/ghi chú chưa quy thành scalar. Hiển thị ObservedText khi có, đồng thời giữ Value nếu cũng có. Không tự suy pass/fail từ text/rule. AttachmentCollectionId chỉ là tham chiếu, không phải URL file công khai.
- CanEdit dựa capability; server vẫn kiểm tra scope/employee khi ghi. IQC canDelete=false; sửa qua API IQC hiện có bằng voucherDetailId. Endpoint DELETE mới không xóa IQC.

## Tạo và sửa

```json
{
  "sampleName": "Mẫu PP",
  "testDate": "2026-10-14",
  "supplierCustomerName": "Nhà cung cấp A",
  "batchNumber": "LOT-01",
  "itemCode": "MA_HANG_EXCEL",
  "resin": "PP",
  "preparedByName": "Người test trong file",
  "weightKg": 12.5,
  "results": [
    {"evaluationStandardId": "<guid chỉ tiêu Density>", "value": "0.9"},
    {"evaluationStandardId": "<guid chỉ tiêu khác>", "observedText": "0.95-0.97"}
  ]
}
```

POST cần sampleName/testDate; template tùy chọn bằng evaluationTemplateId. Khi gửi template phải là IQC Published/có hiệu lực, cùng company và nguồn standard/method đúng company. Server copy đầy đủ items; không gửi template thì chỉ copy các standard được chọn từ danh mục active. Không tự chọn tất cả danh mục hoặc bắt buộc default template.

Snapshot không tự đổi theo danh mục; PATCH không đổi template hoặc cấu hình snapshot. Tối đa 200 chỉ tiêu/phiếu, không trùng standard/code. Có thể thêm chỉ tiêu từ danh mục đang active; chỉ sửa Value/ObservedText của dòng đã lưu thì không yêu cầu danh mục còn active. Value validate bởi EvaluationValueRules hiện có. Attachment không nhận Value/ObservedText; API này không upload file cho LabTest.

PATCH nhận cùng shape POST nhưng mọi field optional. Bỏ field hoặc gửi null là giữ nguyên. Nullable string gửi whitespace được chuẩn hóa về null. `clearFields` chỉ cho supplierCustomerName/batchNumber/itemCode/resin/application/preparedByName/note/weightKg/importedPhotoName; không vừa gửi value vừa clear cùng field. SampleName không được xóa/để trắng, TestDate không nhận MinValue. Results bỏ qua giữ toàn bộ; `results:[]` không xóa kết quả. Mỗi result bỏ Value/ObservedText giữ nguyên; xóa bằng clearValue/clearObservedText=true. Không vừa gửi vừa clear. Khi thay scalar mà muốn bỏ quan sát cũ, gửi clearObservedText=true.

Metadata giới hạn: tên mẫu 200, NCC/KH 300, batch/mã hàng/resin 100, application 2000, preparedByName 200, note/observedText 16000, tên ảnh 255. WeightKg không âm, precision(18,6). ImportedPhotoName chỉ là tên file, không nhận đường dẫn.

Mã sinh tăng trong company, giữ mã đã xóa; transaction Serializable, lock cùng company và unique index ngăn trùng giữa process. Xung đột DB trả theo handler hiện hành, không tự retry mutation/commit nhiều lần. Soft delete giữ kết quả và khóa import; GET/GRID không trả phiếu đã xóa.

## Import

Preview multipart field `file`, optional `columnStandards` là JSON object **tên cột đúng như headers → EvaluationStandardId**. Có thể tự ghép khi tên/code/nameEn trùng chính xác sau normalize; không ghép gần đúng. Dùng API danh mục evaluation-standards hiện có để chọn; danh mục/phương pháp không tự tạo từ Excel.

Hỗ trợ CSV dấu phẩy, multiline quoted cells, UTF-8/BOM; nguồn Windows-1258 được đọc với warning. Chữ đã thành `?` trong source không phục hồi. XLSX đúng một sheet, không công thức hoặc external links. File tối đa 5 MB, 10000 hàng/100 cột, XLSX tối đa 300000 ô, 50 MB sau giải nén/1000 ZIP entries. Header TT/DATE nằm trong 11 hàng đầu.

Giữ thứ tự mẫu đã cung cấp, 25 cột có tên: TT, DATE, NCC/KH, BATCH, mã hàng, tên hàng, RESIN, 13 chỉ tiêu Density → Secant Modulus, Application, PreparedBy, Note, Sample Photo, khối lượng. Có thể thêm **Mã NVL ngay sau TT**. Mã đó chỉ tham khảo khi tạo mới; backend vẫn sinh LabTest.Code. Cột phụ cuối được giữ trong Note kèm warning. Dòng trống/chỉ có STT bị bỏ qua. RowNumber là số bản ghi bảng sau parse CSV multiline (không phải dòng vật lý của file text).

Preview trả importBatchId (từ hash nội dung file), totalDataRows, ignoredRows, headers, warnings, columnStandards (mapping đã resolve) và rows: `{rowNumber,data,errors,warnings}`. Dòng sai ngày/tên/mapping/type/config xuất hiện errors; FE sửa trước commit. Dates hỗ trợ yyyy-MM-dd, d/M/yyyy, d-M-yyyy, các dạng hai chữ số năm và `27 Thg11 23`; năm hai chữ số là 2000+year. Số thuần dấu phẩy được normalize; khoảng/ghi chú ở chỉ tiêu numeric giữ trong ObservedText với warning, không suy số đại diện. Số thuần vi phạm miền nhập vẫn là error. Khối lượng dạng chữ giữ nguyên trong Note.

Sample Photo trong CSV chỉ có đường dẫn tham khảo: chỉ lấy tên file, không đọc đường dẫn hoặc tải file bên ngoài; không có ảnh thật được import. Cần workflow upload LabTest riêng nếu muốn ảnh thực tế. Đường dẫn ảnh/cột phụ được hiển thị bằng textContent trong FE, không render HTML.

Commit JSON:

```json
{
  "importBatchId": "<guid từ preview>",
  "rows": [
    {"rowNumber": 5, "data": {"sampleName":"Mẫu PP","testDate":"2023-11-27","results":[]}}
  ]
}
```

Mỗi commit 1..500 dòng (tổng tối đa 2000 standard khác nhau). Với file lớn chia batch; mỗi request atomic, toàn bộ file không atomic qua nhiều request. Server validate lại tất cả, tải danh mục/phương pháp theo lô để tạo snapshot, lỗi không ghi một phần. Khóa unique company/importBatchId/importRowNumber; retry cùng khóa trả skipped và ID/code cũ, không update/resurrect phiếu. Cùng mã hàng có thể nhiều ngày/lô nên không dedup bằng mã hàng; không tự ghép IQC chỉ bằng mã/lô. BatchId là khóa idempotency client giữ sau preview, không phải token quyền; company/standard vẫn kiểm tra ở commit. Đổi file/hash tạo batch mới, FE cần review trùng nghiệp vụ.

## Schema và rollout

Đã thêm field entity/EF, không tạo/apply migration hoặc SQL. Cần review triển khai LabTest/LabTestResult và metadata mới trước deploy code: xem [SCHEMA_REVIEW.md](SCHEMA_REVIEW.md). Chưa xác minh DB thật; không bật API mới trên schema cũ. Không ghi F:\Backend trong task API này; mô hình Backend đã đồng bộ ở task trước chưa có metadata mới.

Verification: unit orchestration/authorization, parser CSV/XLSX, metadata/import mapping và translation PostgreSQL; test store không thay thế integration DB. Cần smoke test schema thật, concurrent mã/import và payload FE trước rollout.
