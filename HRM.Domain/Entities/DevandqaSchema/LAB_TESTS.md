> Trạng thái: API cơ bản tạo/xem/sửa/danh sách và lưu tệp đã mở lại; xem
> [BASIC_API.md](../../../HRM.Application/Features/DevAndQA/LabTests/BASIC_API.md).
> BatchNumber và WeightKg đã mở lại cho nhập tay lot/khối lượng; 9 trường mở rộng khác vẫn comment.
> API Excel/grid/import/xóa cũ dưới đây vẫn tạm dừng.
> Không thay đổi DB; API cơ bản dùng các entity/configuration/DbSet hiện có.

# Phiếu test độc lập

`LabTest` là đầu phiếu kiểm nghiệm không cần NVL trong danh mục kho, voucher hoặc `QCInputByQC`.
Mỗi mã như `NVL_TEST_00001` đại diện một phiếu; không phải mã mẫu dùng chung cho nhiều lần test.
CompanyId scope dữ liệu; Code duy nhất trong company. Service cơ bản sinh mã tăng NVL_TEST_00001 trong transaction.
TestDate là ngày kiểm nghiệm; CreatedDate/UpdatedDate là thời điểm audit.
`MaterialId` nullable liên kết Material với FK Restrict; chọn NVL có sẵn không cần nhập kho.
`MaterialCode` là snapshot mã, `SampleName` là snapshot tên khi chọn. Không chọn NVL thì tên nhập tự do.
Mapping mới: `materialid` uuid nullable, `materialcode` citext nullable; chưa tạo migration hoặc đổi DB.
`SupplierId` nullable liên kết `MaterialSchema.Supplier.SupplierId`, navigation `Supplier`;
mapping cột `supplierid`, FK Restrict, không xóa dây chuyền phiếu test khi xóa NCC.
API Basic đã nhận/trả SupplierId và FE có lookup NCC tùy chọn, kiểm tra nguồn active cùng company.
DB cần cột/FK tương ứng;
không tạo/apply migration hoặc thay đổi schema trong lần bổ sung này.

`LabTestResult` lưu mỗi chỉ tiêu một dòng với snapshot mã/tên Việt/Anh, kiểu dữ liệu, đơn vị, phương pháp,
cấu hình nhập và yêu cầu đánh giá. Snapshot luôn phải có mã/tên, datatype 1..8 và sortOrder không âm;
không cần cờ legacy HasConfigurationSnapshot vì đây là dữ liệu mới. Value null nghĩa là chưa nhập scalar;
kiểu Attachment dùng AttachmentCollectionId nullable với FK Restrict để chứa tệp thực tế.

EvaluationStandardId và MeasurementMethodId là nguồn nullable với FK Restrict theo pattern kết quả hiện có.
SourceTemplateItemId chỉ tham khảo, không có FK; cấu hình đọc từ snapshot, không lấy lại từ dòng template.
Đầu phiếu có nguồn template tùy chọn và bản sao Code/Name/Version. Không có nhóm tệp trên template.

Đã có API CRUD/xóa mềm, bảng kết hợp IQC/LabTest và preview/commit import CSV/XLSX tại
[Features/DevAndQA/LabTests](../../../HRM.Application/Features/DevAndQA/LabTests/README.md).
Metadata giữ NCC/KH, batch, mã hàng gốc, resin, application, người test nguồn, khối lượng và tên ảnh tham khảo.
Kết quả chỉ dùng Value, validate theo kiểu chỉ tiêu IQC; không map ObservedText.
IsDeleted loại phiếu khỏi đọc; ImportBatchId/ImportRowNumber chống import lặp ngay cả khi đã xóa mềm.
Luồng cơ bản đã có multipart file-save cho kết quả Attachment, không phải metadata ảnh Excel. Không tạo/apply migration hoặc cập nhật DB;
muốn dùng thực tế cần triển khai schema mới. QC nhập kho hiện tại giữ nguyên.
