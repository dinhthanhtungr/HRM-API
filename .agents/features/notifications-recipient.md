# Notification Recipient Rules

Đọc file này khi task thêm/sửa người nhận notification, role, team, group, owner, sender exclusion hoặc permission thu hồi người nhận.

## Recipient Scope

- Recipient phải được resolve rõ theo employee/user/role/team và vẫn phải tôn trọng company scope/ownership của feature gọi notification.
- Không để FE truyền tùy ý danh sách người nhận nếu recipient là rule nghiệp vụ cần kiểm soát ở backend.
- Không dùng role rộng như `Leader` để đại diện cho một bộ phận cụ thể nếu dữ liệu có group/team/department.
- Role global như `Admin`, `Developer`, `President` chỉ dùng khi nghiệp vụ thật sự cần quyền/toàn cảnh toàn công ty.
- Nếu thay đổi recipient nhầm sau khi đã phát, ưu tiên archive user state thay vì xóa cứng dữ liệu audit.

## Role Sets

- Không check trực tiếp nhiều role trong handler/service cho một quyền nghiệp vụ lặp lại.
- Tạo role set có tên theo hành động trong `HRM.Application/Commons/Authorization/ApplicationRoleSets.cs`, ví dụ `ApplicationRoleSets.Notifications.RecipientManagers`.
- `ApplicationRoles` chỉ chứa tên role thật trong DB/JWT. `ApplicationRoleSets` mới là nơi gom quyền theo nghiệp vụ.
- Không tạo constant kiểu `PresidentDeveloperRoles`; đặt tên theo quyền như `RecipientManagers`, `FormulaPriceViewers`, `MaterialSupplierPriceEditors`.

Quyền thu hồi người nhận notification phải dùng `ApplicationRoleSets.Notifications.RecipientManagers` cộng rule owner/creator hiện có; không hard-code `President`, `Developer` trực tiếp trong notification service.

## Resolver

Rule recipient có đụng DB/current user/company/group/role phải đặt trong DI service hoặc service hiện có, không dùng static helper. Khi thêm/sửa role set hoặc resolver, phải xác định role nào là global, role nào bị giới hạn theo group/team/department, group type nào áp dụng, và có loại trừ current user/sender không.
