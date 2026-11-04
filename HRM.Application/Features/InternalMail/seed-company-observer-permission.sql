-- Optional catalog/claim alignment. Company observation/send: Developer only, including older explicit tokens.
-- No schema change; refreshing/login is required after a role is changed or revoked.
BEGIN;
DELETE FROM "AspNetRoleClaims" claims USING "AspNetRoles" role
WHERE claims."RoleId" = role."Id" AND lower(role."Name") = 'president'
  AND claims."ClaimType" = 'permission'
  AND claims."ClaimValue" IN ('internal-mail.company-messages.send', 'internal-mail.sample-request-groups.observe');
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.company-conversations.observe'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND existing."ClaimValue" = 'internal-mail.company-conversations.observe'
  );
COMMIT;

BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.company-messages.send'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND existing."ClaimValue" = 'internal-mail.company-messages.send'
  );
COMMIT;
