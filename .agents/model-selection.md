# Model Selection

Đọc file này trong mọi task để nhắc model khuyến nghị cho user. Agent không được hứa tự đổi model nếu môi trường hiện tại không hỗ trợ đổi model trực tiếp; chỉ báo model nên dùng để user chọn đúng.

## Mặc Định

- Với câu hỏi thường, tìm hiểu code, giải thích luồng, đọc file hoặc dò pattern: khuyến nghị mặc định `gpt-5.6-terra`.
- Với câu hỏi rất đơn giản hoặc thao tác số lượng lớn cần tiết kiệm: cân nhắc `gpt-5.6-luna`.
- Với task phức tạp, rủi ro cao, nhiều module hoặc nhiều side effect: khuyến nghị `gpt-5.6-sol`.
- Khi chuẩn bị làm việc và báo đã đọc agent nào, agent phải nói thêm model khuyến nghị theo bảng bên dưới.
- Nếu user đã chọn model khác, vẫn làm theo khả năng hiện tại nhưng nên nhắc nhẹ nếu task có rủi ro cao hơn model đang dùng.

## Bảng Khuyến Nghị

| Loại task | Model | Reasoning |
| --- | --- | --- |
| Tìm file, dò pattern, giải thích code | `gpt-5.6-terra` | low-medium |
| Kiểm tra trước, đọc agent, soi code, đề xuất hướng làm rồi chờ user OK | `gpt-5.6-terra` | medium |
| Câu hỏi rất đơn giản hoặc thao tác cơ học số lượng lớn cần tiết kiệm | `gpt-5.6-luna` | low-medium |
| Sửa typo, README, XML summary, mapping DTO đơn giản | `gpt-5.6-terra` | medium |
| CRUD theo pattern có sẵn, bug cục bộ, viết test rõ ràng | `gpt-5.6-terra` | medium-high |
| Feature đi qua API -> Application -> Infrastructure | `gpt-5.6-sol` | medium-high |
| Auth, JWT, permission, company scope, ownership, IDOR | `gpt-5.6-sol` | high |
| PATCH semantics, field nhạy cảm, EF query phức tạp | `gpt-5.6-sol` | high |
| Notification + SignalR + Web Push hoặc flow nhiều side effect | `gpt-5.6-sol` | high-xhigh |
| Reports/PnL, công thức PLM, logic tài chính/lương/cost | `gpt-5.6-sol` | high-xhigh |
| Thiết kế kiến trúc, refactor xuyên module, điều tra lỗi production khó | `gpt-5.6-sol` | xhigh |
| Thao tác cơ học sau khi thiết kế đã chốt | `gpt-5.6-terra` | medium |

## Cách Báo Cho User

Trong phần báo trước khi làm, ghi ngắn gọn:

- `Model khuyến nghị: ...`
- `Reasoning khuyến nghị: ...`
- Lý do một câu nếu task có rủi ro cao hoặc nên dùng `gpt-5.6-sol`.
- Nếu user chỉ yêu cầu "kiểm tra", "xem thử", "set hướng", "đọc agent rồi báo tôi OK mới làm", khuyến nghị `gpt-5.6-terra` reasoning `medium` cho bước khảo sát. Sau khi user OK, đánh giá lại model cho bước implement thật.

Nếu task quá rộng, vừa đề xuất goal vừa nhắc model khuyến nghị cho goal đó.
