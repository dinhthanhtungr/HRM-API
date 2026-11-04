# Gói cập nhật Backend từ mô hình HRM

Đã chuẩn bị 32 file entity/configuration/enum/DbSet/SQL, đổi namespace sang VietausWebAPI. Gói đặt trong repo HRM; không ghi/build/Git/migrate tại F:\Backend. Rule chỉ đọc Backend vẫn có hiệu lực; gói này không tự cấp quyền cho agent áp dụng vào Backend.

## Nội dung

- `COMPARISON.md`: chênh lệch từng trường giữa Backend hiện tại và gói cập nhật.
- `files/`: cây thư mục đúng đường dẫn tương đối trong Backend, để review/copy khi được phép.
- `backend-evaluation-sync.patch`: diff cập nhật/thêm file; không xóa file hay migration cũ.
- `manifest.json`: SHA256 nguồn Backend tại lúc đọc và SHA256 từng file trong gói. Nếu nguồn thay đổi, đối chiếu lại trước áp dụng.
- `00_Preflight.sql`: chỉ đọc metadata schema/FK; chưa chạy vào DB.
- SQL triển khai nằm trong `files/VietausWebAPI.Infrastructure/DatabaseContext/DeploymentScripts/`.

## Kiểm tra trước áp dụng

1. Đọc COMPARISON; giữ nguyên code/API nghiệp vụ IQC/ProductInspection hiện có. Gói chỉ đồng bộ mô hình, không tạo chức năng nhập động.
2. Kiểm tra manifest và source hiện tại; patch được kiểm tra trên bản sao các file nguồn trong repo HRM, không áp dụng vào Backend.
3. Sau khi người dùng được phép áp dụng gói vào Backend, build/test Backend tại môi trường phù hợp. Gói chưa được build với toàn project Backend; test/build HRM trước đó không thay thế bước này.
4. Kiểm tra Model EF không tạo FK shadow, đặc biệt CoaSpecification và audit Employee. Không tự drop cột CompanyId1/EmployeeEmployeeId trong DB; đối chiếu dữ liệu trước.

## DB rollout riêng

- Chưa kết nối DB, chưa chạy preflight/DDL.
- Chạy preflight trên DB đích để biết chín bảng cũ/các cột đã tồn tại chưa. Có migration source không chứng minh DB đã được cập nhật.
- Nếu các cột mặc định EvaluationStandard chưa có, chạy `20261007_AddEvaluationStandardDefaults.sql` trước. Nếu đã có, không chạy lại script này.
- Sau khi xác nhận đầy đủ prerequisite, chạy `20261007_AddEvaluationTemplates.sql` một lần. Script thêm ba bảng, cột snapshot, đổi FK nguồn cũ nullable và backfill cấu hình từ Item cũ; không xóa dữ liệu Value.
- Không chạy script tạo lại các bảng cũ. Không chạy cả SQL thủ công lẫn một EF migration tương đương cho cùng thay đổi. Nếu Backend quản lý schema bằng EF migration, tạo/review migration mới trong môi trường được phép, đối chiếu SQL và migration history trước deployment; không sửa migration cũ đã áp dụng.
- Cần xử lý các FK shadow còn tồn tại ở schema cũ theo một migration riêng, sau khi kiểm tra dữ liệu. Các SQL cộng thêm trong gói không sửa lỗi schema lịch sử đó.
- Deploy code mới chỉ sau DB rollout thành công vì EF sẽ query các cột mới.
- Rollback template có chặn nếu đã có dữ liệu mới. Rollback defaults xóa cấu hình mới nên chỉ được dùng khi đã sao lưu và xác nhận không làm mất dữ liệu cần giữ. Nếu đã phát sinh dữ liệu mới, lập phương án bảo toàn/khôi phục backup trước.

## API chưa có

Backend vẫn cần API danh mục phương pháp đo, quản lý template/phiên bản và ghi/đọc Result động. Không copy thẳng Application/DI/controller của HRM vì hai project có cấu trúc khác nhau. Company/capability/audit và snapshot phải được xử lý khi triển khai API.
