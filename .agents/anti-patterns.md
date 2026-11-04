# Anti-Patterns

Đọc khi review/refactor rộng hoặc kiểm tra chất lượng xuyên module. Đây là điều hướng review; không tạo bản sao checklist đã có.

Đối chiếu code trong phạm vi review với root `AGENTS.md`, `core.md` và các nguồn sau theo phần chịu tác động:

- Tổ chức layer, shared code, helper/DI, handler: `architecture.md`.
- API/DTO, EF, company/ownership, over-posting, PATCH: `security/api-security.md`.
- Capability, role mapping, dữ liệu nhạy cảm: `security/authorization-capabilities.md` và `security/field-visibility.md`.
- Authentication, token/cookie/CORS: `security/auth.md`.
- Attachment/storage: `security/files.md`.
- Notification, topic, recipient, transport: `features/notifications.md` và file con được router chỉ định.
- Git, artifacts và verification: `git-and-build.md`.

Chỉ nạp nguồn liên quan chưa có đủ trong context. Phát hiện vấn đề ngoài phạm vi thì báo riêng, không tự mở rộng refactor.
