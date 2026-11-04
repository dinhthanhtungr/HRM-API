# Lưu chỉ tiêu Attachment và file cùng một nút Lưu

## Kiểu dữ liệu và cấu hình

`EvaluationDataType.Attachment = 8`, mã 1..7 giữ nguyên. UI chọn Attachment mới hiện upload cho từng chỉ tiêu.
Kiểu Attachment nhận file, không nhận `Value` string. Kiểu khác không được upload hoặc giữ file trên luồng lưu này.
Không thêm kiểu UI/style tách rời với DataType. `IsRequired` của dòng kết quả Attachment nghĩa là cần ít nhất một file active;
danh mục/template/specification chỉ lưu cấu hình kiểu Attachment, không lưu tệp.

Ví dụ inputConfigJson (trường JSON string như contract hiện có):
```json
{"maxFiles":5,"maxFileSizeBytes":5000000,"allowedExtensions":[".jpg",".png",".pdf"]}
```
Giới hạn server: 20 file/chỉ tiêu, 10.000.000 byte/file, tối đa 200 file và 50.000.000 byte file mới/request.
Tên file tối đa 255 ký tự. Không có luồng copy file nguồn.
Cho phép jpg/jpeg/png/webp/gif/pdf/txt/csv/doc/docx/xls/xlsx, kiểm tra cả extension và MIME.
`acceptanceRuleJson` không áp dụng Attachment. Khi đổi kiểu, FE phải clear acceptance rule không còn phù hợp và
clearValue nếu kết quả cũ có Value. Đổi từ Attachment sang kiểu khác phải xóa các file active qua manifest trong cùng lần lưu.

## Endpoint một lần lưu

Base `/api/v1/dev-and-qa`. API JSON hiện có giữ nguyên. Form kết quả có chỉ tiêu Attachment nên dùng multipart-save
kể cả khi không upload thêm để validation file đầy đủ. Danh mục/template dùng API JSON thông thường.

| Method | Route | Payload |
|---|---|---|
| POST | `/iqc/save-with-attachments` | PostQCInputByQC, gồm evaluation.results |
| PATCH | `/iqc/{voucherDetailId}/save-with-attachments` | PatchQCInputByQC, gồm evaluation.results |
| POST | `/coa-certificates/save-with-attachments` | CreateCoaCertificateCommand + results tùy chọn |
| PATCH | `/coa-certificates/{id}/save-with-attachments` | PatchCoaCertificateCommand + results tùy chọn |

COA results dùng `{evaluationStandardId,resultId?,sourceTemplateItemId?,value?,clearValue?,configuration?}`;
dòng đã có được PATCH, dòng chưa có thêm từ danh mục. Null/bỏ results giữ nguyên. Không xóa dòng khi results=[].
POST multipart IQC có QC đã lưu sẽ áp PATCH để lưu cả dữ liệu/file; POST JSON legacy vẫn giữ semantics idempotent cũ.

Request `multipart/form-data` có:
- Part text `payload`: JSON của contract tương ứng; không bọc command IQC trong request.
- Part text `attachments`: JSON mảng ghép file.
- Mỗi file là một part có tên key duy nhất, được liệt kê đúng một lần trong manifest.

Kết quả ghép tệp bằng evaluationStandardId (GUID danh mục), không ghép bằng tên, mã hoặc thứ tự.
Không cần clientKey/resultId tạm. Dùng FormData, append payload và attachments dạng JSON string,
append từng File bằng key duy nhất; để browser tự đặt multipart boundary.

Ví dụ manifest cho nhiều dòng:
```json
[
  {"evaluationStandardId":"<guid-a>","fileKeys":["image_a","pdf_a"],"deleteAttachmentIds":[]},
  {"evaluationStandardId":"<guid-b>","fileKeys":[],"deleteAttachmentIds":["<attachment-id>"]}
]
```
Giá trị đo của dòng Attachment để null; phần dữ liệu scalar còn lại vẫn gửi như form thường.
Không gửi AttachmentCollectionId/StoragePath từ FE. Không cần lưu phiếu hoặc upload trước: một click gọi một request.
File xóa được soft-delete trong cùng transaction, phải thuộc đúng dòng; không hard-delete file vật lý cũ.

