-- Bổ sung quyền đọc công việc mọi team cho LabUser/LabAdmin. Không đổi schema. Refresh/login sau khi chạy.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'plm.sample-request.lab-teams.view-all'
FROM "AspNetRoles" r WHERE r."Name" IN ('LabAdmin', 'LabUser')
AND NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId"=r."Id"
 AND c."ClaimType"='permission' AND c."ClaimValue"='plm.sample-request.lab-teams.view-all');
COMMIT;
