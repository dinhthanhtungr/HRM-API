> Trạng thái: API LabTest tạm dừng. 11 trường mở rộng trên đầu phiếu và mapping đã comment;
> source API/import được giữ nhưng không compile. Không thay đổi DB. Xem README của feature LabTests.

# Phiếu test độc lập

`LabTest` là đầu phiếu kiểm nghiệm không cần NVL trong danh mục kho, voucher hoặc `QCInputByQC`.
Mỗi mã như `NVL_TEST_00001` đại diện một phiếu; không phải mã mẫu dùng chung cho nhiều lần test.
CompanyId scope dữ liệu; Code duy nhất trong company. Service sinh mã tăng NVL_TEST_00000001 trong transaction.
TestDate là ngày kiểm nghiệm; CreatedDate/UpdatedDate là thời điểm audit.

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
ObservedText giữ khoảng/ghi chú chưa quy thành scalar; Value tiếp tục validate theo kiểu.
IsDeleted loại phiếu khỏi đọc; ImportBatchId/ImportRowNumber chống import lặp ngay cả khi đã xóa mềm.
Chưa có file-save/upload ảnh thực tế cho LabTest. Không tạo/apply migration hoặc cập nhật DB;
muốn dùng thực tế cần triển khai schema mới. QC nhập kho hiện tại giữ nguyên.
