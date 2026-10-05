-- Seeds capability claims into the existing ASP.NET Identity AspNetRoleClaims table.
-- Idempotent: existing role/permission claims are preserved and not duplicated.

BEGIN;

WITH permission_rows(role_name, permission) AS (
    VALUES
        ('Developer', 'internal-mail.message.delete'),
        ('Admin', 'mro.equipment.view'),
        ('Developer', 'mro.equipment.view'),
        ('President', 'mro.equipment.view'),
        ('MaintenanceUser', 'mro.equipment.view'),
        ('ManufactureUser', 'mro.equipment.view'),
        ('QLSXUser', 'mro.equipment.view'),
        ('Admin', 'mro.equipment.create'),
        ('Developer', 'mro.equipment.create'),
        ('President', 'mro.equipment.create'),
        ('MaintenanceUser', 'mro.equipment.create'),
        ('Admin', 'mro.equipment.update'),
        ('Developer', 'mro.equipment.update'),
        ('President', 'mro.equipment.update'),
        ('MaintenanceUser', 'mro.equipment.update'),
        ('Admin', 'mro.equipment.delete'),
        ('Developer', 'mro.equipment.delete'),
        ('President', 'mro.equipment.delete'),
        ('Developer', 'pricing.workbench.view'),
        ('President', 'pricing.workbench.view'),
        ('ACCUser', 'pricing.workbench.view'),
        ('SaleUser', 'pricing.workbench.view'),

        ('Admin', 'pricing.approved-selling-price.view'),
        ('Developer', 'pricing.approved-selling-price.view'),
        ('President', 'pricing.approved-selling-price.view'),
        ('SaleUser', 'pricing.approved-selling-price.view'),
        ('PriceView', 'pricing.approved-selling-price.view'),
        ('ACCUser', 'pricing.approved-selling-price.view'),
        ('SeePriceUser', 'pricing.approved-selling-price.view'),

        ('Admin', 'pricing.system-calculated-price.view'),
        ('Developer', 'pricing.system-calculated-price.view'),
        ('President', 'pricing.system-calculated-price.view'),
        ('PriceView', 'pricing.system-calculated-price.view'),
        ('ACCUser', 'pricing.system-calculated-price.view'),
        ('SeePriceUser', 'pricing.system-calculated-price.view'),

        ('Admin', 'pricing.material-cost.view'),
        ('Developer', 'pricing.material-cost.view'),
        ('President', 'pricing.material-cost.view'),
        ('PLPUUser', 'pricing.material-cost.view'),
        ('ACCUser', 'pricing.material-cost.view'),
        ('LabUser', 'pricing.material-cost.view'),

        ('Admin', 'pricing.manufacturing-cost.view'),
        ('Developer', 'pricing.manufacturing-cost.view'),
        ('President', 'pricing.manufacturing-cost.view'),
        ('ACCUser', 'pricing.manufacturing-cost.view'),
        ('Admin', 'pricing.margin.view'),
        ('Developer', 'pricing.margin.view'),
        ('President', 'pricing.margin.view'),
        ('ACCUser', 'pricing.margin.view'),
        ('Admin', 'pricing.history.view'),
        ('Developer', 'pricing.history.view'),
        ('President', 'pricing.history.view'),
        ('ACCUser', 'pricing.history.view'),
        ('Developer', 'pricing.manage'),
        ('President', 'pricing.manage'),
        ('ACCUser', 'pricing.manage'),
        ('Developer', 'pricing.approve'),
        ('President', 'pricing.approve'),
        ('ACCUser', 'pricing.approve'),

        ('Admin', 'plm.formula-price.view'),
        ('Developer', 'plm.formula-price.view'),
        ('President', 'plm.formula-price.view'),
        ('PriceView', 'plm.formula-price.view'),
        ('ACCUser', 'plm.formula-price.view'),
        ('SeePriceUser', 'plm.formula-price.view'),

        ('Admin', 'plm.formula-material.view'),
        ('Developer', 'plm.formula-material.view'),
        ('President', 'plm.formula-material.view'),
        ('PLPUUser', 'plm.formula-material.view'),
        ('ACCUser', 'plm.formula-material.view'),
        ('LabUser', 'plm.formula-material.view'),

        ('Admin', 'plm.product-technical-info.view'),
        ('Developer', 'plm.product-technical-info.view'),
        ('President', 'plm.product-technical-info.view'),
        ('LabUser', 'plm.product-technical-info.view'),
        ('PLPUUser', 'plm.product-technical-info.view'),

        ('Admin', 'dispatch.delivery-cost.view'),
        ('Developer', 'dispatch.delivery-cost.view'),
        ('President', 'dispatch.delivery-cost.view'),
        ('PriceView', 'dispatch.delivery-cost.view'),
        ('ACCUser', 'dispatch.delivery-cost.view'),
        ('SeePriceUser', 'dispatch.delivery-cost.view')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', permission_rows.permission
FROM permission_rows
JOIN "AspNetRoles" role
  ON lower(role."Name") = lower(permission_rows.role_name)
WHERE NOT EXISTS (
    SELECT 1
    FROM "AspNetRoleClaims" existing
    WHERE existing."RoleId" = role."Id"
      AND existing."ClaimType" = 'permission'
      AND lower(existing."ClaimValue") = lower(permission_rows.permission)
);

WITH managed_roles(role_name) AS (
    VALUES
        ('Admin'), ('Developer'), ('President'), ('SaleUser'), ('PriceView'),
        ('ACCUser'), ('SeePriceUser'), ('PLPUUser'), ('LabUser')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission-model', '1'
FROM managed_roles
JOIN "AspNetRoles" role
  ON lower(role."Name") = lower(managed_roles.role_name)
WHERE NOT EXISTS (
    SELECT 1
    FROM "AspNetRoleClaims" existing
    WHERE existing."RoleId" = role."Id"
      AND existing."ClaimType" = 'permission-model'
      AND existing."ClaimValue" = '1'
);

COMMIT;
