-- Quyền tạo mới lệnh NVL. Chạy thủ công khi triển khai; đăng nhập/refresh token sau khi chạy.
-- Không cấp quyền sửa/xác nhận cho PLPUUser. Không đổi schema hoặc dữ liệu nghiệp vụ.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', p.permission
FROM "AspNetRoles" r
CROSS JOIN (VALUES ('plm.material-processing.view'), ('plm.material-processing.create')) p(permission)
WHERE r."Name" IN ('PLPUUser', 'LabUser', 'Developer')
AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c
    WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = p.permission
);
COMMIT;
