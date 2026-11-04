# Đồng bộ snapshot kết quả IQC đã lưu

## Phạm vi và quyền

Đây là thao tác chủ động viết lại snapshot lịch sử theo EvaluationStandard hiện tại. GET/PATCH thông thường
vẫn không tự đồng bộ. Không cập nhật template, phiếu COA hoặc LabTest, không thêm/bỏ chỉ tiêu, không sửa Value,
AttachmentCollectionId, ID dòng, thứ tự hoặc thông tin template của đầu đánh giá. Mã/tên Việt/Anh, kiểu dữ liệu,
đơn vị, phương pháp (ID/code/TestMethod), inputConfigJson, acceptanceRuleJson, specificationText và isRequired
được thay bằng catalog, bao gồm null. ExternalId cập nhật theo mã chỉ tiêu nguồn để nhất quán CriterionCode.

Dùng gate tạm thời CatalogSyncManagers: QCUser, Developer, LabUser kiểm tra role trực tiếp,
không yêu cầu claim ManageIqc/ManageCoa, giống API đồng bộ template. Vẫn bắt buộc authenticated,
company hợp lệ và employee thuộc company. Query scope qua voucher của QC cha; không nhận companyId từ FE.
Mã chỉ tiêu chỉ là lookup catalog; ghép result bằng EvaluationStandardId (legacy lấy từ specification item),
không ghép bằng tên/mã snapshot cũ. Nguồn legacy phải hợp lệ trong company; sau sync chuyển sang snapshot.

## Endpoint và payload

Base `/api/v1/dev-and-qa/iqc/evaluation-results`.

POST `/catalog-sync-preview`:
```json
{
  "selection": {
    "scope": "Selected",
    "qcInputByQCIds": ["<qc-guid-1>", "<qc-guid-2>"],
    "codes": ["DENSITY", "MOISTURE"]
  }
}
```

- Current: đúng một QCInputByQCId.
- Selected: 1..1000 QCInputByQCId, không trùng.
- Company: bỏ/null/[] qcInputByQCIds, lấy toàn company hiện tại.
- IDs là GUID phiếu QC, không phải voucherDetailId hoặc evaluationId.
- Chọn codes (1..200 mã, không phân biệt hoa thường, không trùng) hoặc allCriteria=true; không dùng đồng thời.
- allCriteria=true đồng bộ tất cả chỉ tiêu đang có trong các evaluation đã lưu, không thêm dòng từ catalog.
- QC chưa có evaluation không được khởi tạo bởi thao tác này.

POST `/sync-from-catalog`:
```json
{
  "selection": { "scope": "Company", "allCriteria": true },
  "previewToken": "<token nhận từ preview với chính selection này>"
}
```

## Response

Cùng wrapper OperationResult, HTTP 200:
```json
{
  "success": true,
  "data": {
    "previewToken": "<64 ký tự>", "canApply": true, "applied": false,
    "matchedQcCount": 2, "matchedResultCount": 4,
    "changedQcCount": 2, "changedResultCount": 3, "issueCount": 0,
    "changes": [{ "qcInputByQCId": "<guid>", "before": {}, "after": {} }],
    "issues": [], "changesTruncated": false, "issuesTruncated": false
  }
}
```

Before/After là EvaluationResultDto với configuration; Value và AttachmentCollectionId không thay đổi.
matchedQcCount đếm evaluation đã lưu trong scope, gồm phiếu không có mã được chọn; matchedResultCount
đếm result được chọn. Changed counts phản ánh snapshot thực sự khác, không phải số dòng đã đo.
changes/issues giới hạn 200 phần tử mẫu, các counts phản ánh toàn bộ phạm vi; không dùng mảng này làm danh sách apply.
Issues chứa QC ID, ResultId, Code và Message. Guid.Empty ResultId biểu thị lỗi trùng mã ở cấp phiếu.
Preview không Save/Commit; canApply=false nếu có lỗi tương thích. Apply thành công trả applied=true;
không có thay đổi là thành công với changedResultCount=0, không gọi SaveChanges.

Token SHA-256 của scope, dữ liệu nguồn/kết quả và metadata tệp dùng phát hiện thay đổi, không phải token cấp quyền
hoặc phiên bản DB được lưu. Apply dựng lại plan trong transaction Serializable và so token; thay đổi catalog,
result, tệp hoặc selection giữa hai request trả 409, yêu cầu preview lại. Không có write request trước để lưu preview.

## Validation, giới hạn và lỗi

Validate toàn bộ trước mutation. Giá trị đo phải hợp lệ với kiểu/config nhập mới; không normalize hoặc tự xóa Value.
Không tự suy lại kết luận QC, acceptance result, voucher hoặc đơn đặt hàng. Giá trị scalar null vẫn được giữ cho
phiếu đang nhập; Attachment bắt buộc phải có tệp active. Tệp hiện có phải đáp ứng maxFiles/maxFileSizeBytes/
allowedExtensions; chuyển sang scalar khi còn tệp active bị chặn. Không tự chuyển đổi đơn vị hay quy đổi số đo.

Nguồn inactive/missing, phương pháp inactive, legacy sai hoặc trùng mã không được silently bỏ qua.
Missing mã catalog/QC ngoài scope trả 404; lỗi dòng không tương thích xuất hiện trong preview issues;
apply có issue trả 409 và không cập nhật phiếu nào. Không hỗ trợ skip dòng lỗi trong request này.

Tối đa 1000 evaluation và 10000 result/lần. Vượt giới hạn trả 409, không âm thầm cắt dữ liệu; chọn phiếu chia đợt.
400: selection/token sai. 403: role/company/employee không hợp lệ. 409: dữ liệu đã đổi, không tương thích,
xung đột DB hoặc phạm vi quá lớn. Lỗi dùng success=false/message, không trả exception/connection nội bộ.

Apply sở hữu transaction và lock iqc theo company. Toàn bộ thay đổi snapshot rollback khi lỗi; không đụng storage.
Chưa tạo migration, chưa chạy sync trên DB thật, chưa cập nhật F:/Backend. Tests mock không thay thế kiểm tra
PostgreSQL thực tế về translation/serialization conflict; deploy API rồi preview trước khi apply dữ liệu vận hành.
