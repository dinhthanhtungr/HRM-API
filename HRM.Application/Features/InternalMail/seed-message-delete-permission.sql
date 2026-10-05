-- Deployment only: grant moderation to Developer; no schema changes.
-- Refresh/login after rollout so the access token includes this permission.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'internal-mail.message.delete'
FROM "AspNetRoles" role
WHERE lower(role."Name") = 'developer'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND existing."ClaimValue" = 'internal-mail.message.delete'
  );
COMMIT;
