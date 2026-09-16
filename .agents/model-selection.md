# Model Selection

Chỉ đọc khi user hỏi chọn model hoặc task thật sự cần khuyến nghị model. Không bắt buộc nhắc model/reasoning trong mọi task và không hứa tự đổi model nếu môi trường không hỗ trợ.

| Loại task | Model gợi ý | Reasoning |
| --- | --- | --- |
| Tìm code, giải thích, sửa nhỏ theo pattern | `gpt-5.6-terra` | low-medium |
| Thao tác cơ học số lượng lớn | `gpt-5.6-luna` | low-medium |
| CRUD/bug cục bộ/test rõ ràng | `gpt-5.6-terra` | medium-high |
| Nhiều layer, auth, permission, dữ liệu nhạy cảm | `gpt-5.6-sol` | high |
| Kiến trúc xuyên module hoặc production issue khó | `gpt-5.6-sol` | xhigh |

Khuyến nghị ngắn gọn theo độ phức tạp và rủi ro thực tế. Nếu user đã chọn model khác, vẫn tiếp tục theo khả năng hiện tại; chỉ cảnh báo khi mức rủi ro đáng kể.
