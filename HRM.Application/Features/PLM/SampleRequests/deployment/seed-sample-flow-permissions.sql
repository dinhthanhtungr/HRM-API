-- Permission claims only; no schema changes or migrations. Review and run explicitly, then refresh tokens.
BEGIN;
WITH grants(role_name, permission) AS (VALUES
 ('Admin', 'plm.sample-request.workflow.lab'),
 ('Developer', 'plm.sample-request.workflow.lab'),
 ('President', 'plm.sample-request.workflow.lab'),
 ('LabUser', 'plm.sample-request.workflow.lab'),
 ('LabAdmin', 'plm.sample-request.workflow.lab'),
 ('Admin', 'plm.sample-request.workflow.sale'),
 ('Developer', 'plm.sample-request.workflow.sale'),
 ('President', 'plm.sample-request.workflow.sale'),
 ('SaleUser', 'plm.sample-request.workflow.sale'),
 ('Leader', 'plm.sample-request.workflow.sale'))
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', g.permission FROM grants g
JOIN "AspNetRoles" r ON lower(r."Name") = lower(g.role_name)
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId" = r."Id"
 AND c."ClaimType" = 'permission' AND c."ClaimValue" = g.permission);
COMMIT;
