# Chuyển nguồn đọc nhóm sang bảng riêng

**Quy trình hiện hành: [API_SYNC.md](API_SYNC.md).** Migration chỉ tạo cấu trúc bảng; API đồng bộ trả Excel; write-through bằng code giữ nhóm mới được lưu atomic, không cần trigger.

Thứ tự: giữ cờ đọc false → tự chạy schema-only migration → triển khai binary mới cho API/worker → Developer gọi API và tải Excel → kiểm tra Completed/dữ liệu/quyền/badge → tự bật `InternalMail:GroupStorage:Enabled = true` và restart.

Không nhúng các fragment before/after SQL vào Up, không chạy full expand-backfill/bridge script của phương án cũ. Nếu đã cài trigger cũ, API báo Blocked để operator xử lý riêng; code không tự chạy DDL hoặc drop trigger.

Cờ false giữ nguồn đọc cũ, không query bảng mới trên list/hover. Live writer độc lập với cờ đọc: có bảng thì ghi mapping cùng transaction, chưa có bảng thì tương thích dữ liệu cũ. Không tạo schema trong code. Không thêm cờ bật writer.

Mapping không cấp quyền. Giữ ID, membership, quyền đọc/gửi, mute/archive, read-state, notification/outbox và cách tính badge. API chỉ bổ sung missing mapping; không tạo lại hội thoại/tin, không thay mốc thời gian hoặc đẩy nhóm rỗng lên đầu.

Rollback nguồn đọc: false + restart; giữ mapping và writer. Không dùng Down để xóa bảng/dữ liệu. Marker và điều kiện dọn: [TRANSITION_CLEANUP.md](TRANSITION_CLEANUP.md). Phương án trigger cũ chỉ lưu làm hồ sơ tại [PHASE2.md](PHASE2.md), không phải hướng dẫn áp dụng mới.
