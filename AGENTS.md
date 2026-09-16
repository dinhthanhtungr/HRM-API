# AGENTS.md

Router bắt buộc cho coding agent trong repo `HRM.api`. Chỉ đọc rule đúng với phạm vi task; không nạp toàn bộ `.agents` theo mặc định.

## Luật Bất Biến

- Không revert hoặc ghi đè thay đổi có sẵn của user nếu chưa được yêu cầu rõ.
- Không hard-code hoặc log secret, token, password, connection string hay API key.
- Không đặt business logic lớn trong controller và không trả EF entity trực tiếp ra public API.
- API user-facing phải giữ `[Authorize]`, company scope, ownership/capability và chống IDOR theo contract hiện hành.
- Không dùng raw SQL nối chuỗi.
- Không tự thêm migration, bảng hoặc cột nếu user chưa đồng ý.
- Không dùng destructive Git như `reset --hard`, `checkout --`, force-push hoặc tự merge nếu user chưa yêu cầu rõ.
- Sửa đúng phạm vi; giữ nguyên nghiệp vụ và public contract ngoài phần user yêu cầu.

## Điều Hướng Rule

Với câu hỏi, tìm file hoặc giải thích code không sửa repo: chỉ đọc file này, rồi đọc file chuyên biệt nếu câu hỏi cần đến rule đó.

Khi sửa code:

- Luật code nền: `.agents/core.md`.
- Feature, service/helper, endpoint hoặc boundary giữa layer: `.agents/architecture.md`.
- Review/refactor rộng hoặc kiểm tra anti-pattern: `.agents/anti-patterns.md`.
- Sửa chính `AGENTS.md` hoặc `.agents/*`: `.agents/agent-authoring.md`.
- Trước khi sửa code hoặc khi cần build/test/commit/push: `.agents/git-and-build.md`.

Security đọc theo đúng trigger:

- API, handler, DTO public, EF query, company scope hoặc PATCH: `.agents/security/api-security.md`.
- Permission, role gate, capability, authorization service hoặc dữ liệu giới hạn người xem/sửa: `.agents/security/authorization-capabilities.md`.
- Giá, cost, lương, margin hoặc field nhạy cảm theo quyền: `.agents/security/field-visibility.md`.
- JWT, cookie, CORS, SignalR token hoặc current user: `.agents/security/auth.md`.
- Upload, download, attachment hoặc storage: `.agents/security/files.md`.

Documentation và workflow:

- Đổi hành vi, API/DTO public, rule, security, side effect, config hoặc vận hành: `.agents/documentation.md`.
- Task lớn, nhiều phase, còn quyết định nghiệp vụ hoặc user yêu cầu kiểm soát kế hoạch: `.agents/workflow.md`.
- Chỉ đọc `.agents/model-selection.md` khi user hỏi chọn model hoặc task thật sự cần khuyến nghị model.

Module nghiệp vụ:

- CRM CustomerCare: `.agents/features/crm.md`.
- CRM Quotations: `.agents/features/quotations.md`.
- PLM, formulas, sample requests: `.agents/features/plm.md`.
- Notifications, SignalR, Web Push: `.agents/features/notifications.md` và file con mà router này chỉ định.
- Warehouse: `.agents/features/warehouse.md`.
- Reports/Executive PnL: `.agents/features/reports.md`.

Nếu chưa rõ module, dùng `rg` tìm feature gần nhất rồi đọc README/AGENTS liên quan. Không đọc các module không liên quan.

## Cách Làm Việc

Trước thay đổi đáng kể, báo ngắn rule đã đọc, phạm vi dự kiến và cách verify. Không cần preamble dài, đề xuất goal hay nhắc model cho câu hỏi, tìm code hoặc sửa nhỏ rõ ràng.

Chỉ đề xuất chia phase khi có migration/backfill, đổi public contract đang được dùng, tác động nhiều boundary, side effect khó đảo ngược hoặc còn quyết định nghiệp vụ chưa rõ. Nếu user yêu cầu chờ xác nhận, dừng ở kế hoạch.
