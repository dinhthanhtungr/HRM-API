-- Apply to the target Identity database, then sign in again/refresh the access token.
-- Idempotent data-only grant. No schema change, no permission-model marker change.
BEGIN;
WITH required(permission) AS (
    VALUES ('purchasing.catalog.manage'), ('purchasing.purchase-order.manage'),
           ('pricing.material-cost.view'), ('plm.formula-material.view'),
           ('plm.material-price-review.detail.view'), ('plm.material-document.extract')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', required.permission
FROM "AspNetRoles" r CROSS JOIN required
WHERE lower(r."Name") = 'plpuuser'
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
        AND lower(c."ClaimValue") = required.permission
  );
COMMIT;

SELECT r."Name", c."ClaimValue"
FROM "AspNetRoles" r JOIN "AspNetRoleClaims" c ON c."RoleId" = r."Id"
WHERE lower(r."Name") = 'plpuuser' AND c."ClaimType" = 'permission'
ORDER BY c."ClaimValue";
