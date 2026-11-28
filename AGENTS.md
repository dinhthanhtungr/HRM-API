# AGENTS.md

## Bắt buộc dùng repo chính của máy hiện tại

Trước mọi thao tác ghi/build/test/chạy dịch vụ cho HRM, dùng [quy định workspace HRM](docs/agents/workspace.md) để xác minh cặp repo trên máy hiện tại. Giữ nguyên nhánh và thay đổi hiện có; nếu thiếu quyền, báo blocker. Bản F chỉ dùng để tham khảo.

Router bắt buộc cho coding agent trong repo `HRM.api`. Chỉ đọc rule đúng với phạm vi task; không nạp toàn bộ `.agents` theo mặc định.

## Luật Bất Biến

- Không revert hoặc ghi đè thay đổi có sẵn của user nếu chưa được yêu cầu rõ.
- Toàn bộ `F:\Backend` và mọi thư mục/file bên trong chỉ được đọc để tham khảo. Không tạo, sửa, ghi đè, xóa, đổi tên hoặc di chuyển bất kỳ nội dung nào tại đó; không chạy lệnh/tool có side effect ghi vào `F:\Backend` (kể cả build, format, code generation hoặc Git). Khi chuyển code từ nguồn này, chỉ ghi thay đổi vào repo đích được user yêu cầu.
- Không hard-code hoặc log secret, token, password, connection string hay API key.
- Không đặt business logic lớn trong controller và không trả EF entity trực tiếp ra public API.
- Application không phụ thuộc trực tiếp Infrastructure.
- API user-facing phải giữ `[Authorize]`, company scope, ownership/capability và chống IDOR theo contract hiện hành.
- Không dùng raw SQL nối chuỗi.
- Không tự thêm migration, bảng hoặc cột nếu user chưa đồng ý.
- Không tự tạo hoặc viết thêm script SQL/query tạo bảng, đổi cấu trúc DB, seed/cấp quyền hay rollback khi user chưa yêu cầu rõ các script đó. Đồng ý triển khai feature/entity không mặc nhiên là yêu cầu sinh script; chỉ thực hiện phần code cần thiết và báo ngắn thay đổi DB cần có, tránh sinh thêm file hoặc nội dung tốn token.
- Không dùng destructive Git như `reset --hard`, `checkout --`, force-push hoặc tự merge nếu user chưa yêu cầu rõ.
- Việc tạo hoặc chuyển nhánh phải được user đồng ý cho từng thao tác; xem `.agents/git-and-build.md`.
- Sửa đúng phạm vi; giữ nguyên nghiệp vụ và public contract ngoài phần user yêu cầu.
- Chỉ được tạo, gọi lại hoặc chia việc cho agent con khi user yêu cầu rõ trong task hiện tại; không suy quyền từ yêu cầu ở task cũ. Áp dụng cả agent review, kiểm tra độc lập và công việc chạy song song. Khi chưa được yêu cầu, agent chính tự thực hiện và kiểm tra, không tự gọi agent con hoặc hỏi chia việc như một bước bắt buộc.
- Sửa code/contract dùng chung phải rà các nơi sử dụng và kiểm tra ảnh hưởng dây chuyền trước khi bàn giao; áp dụng [quy trình kiểm tra ảnh hưởng](.agents/change-impact.md). Review bằng agent độc lập chỉ thực hiện khi user yêu cầu chia agent.

## Đọc Và Tái Sử Dụng Context

- AGENTS/rule đã có đầy đủ trong context được xem là đã đọc; không đọc lại chỉ vì sang lượt chat hoặc bước làm việc mới.
- Đọc lại phần cần dùng khi file thay đổi, chuyển repo/branch làm rule có thể đổi, hoặc context thiếu chi tiết sau khi nén. Kiểm tra diff/hash khi cần xác nhận phiên bản, không xuất lại toàn bộ file không đổi.
- Chọn rule theo phần thực sự sửa và dependency chịu tác động; không nạp theo từ khóa xuất hiện đơn thuần. Nếu phát hiện thêm boundary/security/side effect, đọc bổ sung trước khi sửa phần đó.
- README dài: tìm heading/use case bằng `rg`, đọc scope/quyền/invariant chung, toàn bộ mục liên quan và dependency được dẫn tới. Mở rộng nếu chưa đủ thông tin; không chỉ đọc vài dòng match rồi suy đoán contract.
- Khi context được nén, giữ danh sách rule đã đọc và phiên bản nếu đã kiểm tra, ràng buộc chính, quyết định, thay đổi của user và verification còn lại. Bản tóm tắt không thay thế chi tiết bảo mật/nghiệp vụ bị thiếu; không cần tạo file memory sao chép rule.

