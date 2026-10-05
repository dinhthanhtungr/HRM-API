-- Removes the baseline capability/marker values managed by 20260913_SeedRolePermissionClaims.sql.
-- Review before use if matching role claims were manually created before the forward seed.

BEGIN;

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'plpuuser', 'qlsxuser', 'manufactureuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.production-order.create';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN (
      'admin', 'developer', 'president', 'saleuser', 'priceview',
      'accuser', 'seepriceuser', 'plpuuser', 'labuser')
  AND (
      (claim."ClaimType" = 'permission-model' AND claim."ClaimValue" = '1')
      OR
      (claim."ClaimType" = 'permission' AND claim."ClaimValue" IN (
          'pricing.workbench.view',
          'pricing.approved-selling-price.view',
          'pricing.system-calculated-price.view',
          'pricing.material-cost.view',
          'pricing.manufacturing-cost.view',
          'pricing.margin.view',
          'pricing.history.view',
          'pricing.manage',
          'pricing.approve',
          'plm.formula-price.view',
          'plm.formula-material.view',
          'plm.product-technical-info.view',
          'dispatch.delivery-cost.view'
      ))
  );

COMMIT;
