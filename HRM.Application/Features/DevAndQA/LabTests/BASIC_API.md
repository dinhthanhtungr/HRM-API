# NVL test cơ bản

Không cần phiếu kho hoặc QCInputByQC; có thể chọn Material có sẵn hoặc nhập mẫu tự do.
Mã phiếu server sinh theo company: NVL_TEST_00001, tách biệt mã Material.
Không tự tạo/bật chỉ tiêu. Tạo mới lấy snapshot từ template IQC Published, có hiệu lực,
được chỉ định hoặc mẫu mặc định; không có mẫu mặc định vẫn có thể chọn chỉ tiêu danh mục đang active.
Phiếu đã lưu giữ nguyên snapshot khi sửa giá trị. Không đồng bộ ngầm với catalog/template.

Base: `/api/v1/dev-and-qa/lab-tests`.

## Chọn NVL có sẵn hoặc nhập mẫu tự do

GET `/lookups/materials?keyword=PP&pageNumber=1&pageSize=20` dùng ViewIqc, company hiện tại,
chỉ đọc Material active; tìm literal theo ExternalId/Name. Nhận selectedId như lookup NCC.
Response `{success,message,data}` chứa PagedResult với items `{id,code,name}`; không trả entity,
giá hoặc thông tin tồn kho. Keyword tối đa 200 ký tự; count/paging thực hiện ở database.

POST/PATCH (JSON và payload multipart) nhận thêm `materialId` tùy chọn, không nhận materialCode.
Khi tạo hoặc chọn ID khác, BE yêu cầu Material active cùng company và tự snapshot ExternalId vào
`materialCode`, Name vào `sampleName`. Tên nguồn phải có nội dung, tối đa 200 ký tự.
Không tin tên gửi kèm để ghi đè tên NVL. Phiếu không có materialId vẫn nhận sampleName nhập tay như trước.

PATCH bỏ/null materialId giữ nguyên; gửi lại cùng ID giữ snapshot, kể cả khi nguồn đổi tên/ngừng dùng.
`clearFields:["materialId"]` bỏ liên kết và mã snapshot, giữ tên mẫu hiện tại nếu không gửi tên mới;
có thể gửi sampleName mới trong cùng request để chuyển sang mẫu tự do. Không vừa gửi materialId vừa clear.
Khi vẫn liên kết NVL, không sửa sampleName thành tên tùy ý. Đổi/bỏ NVL không đổi template, kết quả hoặc tệp.

Ví dụ tạo từ NVL (có thể đặt trong trường payload của multipart):
```json
{"materialId":"UUID", "testDate":"2026-11-17"}
```
Response rút gọn: `{id,code:"NVL_TEST_00001",materialId,materialCode:"NVL_NH_012",sampleName:"Hạt nhựa PP",results:[...]}`.
materialId là FK nguồn nullable; materialCode/sampleName là snapshot đã lưu, không phải metadata realtime.
Mã NVL không có ở nguồn trả null; phiếu nhập tự do có materialId/materialCode=null. Không tự tạo Material,
voucher, ledger hoặc nhập/xuất tồn. Summary giữ externalId=code phiếu; keyword có tìm cả materialCode snapshot.
Đọc/ghi vẫn dùng ViewIqc/ManageIqc, company scope và employee của người thao tác như trước.

Entity/EF cần thêm hai cột nullable `devandqa.LabTest.materialid` (uuid), `materialcode` (citext),
FK MaterialId → Material.MaterialId với Restrict và index theo mapping EF. Phiếu cũ giữ null, không suy
liên kết/backfill từ tên cũ. Chưa tạo migration/SQL hoặc thay đổi DB thực tế; phải đồng bộ schema trước deploy.

## Chọn nhà cung cấp và lookup nhân viên

