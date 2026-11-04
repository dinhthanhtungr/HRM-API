-- Deployment only: no schema changes. Refresh/login after applying role claims.
BEGIN;
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'plm.manufacturing-defective-lot.view'
FROM "AspNetRoles" role
WHERE lower(role."Name") IN ('developer', 'qcuser', 'labuser')
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" existing
    WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
      AND existing."ClaimValue" = 'plm.manufacturing-defective-lot.view'
  );
DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id" AND lower(role."Name") = 'plpuuser'
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.manufacturing-adjustment.manage';
COMMIT;
-- Rollback: DELETE FROM "AspNetRoleClaims" WHERE "ClaimType" = 'permission'
-- AND "ClaimValue" = 'plm.manufacturing-defective-lot.view';
