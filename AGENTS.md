# AGENTS.md

Hướng dẫn điều hướng bắt buộc cho AI/coding agent khi làm việc trong repo `HRM.api`.

File này chỉ là router và một ít luật nền tối thiểu. Trước khi làm việc, luôn đọc file này, sau đó đọc các file `.agents/...` được liệt kê dưới đây theo đúng ngữ cảnh. Trước khi bắt tay làm, nói ngắn gọn đã đọc `AGENTS.md` và những file agent con nào.

## Luôn Đọc

Trong mọi task có khả năng sửa repo, đọc các file sau:

- Core rules: `.agents/core.md`
- Workflow rules: `.agents/workflow.md`
- Anti-patterns: `.agents/anti-patterns.md`
- Documentation rules: `.agents/documentation.md`
- Model selection: `.agents/model-selection.md`

Nếu task chỉ là câu hỏi ngắn hoặc tìm thông tin, vẫn đọc `AGENTS.md`; đọc thêm file con khi câu hỏi đụng luật tương ứng.

## Điều Hướng Theo Ngữ Cảnh

### Kiến Trúc, Code Organization, Shared Component

- Khi thêm/sửa feature, service, helper, endpoint hoặc boundary giữa layer: đọc `.agents/architecture.md`.
- Khi tạo/sửa chính hệ thống agent/rule: đọc `.agents/agent-authoring.md`.

### Bảo Mật, API, Auth, File

- Khi thêm/sửa API, DTO public, EF query, permission, company scope, PATCH/partial update: đọc `.agents/security/api-security.md`.
- Khi đụng field nhạy cảm theo role như giá, cost, lương, margin hoặc thông tin nội bộ: đọc `.agents/security/field-visibility.md`.
- Khi đụng auth, JWT, cookie, CORS, SignalR token hoặc current user: đọc `.agents/security/auth.md`.
- Khi đụng upload/download/attachment/storage: đọc `.agents/security/files.md`.

### Build, Git, Verification

- Khi cần build/test/commit/push hoặc chuẩn bị kết thúc thay đổi code: đọc `.agents/git-and-build.md`.
- Với feature hoặc bug fix độc lập, mặc định agent được phép tự tạo nhánh `codex/...`, verify, commit đúng phạm vi và push nhánh sau khi hoàn tất mà không cần hỏi lại. Agent không được tự merge; các ngoại lệ phải dừng xin xác nhận được quy định trong `.agents/git-and-build.md`.

### Module Nghiệp Vụ

- CRM CustomerCare: `.agents/features/crm.md`
- Báo giá CRM: `.agents/features/quotations.md`
- PLM, formulas, sample requests: `.agents/features/plm.md`
- Notifications, SignalR, Web Push: `.agents/features/notifications.md`
- Warehouse: `.agents/features/warehouse.md`
- Reports/Executive PnL: `.agents/features/reports.md`

Nếu không chắc task thuộc module nào, dùng `rg` tìm feature gần nhất rồi đọc README/AGENTS liên quan.

## Luật Tối Thiểu Không Được Bỏ Qua

- Không revert code người dùng đã sửa trừ khi được yêu cầu rõ.
- Không hard-code secret/token/password/connection string/API key.
- Không viết logic nghiệp vụ lớn trong controller.
- Không trả EF entity trực tiếp ra FE.
- Không bỏ qua `[Authorize]`, company scope, ownership và IDOR check với API user-facing.
- Không dùng raw SQL nối chuỗi.
- Không tự ý thêm migration/bảng/cột nếu user chưa đồng ý.
- Không dùng destructive git command như reset/checkout nếu user không yêu cầu rõ.

## Trước Khi Làm

Với task sửa code hoặc tài liệu rule, trước khi sửa hãy nói:

- Đã đọc `AGENTS.md`.
- Đã đọc các file `.agents/...` liên quan nào.
- Dự định làm các bước chính nào.
- Đánh giá nhanh có thể làm trọn trong một lượt không, có cần chia nhỏ việc không, và có nên đề xuất goal không.
- Model khuyến nghị và reasoning khuyến nghị cho task hiện tại.
- README/summary/build/test dự kiến xử lý ra sao nếu task có đổi code.

Nếu user yêu cầu chờ xác nhận trước khi làm, dừng ở kế hoạch và chỉ thực hiện khi user đồng ý.
