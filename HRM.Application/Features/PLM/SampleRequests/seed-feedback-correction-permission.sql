-- Quyền sửa ghi chú/ngày phản hồi lần mới nhất. Không thay đổi schema.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'plm.sample-trial.feedback.correct'
FROM "AspNetRoles" r WHERE r."Name" = 'SaleUser' AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId" = r."Id"
    AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.sample-trial.feedback.correct');
COMMIT;
