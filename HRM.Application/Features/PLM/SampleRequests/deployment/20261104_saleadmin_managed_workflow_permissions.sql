-- DML only: no migration, table or column changes. Run against the intended application's Identity database.
-- Idempotent, additive, preserves custom permissions/model markers and does not issue tokens.
-- The new permission delegates ONLY to owners in the server-resolved managed employee scope.
BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE lower("Name") = lower('SaleAdmin')) THEN
        RAISE EXCEPTION 'SaleAdmin role was not found in the selected database';
    END IF;
END $$;

WITH permissions(code) AS (
    VALUES ('plm.sample-request.workflow.sale'),
           ('plm.sample-request.workflow.sale.managed'),
           ('plm.sample-request.customer-price.confirm'),
           ('plm.sample-trial.feedback.correct')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', permissions.code
FROM "AspNetRoles" role CROSS JOIN permissions
WHERE lower(role."Name") = lower('SaleAdmin')
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND lower(existing."ClaimValue") = lower(permissions.code)
  );

-- Eligibility for SaleUser leaders; the server also requires a managed Leader group.
-- Ordinary SaleUser without IsAdmin membership remains self-only even with this claim.
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', 'plm.sample-request.workflow.sale.managed'
FROM "AspNetRoles" role
WHERE lower(role."Name") = lower('SaleUser')
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetRoleClaims" existing
      WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
        AND lower(existing."ClaimValue") = 'plm.sample-request.workflow.sale.managed'
  );

COMMIT;
-- Refresh/login after API/FE deployment to receive updated role permissions.
-- Do not add a permission-model marker here: that would suppress other legacy fallback permissions.