## Response và mở lại form

POST trả 201, PATCH trả 200, cùng OperationResult:
```json
{"success":true,"message":null,"data":{
  "record":{},"parentId":"<guid>",
  "attachments":[{"ownerId":"<row-guid>","evaluationStandardId":"<standard-guid>",
    "attachmentCollectionId":"<collection-guid>","files":[
      {"attachmentId":"<file-guid>","fileName":"evidence.pdf","sizeBytes":1234,"slot":19,
       "url":".../content","downloadUrl":".../download","isImage":false}
    ]}]
}}
```
Record là DTO dữ liệu đã lưu, không phải EF entity; result DTO có attachmentCollectionId nullable.
Attachment groups có configuration snapshot (dataType/isRequired/inputConfigJson...) và là nguồn canonical cho collection và file,
không lấy collection ở payload input. Null collection nghĩa là chưa có collection; files=[] nghĩa là không có file active.
ownerId là ID thật của result. CreateDate của file là thời điểm upload, không phải ngày lấy mẫu.

GET `/evaluation-attachments/{resource}/{parentId}` trả OperationResult với danh sách groups.
Resource: Iqc/Coa (giữ mã enum lần lượt 2/3). Các resource cấu hình không được hỗ trợ.
**Iqc parentId là QCInputByQCId trả sau lưu, không phải voucherDetailId**; các loại khác là ID đầu bản ghi.
Mở form mới chỉ chọn file tại máy, không gọi GET/POST để tạo parent hoặc collection tạm.
Content/download dùng URL trả bởi server; backend kiểm tra resource, parent, owner, file và company.
Không trả file bytes/base64 hoặc storage path trong metadata.

## Snapshot và quyền

Danh mục, template và specification chỉ định nghĩa DataType=8, IsRequired và inputConfigJson.
Tệp thực tế chỉ được upload vào kết quả IQC/COA. Không có collection nguồn hoặc tự sao chép tệp.
Snapshot cấu hình kết quả vẫn được bảo toàn như trước.

Giữ [Authorize], company/employee scope và capability đúng loại theo handler/service hiện có.
COA Issued/Cancelled không được sửa file.
Phát hành COA kiểm tra file active cho dòng Attachment bắt buộc. Role-only catalog-sync không mở quyền file-save.
Slot EvaluationAttachment=19 bị chặn upload/list/content/delete/hard-delete ở attachment service chung để không
bỏ qua quyền cha. Luồng file toàn phiếu QC và các slot cũ giữ nguyên.

400: JSON/manifest/configuration/file không hợp lệ; 403: quyền/company/employee hoặc collection bị dùng chung;
404: parent/row/file không thuộc scope; 409: trạng thái/DB conflict; 503: storage I/O thất bại.
Lỗi trả success=false/message. FE giữ nguyên draft và File objects để retry; chỉ báo thành công khi toàn bộ lưu xong.

## Transaction và triển khai

Combined-save sở hữu transaction Serializable. Handler JSON được gọi bên trong chỉ mượn transaction,
không commit/dispose transaction cha. Payload, liên kết collection, metadata, soft-delete và PO sync IQC
cùng rollback nếu file lỗi. File mới có path được đăng ký trước khi ghi, để dọn cả file viết dở khi rollback.
Nếu rollback không xác nhận được hoặc cleanup storage thất bại, ghi cảnh báo vận hành; không hứa storage và DB
là distributed transaction. Mất phản hồi lúc commit có thể cần refetch để xác định trạng thái trước retry.

Không tạo/chạy migration và không sửa DB trong task này. Trước rollout cần hai cột attachmentcollectionid/FK trên IqcInspectionResult và CoaCertificateResult, và sửa hai check constraint snapshot IqcInspectionResult/CoaCertificateResult để chấp nhận datatype 8.
Nếu DB có constraint khác giới hạn datatype/slot, cần cập nhật tương ứng. F:/Backend chưa được sửa trong task này. Nếu DB đã có cột attachmentcollectionid ở bốn bảng cấu hình, code mới không đọc/ghi các cột đó; chưa xóa cột hay file đang lưu trong DB/storage.
