-- Idempotent permission data only; no schema changes. Review/deploy separately.
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', 'purchasing.purchase-order.manage'
FROM "AspNetRoles" r
WHERE r."Name" = 'PLPUUser'
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" c
    WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
      AND lower(c."ClaimValue") = 'purchasing.purchase-order.manage'
  );
