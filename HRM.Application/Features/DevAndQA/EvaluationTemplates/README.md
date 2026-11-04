# EvaluationTemplates

Commands và Queries chia theo use case; handler gọi service riêng tại Services của feature.

Xem [endpoint, payload và quyền](../Shared/Evaluations/API.md). Tất cả dữ liệu scope theo company. Không chạy migration hoặc SQL trong task này.

## Tổ chức và cách đọc code

Commands/Queries gọi Services/EvaluationTemplateService.cs.
DTO template và payload dòng nằm trong Dtos của feature này.

Đọc theo luồng: danh sách/chi tiết → chọn mẫu đã Published/đúng Type/có hiệu lực → tạo/sửa Draft → publish/retire.
SelectTemplateAsync là lookup nội bộ dùng cho IQC và COA, không tự Save/Commit.
Cấu hình/snapshot/value rules dùng lại từ Shared; nghiệp vụ template không nằm trong Shared.

## Sửa template đã phát hành

Developer hoặc QCUser có quyền quản lý đúng Type được PATCH Name/Items ở Draft/Published/Retired; chỉ Developer được tạo template hoặc đổi Code. Role khác chỉ sửa nội dung Draft; LabUser vẫn giữ giới hạn publish/retire hiện có. Company và employee vẫn phải hợp lệ. Items thay toàn bộ danh sách: null giữ nguyên, [] bỏ hết; backend đánh số theo thứ tự mảng. Dòng còn lại giữ ID, dòng bị bỏ được xóa, dòng mới được thêm. Snapshot phiếu cũ không đổi; ID nguồn bị xóa chỉ là dấu vết tham khảo. Trạng thái/phiên bản không đổi; Published áp dụng cho lần tạo mới, Retired vẫn không được chọn. Không xóa cả template header hoặc phiếu. DB phải bỏ hai FK SourceTemplateItemId trước khi dùng.

## Đồng bộ snapshot từ EvaluationStandard

POST `/api/v1/dev-and-qa/evaluation-templates/{id}/sync-from-catalog`
```json
{ "codes": ["COA_TENSILE_STRENGTH", "COA_DENSITY"] }
```
Codes là mã EvaluationStandard.Code hiện tại (không phải ID hoặc mã snapshot cũ), trim và không phân biệt hoa/thường.
Nhận 1..200 mã duy nhất; không có chế độ ngầm đồng bộ tất cả. Muốn đồng bộ một dòng truyền một mã.
Backend resolve nguồn đúng company, ghép dòng mẫu bằng EvaluationStandardId, không ghép bằng tên.
Chỉ đồng bộ chỉ tiêu đã có trong mẫu, thay mã/tên Việt/tên Anh và toàn bộ configuration, kể cả null/false;
TestMethod lấy đúng trường catalog, MeasurementMethodCode đọc phương pháp liên kết. Giữ ID dòng, thứ tự,
Type/Version/Status và các dòng không được chọn. Không sửa snapshot IQC/COA đã lưu.

POST `/{id}/catalog-sync-preview` nhận cùng codes, thêm items tùy chọn để xem trước draft FE.
Response OperationResult có data.templateId và data.changes; mỗi dòng gồm code/evaluationStandardId/before/after,
before/after là item DTO có tên và configuration. Preview không Save hoặc thay entity đang lưu.
FE nên xác nhận rõ việc mất override trước khi lưu; API sync-from-catalog ghi server ngay, dùng cho form đã lưu.
Nếu còn chỉnh sửa chưa lưu, dùng PATCH hiện có với field syncStandardCodes:
```json
{ "items": [{ "evaluationStandardId": "<guid>", "configuration": { "unit": "%" } }], "syncStandardCodes": ["COA_DENSITY"] }
```
Danh sách items là danh sách cuối cùng; sync được áp sau configuration request nên catalog thắng đối với dòng được chọn.
Không gửi items/null giữ danh sách và thứ tự. Items=[] bỏ hết và không thể sync mã không còn trong mẫu.
Lưu thường không có syncStandardCodes giữ mã/tên snapshot và merge trên configuration hiện có của dòng cũ,
không lookup lại nguồn active. Dòng mới vẫn lấy catalog active; thứ tự mảng items được đánh 1..N.

Response sync/PATCH là 200 OperationResult<EvaluationTemplateDto>, success=true/data là mẫu đã lưu.
400: danh sách rỗng/null trên endpoint sync, mã trùng/sai định dạng, cấu hình sai hoặc phương pháp không hợp lệ.
404: mẫu/nguồn không tồn tại trong company. 409: nguồn inactive, chỉ tiêu không nằm trong mẫu cuối cùng,
mã sau sync trùng hoặc trạng thái không được sửa. 403: thiếu capability/company/employee.
Quyền dùng gate PATCH hiện tại theo đúng Type, bao gồm ngoại lệ non-Draft hiện có; không cấp thêm role.
Request nhiều mã được validate trước khi apply và lưu cùng transaction; một mã lỗi thì không lưu mã nào.
Preview chưa có optimistic concurrency token: lưu đọc catalog mới nhất tại thời điểm request, không cam kết
catalog giống preview nếu có người sửa ở giữa. FE refetch sau lưu; không tự tạo migration trong feature này.
### Ngoại lệ phân quyền đồng bộ tạm thời

Preview và sync-from-catalog kiểm tra trực tiếp role QCUser hoặc Developer; không yêu cầu
ManageIqc/ManageCoa kể cả token permission-model=1. Các role khác không được sync dù có manage claim.
Vẫn yêu cầu authenticated, company, employee cùng company và scope template/catalog; áp dụng cả IQC/COA
ở Draft/Published/Retired. Không cho truy cập company khác.
PATCH chỉ có syncStandardCodes (không Code/Name/Items) cũng dùng ngoại lệ này.
PATCH có Code/Name/Items vẫn yêu cầu capability hiện hành để sửa các nội dung đó, đồng thời role QCUser/Developer
để sync. Không mở quyền tạo mẫu, đổi mã mẫu, publish/retire hoặc API khác. FE nút đồng bộ thuần gate bằng role;
nếu cần lưu draft chung, giữ gate quản lý của thao tác PATCH đầy đủ.
LabUser cũng được preview/đồng bộ theo role, không cần manage claims, nhưng chỉ ở Draft; Published/Retired vẫn bị chặn. Company/employee và quyền sửa nội dung khác giữ nguyên.


### LabUser tương đương QCUser trong DevAndQA

LabUser có quyền DevAndQA tương đương QCUser: View/Manage IQC và COA; sửa/đồng bộ nội dung mẫu Draft/Published/Retired; publish/retire theo capability như QCUser. Chỉ Developer vẫn được tạo/đổi mã template. Ngoại lệ role-only đồng bộ áp dụng cả LabUser không có manage claim; API khác vẫn kiểm tra claims hiện hành. Seed đã bổ sung LabUser dev-and-qa.coa.manage nhưng chưa chạy DB; token permission-model=1 cần cập nhật claim và login/refresh cho API COA thông thường. Giữ company/employee scope.

Khi PATCH thêm chỉ tiêu chưa có trong mẫu, backend gọi Add tường minh trên EvaluationTemplateItems để EF đánh dấu Added dù GUID đã được factory gán. Dòng cũ giữ ID và không Add lại; không cần thay schema hoặc migration.


## Attachment theo chỉ tiêu

[Lưu mẫu và file chỉ tiêu trong một request](../Shared/Evaluations/ATTACHMENT_SAVE_API.md)
