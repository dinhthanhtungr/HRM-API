-- Chỉ seed capability vào Identity hiện có, không thay đổi schema hay dữ liệu hồ sơ.
-- Sau khi chạy, Admin cần login/refresh token để nhận claim mới.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'plm.sample-request.legacy-work.manage'
FROM "AspNetRoles" r
WHERE r."Name" = 'Admin' AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId" = r."Id"
        AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.sample-request.legacy-work.manage');
COMMIT;
