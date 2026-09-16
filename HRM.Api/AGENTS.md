# HRM.Api Rules

Áp dụng bổ sung khi sửa file dưới `HRM.Api`; root `AGENTS.md` và rule được router chỉ định vẫn có hiệu lực.

- Controller chỉ bind request/route, gọi Application và trả response; business rule không đặt tại presentation layer.
- Endpoint user-facing phải đánh giá `[Authorize]` và đọc `.agents/security/api-security.md`.
- Khi sửa JWT, cookie, CORS, current user hoặc SignalR token, đọc `.agents/security/auth.md`.
- Khi sửa hub/outbox/notification transport, đọc `.agents/features/notifications.md` và file con liên quan.
- Registration/DI phải dùng abstraction và pattern hiện có; không hard-code secret hoặc config phụ thuộc môi trường.
