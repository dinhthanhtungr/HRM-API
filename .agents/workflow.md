# Workflow Rules

Đọc file này trong mọi task có khả năng sửa repo. File này mô tả cách làm việc, cách xin xác nhận và cách trả lời.

## Trước Khi Làm

- Trước khi bắt tay làm việc, nói ngắn gọn đã đọc `AGENTS.md` và các file `.agents/...` liên quan nào.
- Báo tình trạng hiểu việc: task đang rõ hay còn mơ hồ, phần nào cần soi code thêm, phần nào có rủi ro.
- Tự đánh giá khả năng làm trọn trong một lượt. Nếu phạm vi quá rộng, nhiều module hoặc có nhiều quyết định nghiệp vụ chưa rõ, nói rõ nên chia nhỏ phần nào trước.
- Nhắc model khuyến nghị và reasoning khuyến nghị theo `.agents/model-selection.md`; nếu chỉ là câu hỏi thường thì có thể ghi ngắn là mặc định `gpt-5.6-terra` đủ.
- Nếu task có rủi ro hoặc nhiều cách hiểu, nói rõ mình định làm gì và chờ user đồng ý khi user yêu cầu kiểm soát trước khi làm.
- Nếu task chỉ là câu hỏi, giải thích, tìm code hoặc sửa nhỏ rõ ràng, không tạo goal và không dừng ở kế hoạch dài.
- Nếu task dài nhiều bước, chạm nhiều module, dễ trôi ngữ cảnh hoặc cần kiểm tra nhiều lớp, tự đánh giá và đề xuất goal ngắn gọn, nói vì sao cần goal, rồi chờ user đồng ý.
- Nếu vẫn có thể làm trọn ngay, nói ngắn gọn sẽ làm luôn và nêu các bước chính thay vì yêu cầu user chia nhỏ.

## Quyết Định Chia Phase

- Chia phase khi thay đổi có từ hai boundary trở lên (API, Application, Domain, Infrastructure, database, FE contract), có migration/backfill dữ liệu, thay đổi contract đang được client sử dụng, side effect khó đảo ngược, hoặc còn quyết định nghiệp vụ chưa rõ.
- Mỗi phase phải deploy/test độc lập được, có phạm vi, tiêu chí hoàn tất, tác động compatibility và rollback rõ ràng. Ưu tiên phase đầu tạo nền an toàn: model/validation, API đọc hoặc dual-write; không trộn migration dữ liệu lớn với đổi contract FE nếu chưa có kế hoạch tương thích.
- Không cần chia phase cho thay đổi cục bộ, có một use case rõ, không đổi schema/contract công khai, không có side effect đáng kể và có thể build/test trọn trong một lượt.
- Nếu task cần chia phase, agent phải đề xuất danh sách phase và điểm cần user quyết định trước khi bắt đầu phase có tác động contract, dữ liệu hoặc vận hành. Không tự thêm migration hay tự đổi contract client hàng loạt khi chưa được user xác nhận.

## Khi Sửa Code

- Sửa đúng phạm vi yêu cầu, không refactor lan nếu không cần.
- Không revert code người dùng đã sửa trừ khi được yêu cầu rõ.
- Nếu file đang có thay đổi không phải mình tạo, đọc kỹ và làm việc cùng thay đổi đó.
- Khi user chỉ xin prompt/code mẫu, không tự ý sửa repo.
- Khi user nói "làm đi" hoặc "implement", tiến hành sửa code và verify.
- Không tự ý thêm migration/bảng/cột nếu user chưa đồng ý.
- Không sửa formatting toàn repo nếu chỉ làm feature nhỏ.
- Không dùng destructive git command như reset/checkout nếu user không yêu cầu rõ.

## Documentation Khi Sửa Feature

Nếu code thay đổi hành vi, API, DTO public, rule, bảo mật, side effect, config hoặc cách vận hành, cập nhật README feature trong cùng thay đổi. README mô tả trạng thái đang đúng của feature, không phải nhật ký sửa code. Nếu cần lịch sử thay đổi, dùng `CHANGELOG.md` gần feature.

Feature mới hoặc feature có nghiệp vụ đáng chú ý nên có summary ngắn trên command/query/handler hoặc README gần feature. Không backfill toàn repo chỉ để đủ summary/README.

## Trả Lời Cuối

Khi trả lời user:

- Nói rõ đã sửa file nào.
- Nói rõ hành vi API mới/cũ nếu có thay đổi.
- Nói rõ build/test đã chạy và kết quả.
- Nếu có warning cũ không liên quan, ghi rõ là warning cũ.
- Nếu tạo/sửa notification, nói rõ topic/topicCode/category, nơi publish, recipient, payload và SignalR/Web Push có đổi hay không; không chỉ trả mỗi đường dẫn.
