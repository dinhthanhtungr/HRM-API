-- Grants mixing-stock read access only. No schema changes or other permissions.
-- Refresh/login after applying so explicit-permission tokens receive the claim.
BEGIN;

INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'warehouse.mixing-stock.view'
FROM "AspNetRoles" r
WHERE r."Name" IN ('PLPUUser', 'Developer', 'President')
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
        AND c."ClaimValue" = 'warehouse.mixing-stock.view'
  );

COMMIT;
