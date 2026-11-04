-- Optional claim rollout on the existing Identity tables; no schema change. Run intentionally, never as startup code.
BEGIN;
WITH permission_rows(role_name, permission) AS (
    VALUES ('Admin', 'plm.sample-request.work-schedule.manage'),
           ('Developer', 'plm.sample-request.work-schedule.manage'),
           ('LabAdmin', 'plm.sample-request.work-schedule.manage')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT roles."Id", 'permission', p.permission
FROM permission_rows p JOIN "AspNetRoles" roles ON roles."Name" = p.role_name
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" claims WHERE claims."RoleId" = roles."Id"
    AND claims."ClaimType" = 'permission' AND claims."ClaimValue" = p.permission);
COMMIT;
