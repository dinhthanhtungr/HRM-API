-- Optional deployment step, not executed by the application. Sign in/refresh after applying.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', p.code
FROM "AspNetRoles" r
CROSS JOIN (VALUES ('dev.workflow-notification-jobs.view'), ('dev.workflow-notification-jobs.retry')) p(code)
WHERE r."Name"='Developer'
AND NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId"=r."Id"
 AND c."ClaimType"='permission' AND c."ClaimValue"=p.code);
COMMIT;
