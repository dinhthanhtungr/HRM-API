-- GROUP-STORAGE-TRANSITION: synchronization-api — optional catalog alignment, not required for existing Developer tokens.
-- Do not execute automatically. Removing the API does not imply changing historical role claims without review.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.group-storage.synchronize'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" existing WHERE existing."RoleId" = role."Id"
    AND existing."ClaimType" = 'permission' AND existing."ClaimValue" = 'internal-mail.group-storage.synchronize');
COMMIT;
