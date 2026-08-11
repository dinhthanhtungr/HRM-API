# Agent Rule Authoring

Đọc file này khi tạo mới hoặc sửa `AGENTS.md` và các file `.agents/...`.

## Nguyên Tắc Tách File

`AGENTS.md` root chỉ nên là router: nói file nào luôn đọc, file nào đọc theo ngữ cảnh, và cách điều hướng tiếp. Không nhét chi tiết nghiệp vụ dài vào root.

Các file rule nên có vai trò rõ:

- Core rules: luật nền bắt buộc như kiến trúc, bảo mật, public contract, build.
- Workflow rules: cách làm việc, xin xác nhận, cập nhật README, trả lời cuối.
- Anti-patterns: những thứ tuyệt đối tránh.
- Module rules: CRM, PLM, Warehouse, Notification, Reports.
- Router module lớn: nếu một module có nhiều nghiệp vụ con, file cha chỉ điều hướng sang file con.

## Khi Nào Tách Nhỏ

Tách file con khi một file module bắt đầu chứa nhiều nhóm luật độc lập, ví dụ notification có topic, recipient, Web Push, outbox, final report. File cha nên giữ ngắn và chỉ định khi nào đọc từng file con.

Không tạo file mới chỉ vì một rule xuất hiện một lần. Tạo file mới khi rule có khả năng được đọc độc lập hoặc giúp tránh bắt agent đọc quá nhiều nghiệp vụ không liên quan.

## Chất Lượng Nội Dung

- Dùng tiếng Việt có dấu, UTF-8, không để mojibake.
- Rule phải nói rõ "khi nào đọc" và "khi nào áp dụng".
- Rule nghiệp vụ phải trỏ tới class/file thật trong repo nếu có.
- Không viết theo kế hoạch chưa implement.
- Không biến README/rule thành nhật ký dài; rule mô tả trạng thái đúng hiện tại.
