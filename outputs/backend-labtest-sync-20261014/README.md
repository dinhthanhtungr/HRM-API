# Gói đồng bộ LabTest và tiêu chuẩn HRM.api → Backend

Gói được chuẩn bị tại HRM.api ngày 2026-10-14. Không ghi, build, chạy Git hoặc migration trong F:\Backend. Theo AGENTS.md hiện hành, Backend chỉ được đọc; gói không cấp quyền cho agent áp dụng trực tiếp.

## Nội dung và phạm vi

- `backend-labtest-sync.patch`: chỉ thêm hai entity, hai EF configuration, partial DbSet và bổ sung enum Attachment=8.
- `files/`: 26 file mô hình cùng dependency; namespace đã chuyển sang VietausWebAPI. Các file `reference` giữ nguyên bản Backend hiện tại, không cần ghi đè.
- `COMPARISON.md`: phân biệt file thêm/cập nhật và dependency đã tương đương.
- `manifest.json`: hash nguồn HRM, nguồn Backend và file đóng gói để phát hiện thay đổi trước áp dụng.
- `check-baseline/`: bản sao các file Backend liên quan, dùng kiểm tra patch tại HRM.api.
- `prepare_package.py`: tạo lại gói từ source; chỉ ghi vào thư mục gói này.

LabTest dùng chung EvaluationStandard, MeasurementMethod và template IQC hiện có. Kết quả được sao chép thành snapshot riêng trong LabTestResult; thay đổi danh mục không tự đổi phiếu cũ. Chỉ tiêu Attachment dùng collection riêng từng kết quả. Không tạo danh mục/loại template riêng cho LabTest.

## Áp dụng tại môi trường được phép

1. Review COMPARISON và patch; kiểm tra từng `backendSha256` trong manifest còn khớp nguồn. Khi khác hash, đối chiếu lại thay vì ghi đè.
2. Áp dụng đúng sáu file có trạng thái add/update. Giữ nguyên API IQC hiện tại và các file reference.
3. Build/test toàn Backend tại môi trường được phép. Backend đang dùng .NET 9/EF Core 9, HRM dùng .NET 10; test mô hình HRM không thay thế build/test Backend.
4. Xác nhận model không có shadow FK và LabTestResult có FK Restrict đến phiếu, standard, phương pháp và collection. Configuration được đăng ký qua ApplyConfigurationsFromAssembly hiện có.

## Database và nghiệp vụ còn lại

Chưa tạo SQL/migration hoặc cập nhật DB. Việc dùng entity mới cần schema LabTest/LabTestResult tương ứng, FK, index và check constraint, được review/đồng ý riêng trước triển khai. Các bảng danh mục/template/attachment phải tồn tại trước; source không chứng minh DB đã cập nhật.

Chưa triển khai API/service LabTest, sinh mã NVL_TEST_..., permission, snapshot factory hoặc file-save. Khi triển khai phải scope company cho phiếu/template/chỉ tiêu/phương pháp/file, kiểm tra nhân viên audit và template IQC Published/có hiệu lực; không nhận snapshot từ FE hoặc tự suy ra kết luận IQC/PO từ kết quả LabTest.

Xem `VERIFICATION.md` để biết kiểm tra thực tế đã chạy.