Form hỗ trợ một dropdown vừa nhập vừa chọn: request/response `supplierId`/`supplierName`.
Chọn NCC: gửi supplierId, BE kiểm tra active/cùng company và lưu tên nguồn thành snapshot,
không tin tên client gửi kèm. PATCH bỏ/null cả hai trường giữ nguyên snapshot, kể cả nguồn đổi tên/inactive.
Nhập tự do: gửi supplierName (trim, tối đa 300 ký tự), supplierId để null/bỏ khi tạo.
Sửa chữ sau khi chọn: gửi `{"supplierName":"NCC tự do","clearFields":["supplierId"]}`.
Không nhận tên nhập tay trên liên kết đang giữ nếu không clear ID. Clear supplierId đơn lẻ xóa cả tên;
xóa tên tự do dùng `clearFields:["supplierName"]`. Không vừa gửi giá trị vừa clear cùng trường.
Không tạo API riêng; áp dụng cả JSON và payload multipart. Company/permission/transaction giữ nguyên.
Entity/EF thêm cột nullable `devandqa.LabTest.suppliername`, tối đa 300 ký tự, không thêm FK.
Chưa tạo/chạy migration hay sửa DB: cần đồng bộ cột này trước deploy. Phiếu cũ chưa có snapshot
được trả tên từ NCC cùng company nếu còn liên kết; không tự backfill. Không bật SupplierCustomerName/import cũ.

GET `/lookups/suppliers` và `/lookups/lab-employees` dùng ViewIqc + company scope.
Nhận keyword (tối đa 200), pageNumber/pageSize, selectedId tùy chọn; trả wrapper chứa PagedResult
với item `{id,code,name}`. Không trả email, số điện thoại, thông tin đăng nhập hay security claims.
Supplier lookup chỉ active cùng company. Nhân viên lookup: employee active/Status=Active, có tài khoản
không bị khóa hiện hành và assignment role LabUser active. Trùng tài khoản không nhân bản employee.
Request POST/PATCH nhận `createdBy` là employeeId được chọn. ID gửi tường minh phải khớp cùng
điều kiện LabUser của lookup và thuộc company; không nhận Guid.Empty hay clearFields=["createdBy"].
FE mặc định chọn tài khoản hiện tại khi đủ điều kiện, nếu không thì yêu cầu chọn LabUser hợp lệ.
Khi sửa, giữ người tạo hiện có; chỉ gửi createdBy nếu người dùng đổi. Nguồn ngừng hoạt động không
chặn lưu nếu không yêu cầu đổi người tạo. Response thêm `createdByName`, lấy tên employee cùng company.
API legacy bỏ/null createdBy: POST giữ mặc định employee của người gọi, PATCH giữ người tạo cũ.
Gửi createdBy không bỏ qua ManageIqc/company scope hoặc xác minh employee của người đang đăng nhập.
CreatedDate luôn là timestamp do server tạo, không đổi khi sửa CreatedBy. UpdatedBy/UpdatedDate của
PATCH vẫn ghi người đang thao tác thực tế. Không thêm cột người thực hiện hoặc migration.

Phần nhập chỉ tiêu FE dùng chung `IqcCriterionCell`/`IqcResultInputCell`, ResultValue,
EvaluationAttachmentField và style bảng IqcEvaluationPanel. Không dựng cấu hình riêng hoặc đổi template.
Không gọi history/export IQC bằng voucher giả cho mẫu độc lập.

| Method | Route | Chức năng |
| --- | --- | --- |
| GET | `?pageNumber=1&pageSize=15` | PagedResult mẫu test trong company |
| GET | `/{id}` | Đầu phiếu và toàn bộ snapshot kết quả |
| POST | `/` | Tạo bằng JSON, không upload tệp |
| PATCH | `/{id}` | Sửa metadata/giá trị bằng JSON |
| POST | `/save-with-attachments` | Tạo và lưu tệp trong một request |
| PATCH | `/{id}/save-with-attachments` | Sửa và lưu/xóa tệp trong một request |

