# Agent Rule Authoring

Đọc file này khi tạo hoặc sửa `AGENTS.md` và `.agents/*`.

## Nguyên Tắc

- Root `AGENTS.md` là router kèm một nhóm nhỏ invariant an toàn; không bắt đọc mọi file con.
- Mỗi rule chỉ có một source of truth. Router và file khác chỉ trỏ tới nguồn đó, không sao chép checklist.
- Rule phải ghi rõ trigger đọc và phạm vi áp dụng.
- Rule nền dùng chung đặt ở `core.md`; workflow chỉ dành cho task phức tạp; build/Git chỉ ở `git-and-build.md`.
- Security và module rules chỉ nạp khi task đụng đúng capability, dữ liệu hoặc feature đó.
- Không đặt quy tắc chọn model, báo cáo dài hoặc nghi thức hội thoại vào luồng mặc định nếu chúng không bảo vệ code/nghiệp vụ.

## Tách File

Tách file khi một nhóm rule có trigger độc lập và việc tách giúp task khác không phải đọc nội dung không liên quan. Module lớn dùng file cha làm router tới file con.

Không tạo file mới cho một rule đơn lẻ và không sao chép invariant sang nested `AGENTS.md`. Nested `AGENTS.md` chỉ chứa khác biệt thật sự của subtree.

## Bảo Toàn Nghiệp Vụ

- Rule nghiệp vụ phải mô tả trạng thái code hiện hành và trỏ tới class/file thật khi có.
- Khi tối ưu rule, ưu tiên đổi routing trước; không đổi semantics của feature, permission, field visibility, topic/recipient hoặc contract.
- Trước khi xóa bản sao, xác nhận còn một source of truth và router vẫn dẫn tới nó bằng đúng trigger.
- Dùng tiếng Việt có dấu, UTF-8; README/rule mô tả trạng thái hiện tại, không phải nhật ký.
