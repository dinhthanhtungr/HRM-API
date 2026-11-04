-- Permission data only; no schema change. Run on the target database before token refresh.
-- Existing permission claims are preserved. No purchasing mutation capability is granted.
BEGIN;
DELETE FROM "AspNetRoleClaims" c USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
  AND lower(c."ClaimValue") = 'reports.plpu.view'
  AND r."Name" IN ('Admin', 'Purchaser');
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'reports.plpu.view'
FROM "AspNetRoles" r
WHERE r."Name" IN ('Developer', 'President', 'PLPUUser')
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c
    WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
      AND lower(c."ClaimValue") = 'reports.plpu.view'
  );
COMMIT;
