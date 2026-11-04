# Kiểm chứng gói đồng bộ

- PASS: patch gồm đúng 6 file thay đổi, kiểm tra và áp dụng trên bản sao nguồn tại HRM.api; nội dung sau áp dụng tương đương các file đóng gói.
- PASS: hash SHA256 của 26 file đóng gói khớp manifest.
- PASS: hash nguồn Backend liên quan vẫn giữ nguyên; các file LabTest mới vẫn chưa tồn tại ở Backend.
- PASS: không còn namespace HRM trong cây files; các dependency dùng namespace VietausWebAPI.
- Đã đối chiếu Backend .NET 9/EF Core 9, AttachmentCollection, EvaluationSnapshotConfiguration và ApplyConfigurationsFromAssembly.
- Chưa build toàn project Backend; không chạy lệnh có side effect trong Backend.
- Test HRM đã thử: `dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~LabTestPersistenceModelTests|FullyQualifiedName~EvaluationStandardPersistenceModelTests" -p:OutDir=../artifacts/verify-labtest-sync/ -v:normal`.
- Test chưa chạy: build trả exit code 1 tại ResolvePackageAssets; log ghi 5 warning NU1902/NU1903 của ImageSharp 3.1.11, 0 error. Chưa xác định nguyên nhân build dừng; không kết luận test pass.
- Chưa tạo/apply migration, SQL hoặc kết nối DB; chưa xác minh schema/data thực tế.
