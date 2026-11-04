# Manufacturing BE verification

Ngày kiểm tra: 2026-10-10. Nhánh giữ nguyên `codex/fix-activity-board-performance`.
FE mới tại hrm-mobile đã nối các luồng tạo MFG và sửa information sang BE mới.
FE đã nối màn RW xử lý lệnh New qua workspace/formula-sources/use-stock/formula.
Các màn hình Lab/adjustment/color-chip chưa nằm trong phạm vi FE đã nối.

## Kết quả thực tế

- Build `HRM.Api/HRM.Api.csproj --no-restore -m:1` thành công, 0 lỗi.
- Có 10 warning từ package ImageSharp
  đang có trong workspace; không có warning từ code Manufacturing mới.
- Chạy nhóm Manufacturing, ProductionOrders và Commons.Authorization:
  297 passed, 0 failed, 0 skipped (gồm 10 tests inform-basic mới).
- Kết quả VSTest: `artifacts/manufacturing-verification/manufacturing-fe-integration.trx`.

Các kiểm thử bao gồm company/permission/field visibility, nguồn công thức,
giá và lot do BE resolve, snapshot, trạng thái/action, reservation, copy,
Lab/Process, adjustment, color chip, đọc lịch sử, PDF và Excel. Các truy vấn
quan trọng được kiểm tra khả năng dịch SQL với Npgsql mà không kết nối database.
Persistence giả kiểm tra điều phối, thứ tự ghi và nhánh rollback; kết quả này
không chứng minh transaction/lock hoạt động đúng trên PostgreSQL thực tế.

## Contract và quyết định đã giữ

- Client mới gửi ID, số lượng/tỷ lệ, lựa chọn, ngày và ghi chú theo
  [FE migration contract](FE-MIGRATION.md). BE kiểm tra trạng thái hợp lệ,
  lấy snapshot/giá/quyền và đồng bộ reservation theo từng workflow.
- API legacy tương thích vẫn tồn tại; không khẳng định mọi endpoint cũ đã
  bị buộc sang payload cơ bản.
- Copy giữ CustomerId null, lượng yêu cầu và giá thỏa thuận bằng 0.
  RW dùng tồn/lưu VA giữ hành vi đổi CreatedDate đã được xác nhận.
- PLPU không tự được cấp quyền xem thành phần; cần claim riêng
  `plm.formula-material.view`. Quyền xem giá không được cấp kèm.
- Thông báo qua PublishAsync/outbox cùng transaction: cảnh báo giá tới
  PLPUNotify/President/Developer, ngày dự kiến tới người tạo đơn bán,
  adjustment tới hai nhóm legacy. Recipient phải active và cùng company;
  cảnh báo giá không gửi giá/cost trong payload.

## Trước khi đưa vào production

1. Kiểm thử tích hợp với database PostgreSQL có schema legacy hiện hành:
   mapping, ghi thực tế, rollback, unique constraints và concurrent writers.
   Lock mới chỉ phối hợp những writer dùng cùng giao thức khóa.
2. Chạy đối chiếu dữ liệu nghiệp vụ thực tế với legacy, gồm công thức lịch sử
   thiếu ProductId/itemType, chuyển trạng thái, schedule/reservation và outbox.
3. Đối chiếu trực quan PDF với mẫu legacy và xác nhận nội dung xuất file.
   Native PDF/Excel đã chạy trong tests nhưng chưa chứng minh parity hình ảnh.
4. Chạy BE mới và kiểm thử toàn luồng FE đã nối trong môi trường test;
   tiếp tục nối các màn hình Lab/adjustment/color-chip khi triển khai phạm vi đó.

Không thực thi migration/DDL, seed quyền hay cập nhật database thật trong
đợt này. Thay đổi đang nằm trong working tree có nhiều phần việc khác;
chưa commit/push để tránh đưa các thay đổi chồng chéo chưa tách phạm vi vào Git.
