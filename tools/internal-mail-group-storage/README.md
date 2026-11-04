# Excel đối chiếu nhóm phối mẫu

`GROUP-STORAGE-TRANSITION: audit-only`. Công cụ SELECT trong transaction RepeatableRead + ReadOnly, timeout ngắn và kết thúc bằng rollback. Không chạy migration, backfill, trigger, gửi tin hoặc mark-read. Không xuất nội dung tin/nhân viên/credential.

Chạy từ root HRM.api, thay tên thư mục để giữ snapshot riêng trước–sau:

```powershell
dotnet run --project tools/internal-mail-group-storage/Audit.csproj -- outputs/group-audit-after-20261101
node tools/internal-mail-group-storage/export.mjs outputs/group-audit-after-20261101
```

Node cần thư viện bundled `@oai/artifact-tool` qua node_modules junction; bản workspace hiện tại đã được cấu hình. Không tải package hoặc nhúng connection string vào script.

Nguồn kết nối là cấu hình BE (appsettings, file environment, Development user secrets, local Project launch profile, environment của tiến trình chạy công cụ). Không xác nhận command-line/environment riêng của API đang chạy trong Visual Studio. Chỉ xuất theo DB đã resolve, không tự nhận đây là production. Source JSON lưu timestamp/scope/config source nhưng không lưu host/tài khoản/connection string.

Snapshot bao gồm mọi hội thoại phối mẫu đã persist và nhóm con cùng company, cả inactive. Đếm toàn bộ message trong nhóm, tách chưa xóa/đã xóa, không lọc theo người đọc. Không xuất quyền, người nhận, badge hoặc nội dung tin. Đây là báo cáo dữ liệu để đối chiếu, không phải xác nhận rằng từng tài khoản xem/gửi đúng.

Excel có hai sheet: một dòng mỗi hồ sơ/mã màu và một dòng mỗi nhóm. Nhóm chưa tạo không có dòng ở chi tiết; tổng tin bằng 0 không chứng minh nhóm đã được tạo. `Nhóm hiện có` và sheet chi tiết phân biệt hai trường hợp. Nhiều nhóm Custom được giữ từng ConversationId, không gộp mất tin.

Mapping khớp khi company/root/type/active khớp nguồn cũ. Bảng chưa tồn tại thì xuất trạng thái chưa chuyển, không tự tạo bảng. Không có cột ngày chuyển trong DB nên báo cáo không tự suy đoán nhóm nào “vừa chuyển”. Chụp trước và sau, so stable IDs và số tin; nếu có người gửi thêm giữa hai lần thì tổng tin có thể tăng hợp lệ.

Mỗi lần chạy nên dùng thư mục output mới; runner từ chối ghi đè source.json. Giới hạn 20.000 dòng nhóm, chạm giới hạn thì từ chối xuất thay vì báo thiếu dữ liệu như kết quả đầy đủ. Sau nghiệm thu có thể bỏ thư mục công cụ này nhưng giữ file Excel/source đối chiếu theo nhu cầu lưu trữ.
