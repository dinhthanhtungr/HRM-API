# Authentication

## JWT role claims

Khi login hoặc refresh token, backend chỉ lấy role assignment có
`AspNetUserRoles.IsActive = true`. Danh sách này được trả trong `LoginResultDto.Roles`
và ghi vào nhiều claim `roles` của access token.

Khi thu hồi role, `EmployeeIdentityAdministrationService.RevokeRoleAsync` chỉ chuyển
`ApplicationUserRole.IsActive` thành `false`; role đã thu hồi không được xuất hiện trong
login response, access token mới hoặc policy authorization.

Access token đã cấp trước khi role bị thu hồi không bị thay đổi ngược. Người dùng phải
login/refresh để lấy token mới, hoặc token cũ hết hạn theo `Jwt:EXPIRATION_MINUTES`.

## JWT permission claims

Login/refresh đọc `AspNetRoleClaims` của các role assignment active. Claim `permission` được hợp nhất, loại trùng và
đưa vào access token. Claim `permission-model = 1` đánh dấu role đã dùng permission DB; khi có marker, một tập
permission rỗng là quyết định thu hồi toàn bộ chứ không fallback sang role hard-code.

Role chưa có marker tiếp tục dùng `ApplicationPermissionRoleSets` để access token cũ và rollout trước/sau seed không
làm mất quyền đột ngột. Baseline được seed idempotent bằng
`HRM.Infrastructure/DatabaseContext/Migrations/20260913_SeedRolePermissionClaims.sql`.

Thay đổi role claim có hiệu lực sau lần login/refresh kế tiếp hoặc khi access token cũ hết hạn. Không đặt permission
nhạy cảm trong log và không dùng permission claim thay cho company/ownership/record-scope check.

## Session contract

REST clients may authenticate with the HttpOnly `hrm_access_token` cookie. Login and refresh return the same
identity snapshot used to issue the access token, including `userId`, `employeeId`, `companyId` and active role
assignments. `GET /api/v1/auth/me` reads those values from the validated access-token principal; it does not query
roles from a separate cache.

Roles, permissions and company membership are access-token snapshots. Database changes to a role assignment or
permission claim become visible after the next successful login or refresh. Every authenticated request still validates that the account/employee
relationship is active and that the employee remains in the token company.

## Cookie contract

When `useCookie = true`, login and refresh set a host-only cookie named `hrm_access_token` with:

- `HttpOnly = true`
- `Secure = true`
- `SameSite = None`
- `Path = /`
- expiry equal to the access-token expiry

The API does not set a `Domain` attribute. Logout deletes the same host-only/path cookie. Logout is intentionally
anonymous-safe so an expired or invalid access token does not prevent the browser from clearing its cookie; the
refresh token is revoked when a valid authenticated principal is available.

## Refresh-token session mode

The current schema stores one refresh token per identity account. By default,
`RefreshToken:ReuseActiveToken = false`, so refresh uses atomic rotation. Cross-tab coordination belongs to the FE:
only one tab refreshes, then broadcasts the new authentication state to the other tabs.

When rotation is enabled, only one concurrent refresh using the same token succeeds; the winner receives the new
token and later requests using the consumed token receive `401`. This protects against reuse of an older token.

Set the environment variable `RefreshToken__ReuseActiveToken=true` and restart the API only as a temporary fallback
when cross-tab refresh coordination is unavailable. In compatibility mode, successful login and refresh reuse the
active unexpired token stored in the database. Logout or administrative revocation then invalidates that shared
token for every tab/device, and a copied token remains usable until it expires or is revoked.

Fully independent, individually revocable browser/device sessions still require a separate session/token-family
table. The compatibility switch is an operational fallback, not the preferred security policy.

## Pricing visibility

Pricing handlers and `/api/v1/auth/me` use the same JWT bearer principal. The bearer handler obtains the JWT from
the cookie for REST requests (or the SignalR query token only on the notifications hub). The backend has no response,
output, distributed or application DTO cache for pricing responses. Sensitive pricing fields may be returned as
`null` when the principal lacks the corresponding capability; this is field redaction rather than an authentication error.
