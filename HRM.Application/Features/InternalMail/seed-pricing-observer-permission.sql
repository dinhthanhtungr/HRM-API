-- Permission data only; no schema change. Run at deployment, then refresh/login.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.pricing-group.observe'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND existing."ClaimValue" = 'internal-mail.pricing-group.observe'
  );
COMMIT;
