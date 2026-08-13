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
