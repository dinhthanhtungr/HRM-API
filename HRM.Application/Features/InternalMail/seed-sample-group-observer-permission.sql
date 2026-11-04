-- Permission data only; no schema change. Run at deployment, then refresh/login.
BEGIN;
DELETE FROM "AspNetRoleClaims" claims USING "AspNetRoles" role
WHERE claims."RoleId" = role."Id" AND lower(role."Name") = 'president'
  AND claims."ClaimType" = 'permission'
  AND claims."ClaimValue" IN ('internal-mail.company-messages.send', 'internal-mail.sample-request-groups.observe');
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.sample-request-groups.observe'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND existing."ClaimValue" = 'internal-mail.sample-request-groups.observe'
  );
COMMIT;
