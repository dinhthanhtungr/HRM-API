# Đã áp dụng ngoại lệ một lần

Ngày 2026-10-14, user cho phép ngoại lệ chỉ cho lần đồng bộ LabTest này vào F:\Backend.

Đã ghi đúng 6 file add/update trong manifest: LabTest, LabTestResult, hai EF configuration, partial DbSet và enum EvaluationDataType bổ sung Attachment=8.

Kiểm tra trước ghi: hash nguồn Backend và gói đúng baseline đã review. Kiểm tra sau ghi: 6 file khớp SHA256 gói, 20 file reference liên quan giữ nguyên SHA256. Không ghi các file reference.

Không thay đổi DB/migration, API/service, chạy build hoặc Git trong Backend. Chưa build toàn Backend; test HRM trước đó dừng tại build như VERIFICATION.md. Ngoại lệ không mở rộng cho thao tác khác trong F:\Backend.
