-- Optional metadata alignment for the permanent configuration-management capability.
-- Operator runs separately; do not execute at startup or as part of group/member synchronization.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.default-membership.manage'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" existing WHERE existing."RoleId" = role."Id"
      AND existing."ClaimType" = 'permission' AND existing."ClaimValue" = 'internal-mail.default-membership.manage');
COMMIT;
