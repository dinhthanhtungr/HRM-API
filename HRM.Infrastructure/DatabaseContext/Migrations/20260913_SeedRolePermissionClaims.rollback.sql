-- Removes the baseline capability/marker values managed by 20260913_SeedRolePermissionClaims.sql.
-- Review before use if matching role claims were manually created before the forward seed.

BEGIN;

DELETE FROM "AspNetRoleClaims" c USING "AspNetRoles" r
WHERE c."RoleId"=r."Id" AND r."Name"='Developer' AND c."ClaimType"='permission'
 AND c."ClaimValue" IN ('dev.workflow-notification-jobs.view', 'dev.workflow-notification-jobs.retry');

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('LabAdmin', 'LabUser')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.sample-request.lab-teams.view-all';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'SaleUser'
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.sample-request.deactivate';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('Developer', 'President')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'internal-mail.company-messages.send';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'Developer'
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'internal-mail.company-conversations.observe';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('Developer', 'President')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'internal-mail.sample-request-groups.observe';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'Developer'
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'internal-mail.pricing-group.observe';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('PLPUUser', 'Developer', 'President')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'warehouse.mixing-stock.view';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('Developer', 'QCUser', 'LabUser')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.manufacturing-defective-lot.view';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'Developer'
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.production-order.requirement.edit';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'PLPUUser'
  AND c."ClaimType" = 'permission'
  AND c."ClaimValue" IN ('plm.material-document.extract', 'plm.material-price-review.detail.view');


DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND lower(r."Name") IN ('admin', 'developer', 'president', 'purchaser', 'plpuuser')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'purchasing.catalog.manage';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('President', 'Developer')
  AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'crm.quotation.send.confirm';

DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'PLPUUser'
  AND c."ClaimType" = 'permission'
  AND lower(c."ClaimValue") = 'purchasing.purchase-order.manage';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'plpuuser', 'qlsxuser', 'qcuser', 'labuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.manufacturing-color-chip.manage';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'plpuuser', 'qlsxuser', 'qcuser', 'labuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.manufacturing-adjustment.manage';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('developer', 'labuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.manufacturing-formula.manage';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'accuser', 'qcuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" IN ('dev-and-qa.coa.view', 'dev-and-qa.coa.manage');

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'plm.material-document.extract';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('president', 'accuser', 'developer')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" = 'reports.sales-profit.view';

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") = 'accuser'
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" IN ('dev-and-qa.iqc.view', 'plm.production-order.view',
      'mro.equipment.view', 'plm.material-price-review.detail.view');

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'qcuser', 'khouser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" IN ('dev-and-qa.iqc.view', 'dev-and-qa.iqc.manage');

DELETE FROM "AspNetRoleClaims" claim
USING "AspNetRoles" role
WHERE claim."RoleId" = role."Id"
  AND lower(role."Name") IN ('admin', 'developer', 'president', 'plpuuser', 'qlsxuser', 'manufactureuser', 'labuser', 'labadmin', 'qcuser')
  AND claim."ClaimType" = 'permission'
  AND claim."ClaimValue" IN ('plm.production-order.view', 'plm.production-order.check', 'plm.production-order.manage');

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