Quản lý/sửa cách điều hướng: xem [Bảng quản lý điều hướng HRM](docs/agents/README.md); không cần đọc trong task thông thường.

## Tiếp nhận yêu cầu đặc biệt

- Tin nhắn riêng `save`, `load` hoặc `laod` kích hoạt [quy trình đồng bộ BE/FE qua main](docs/agents/workspace.md#đồng-bộ-befe-qua-main). Hỏi xác nhận cho từng lần trước khi thao tác Git có ghi.

- User yêu cầu tiếp tục từ tên/link/id chat cũ: đọc [Bàn giao chat](docs/agents/chat-handoff.md), chỉ lấy context liên quan; không tự tạo chat, gửi tin hoặc gọi agent con.
- User báo chậm, tải lâu hoặc giật: đọc [Chẩn đoán hiệu năng HRM](docs/agents/hrm-performance.md); đề xuất phép đo còn thiếu và chờ đồng ý trước khi thêm, trừ khi đã được cho phép rõ trong task.
- Quản lý/sửa/giải thích cách điều hướng: đọc [Bảng quản lý điều hướng](docs/agents/README.md). Các hướng dẫn trên chỉ nạp khi trigger khớp, không đọc toàn bộ trong task thông thường.

## Quy tắc soạn prompt và Goal cho Codex

Tự áp dụng khi user nói “tạo prompt”, “cho prompt”, “sửa prompt”, “chốt prompt”, “viết Goal” hoặc yêu cầu tương đương. Dùng khung RCTOC với đủ năm phần cho cả prompt và nội dung Goal:

1. **Role** — Nêu vai trò phù hợp với công việc.
2. **Context** — Nêu hiện trạng, mục tiêu và căn cứ. Dùng 6W làm checklist để chọn dữ kiện liên quan: **Who** (ai liên quan), **What** (hiện trạng hoặc vấn đề gì), **Where** (ở đâu), **When** (khi nào), **Why** (vì sao cần làm), **With What** (dùng nguồn, dữ liệu hoặc công cụ nào). Khi có ảnh, đưa các chi tiết liên quan đọc được từ ảnh vào prompt và ghi rõ nếu cần đính kèm lại ảnh để đối chiếu.
3. **Task** — Nêu việc cần làm và mức độ được yêu cầu: giải thích, phân tích, lập kế hoạch hay triển khai.
4. **Output** — Nêu sản phẩm bàn giao và tiêu chí kiểm tra để xác định đã hoàn thành.
5. **Constraints** — Nêu phạm vi và các ràng buộc đã xác nhận.

- Soạn nội dung đủ để dùng trong phiên mới. Ghi lại các quyết định đã thống nhất, phân biệt dữ kiện với giả định và nêu thông tin còn thiếu nếu có thể ảnh hưởng đến kết quả.
- Khi làm rõ chi tiết trong lúc bàn phương án hoặc soạn prompt, giữ nguyên phương án đã thống nhất trừ khi user yêu cầu thay đổi.
- Trả toàn bộ prompt hoặc nội dung Goal trong một khối mã Markdown duy nhất để dễ sao chép. Khi user chỉ yêu cầu soạn prompt, chỉ trả khối prompt.

## Điều Hướng Rule

Với câu hỏi, tìm file hoặc giải thích code không sửa repo: chỉ đọc file này, rồi đọc file chuyên biệt nếu câu hỏi cần đến rule đó.

Khi sửa code:

- Luật code nền: `.agents/core.md`.
- Thêm/đổi tổ chức feature, service/helper, endpoint hoặc boundary giữa layer: `.agents/architecture.md`. Sửa cục bộ trong cấu trúc hiện có không cần nạp lại kiến trúc.
- Review/refactor rộng hoặc kiểm tra anti-pattern: `.agents/anti-patterns.md`.
- Sửa code dùng chung, API/DTO/interface, default, query, permission, mapping/config hoặc side effect có nơi khác sử dụng: `.agents/change-impact.md`, kể cả code không nằm trong thư mục Shared/Commons.
- Sửa chính `AGENTS.md` hoặc `.agents/*`: `.agents/agent-authoring.md`.
- Trước khi sửa code hoặc khi cần build/test/commit/push: `.agents/git-and-build.md`.

Security đọc theo đúng trigger:

- API, handler, DTO public, EF query, company scope hoặc PATCH: `.agents/security/api-security.md`.
- Permission, role gate, capability, authorization service hoặc dữ liệu giới hạn người xem/sửa: `.agents/security/authorization-capabilities.md`.
- Giá, cost, lương, margin hoặc field nhạy cảm theo quyền: `.agents/security/field-visibility.md`.
- Authentication, authorization policy, JWT, cookie, CORS, SignalR token hoặc cơ chế resolve current user/claims: `.agents/security/auth.md`. Chỉ dùng current user để scope query thì đọc API security.
- Upload, download, attachment hoặc storage: `.agents/security/files.md`.

Documentation và workflow:

- Đổi hành vi, API/DTO public, rule, security, side effect, config hoặc vận hành: `.agents/documentation.md`.
- Thêm, sửa, đổi tên hoặc xóa cờ tính năng (feature flag), kể cả cờ FE và cấu hình triển khai: bắt buộc áp dụng [quy tắc cập nhật sổ tính năng](docs/feature-flags.md#quy-tắc-cập-nhật-bắt-buộc) và cập nhật sổ trong cùng thay đổi.
- Task lớn, nhiều phase, còn quyết định nghiệp vụ hoặc user yêu cầu kiểm soát kế hoạch: `.agents/workflow.md`.
- Chỉ đọc `.agents/model-selection.md` khi user hỏi chọn model hoặc task thật sự cần khuyến nghị model.

Module nghiệp vụ:

- CRM CustomerCare: `.agents/features/crm.md`.
- CRM Quotations: `.agents/features/quotations.md`.
- PLM, formulas, sample requests: `.agents/features/plm.md`.
- Notifications, SignalR, Web Push: `.agents/features/notifications.md` và file con mà router này chỉ định.
- Warehouse: `.agents/features/warehouse.md`.
- Reports/Executive PnL: `.agents/features/reports.md`.

Nếu chưa rõ module, dùng `rg` tìm feature gần nhất rồi đọc README/AGENTS liên quan. Với PLM/Quotation, đọc router module và các phần con đúng trigger. Không đọc các module không liên quan.

## Cách Làm Việc

- Nghiên cứu, phân tích, đề xuất hoặc bàn giao thay đổi HRM → [Nghiên cứu và đánh giá ảnh hưởng](docs/agents/research-review.md).

Khi sử dụng, giải thích hoặc triển khai thuật ngữ chuyên môn trong task FE/BE (kể cả trao đổi không sửa code), tra và bổ sung mục liên quan trong [README thuật ngữ dùng chung](docs/glossary/README.md), theo quy ước cập nhật tại đó; không nạp toàn bộ danh mục mặc định.

Trước thay đổi đáng kể, báo ngắn rule đã đọc, phạm vi dự kiến và cách verify. Không cần preamble dài, đề xuất goal hay nhắc model cho câu hỏi, tìm code hoặc sửa nhỏ rõ ràng.

Chỉ đề xuất chia phase khi có migration/backfill, đổi public contract đang được dùng, tác động nhiều boundary, side effect khó đảo ngược hoặc còn quyết định nghiệp vụ chưa rõ. Nếu user yêu cầu chờ xác nhận, dừng ở kế hoạch.