FE form luôn dùng multipart-save, kể cả không có file, để kiểm tra đầy đủ IsRequired của Attachment.
Multipart gồm `payload` JSON string, `attachments` manifest JSON và các file theo key.
Manifest/giới hạn/rollback theo [attachment save](../Shared/Evaluations/ATTACHMENT_SAVE_API.md).
Resource đọc file là `Lab` (enum 6); slot là EvaluationAttachment (19).

Ví dụ payload tạo:
```json
{
  "sampleName": "Mẫu PP khách gửi",
  "testDate": "2026-11-14",
  "note": "Ghi chú mẫu",
  "evaluationTemplateId": "UUID",
  "results": [{ "evaluationStandardId": "UUID", "value": "0.97" }]
}
```

Tên mẫu bắt buộc khi nhập tự do, tối đa 200 ký tự; chọn Material thì BE lấy tên nguồn. Ngày test bắt buộc; note tối đa 16000.
Đầu phiếu hỗ trợ `batchNumber` nullable (trim, tối đa 100 ký tự), `weightKg` nullable
(không âm, numeric(18,6)). Người dùng tự nhập, không lấy hoặc tạo lô/tồn kho.
DTO detail/list/multipart record trả hai field này. PATCH bỏ/null giữ; clearFields cho phép
note/batchNumber/weightKg; không vừa gửi giá trị vừa clear cùng field. Khối lượng 0 là giá trị thật.
Hai property/mapping tương ứng được mở lại, các trường mở rộng khác và import vẫn tạm dừng.
DB cần có cột `devandqa.LabTest.batchnumber` nullable và `weightkg` nullable numeric(18,6).
Chưa kiểm tra/đổi schema thực tế, không tạo/apply migration hoặc SQL trong lần sửa này.
Tối đa 200 chỉ tiêu duy nhất. Chỉ backend quyết định tên/configuration snapshot.
Kết quả dùng chỉ tiêu IQC và Value theo cấu hình kiểu dữ liệu. Entity/EF không có ObservedText, không yêu cầu cột observedtext hoặc migration. Không nhận ObservedText/import trong API cơ bản. Không thay template nguồn sau khi tạo.
Không gửi results khi tạo: lấy toàn bộ dòng template; results=[]: không thêm dòng.
PATCH null/bỏ results giữ nguyên; [] không thay đổi; gửi dòng mới thì thêm từ catalog active.
PATCH không bỏ dòng cũ; value bỏ/null giữ giá trị, clearValue=true xóa scalar.
Metadata bỏ/null giữ nguyên; clearFields=["note"] xóa note, không vừa gửi note vừa clear.

Response JSON: `{success,message,data}`; data là BasicLabTestDto gồm id/code/sampleName/testDate/note,
evaluationTemplateId/templateCode/templateName/templateVersion, createdDate/createdBy và results.
Mỗi result theo EvaluationResultDto (snapshot, value, attachmentCollectionId).
Multipart trả data `{record,parentId,attachments}` theo contract dùng chung.
GET list data là PagedResult. POST tạo 201, các thao tác khác 200.
401 chưa đăng nhập, 403 thiếu quyền/company/employee, 404 nguồn/phiếu ngoài company hoặc không tồn tại,
400 validation, 409 xung đột. Không trả EF entity.

Đọc dùng ViewIqc, ghi dùng ManageIqc qua EvaluationAccessService; giữ permission-model/role mapping hiện có.
Mọi phiếu, nguồn, phương pháp và collection đều scope theo company của current user.
Không có DELETE/import/grid trong phần mở lại. Không thêm migration; cần schema LabTest/LabTestResult
hiện hành đã được triển khai trước khi chạy. Build/test mock không chứng minh schema thực tế đã đủ.

Code: DTO tại Dtos/BasicLabTestDto.cs; Commands/Queries Basic chỉ điều phối;
Services/Basic/BasicLabTestService.cs xử lý nghiệp vụ và BasicLabTestMapper.cs dựng snapshot/response;
BasicLabTestsController chỉ khai báo routes. Luồng tệp tái sử dụng Shared/Evaluations/Attachments.
