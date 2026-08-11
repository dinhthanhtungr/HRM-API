# Auth, JWT, Cookie, CORS

Đọc file này khi task đụng authentication, authorization policy, JWT, cookie, CORS, SignalR token hoặc current user.

## JWT

- JWT phải validate issuer/audience/lifetime/signing key.
- Không hard-code signing key thật trong source.
- Không log access token, refresh token, cookie hoặc claims nhạy cảm.
- Refresh token phải được validate hạn sử dụng và thu hồi đúng rule.

## Cookie Auth

- Cookie auth cross-site phải cân nhắc `HttpOnly`, `Secure`, `SameSite=None`, `Path=/`.
- Cookie name nếu dùng nhiều nơi nên đưa vào constant đúng scope.
- Không trả token trong log response/request.

## CORS

- Không kết hợp `AllowAnyOrigin()` với `AllowCredentials()`.
- Allowed origins thay đổi theo môi trường phải nằm trong config/options.

## SignalR Token

- Nếu SignalR dùng token query, chỉ chấp nhận token query cho hub path cần thiết.
- Không dùng token query rộng rãi cho mọi endpoint.

