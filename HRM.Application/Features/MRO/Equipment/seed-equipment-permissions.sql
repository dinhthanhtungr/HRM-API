-- Approved role mapping. Save for deployment only; do not run automatically.
-- Idempotent role-claim rollout for existing deployments; no schema changes.
-- Login/refresh is required for existing tokens to receive new capabilities.
BEGIN;
WITH permission_rows(role_name, permission) AS (
    VALUES
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
        ('President', 'mro.equipment.delete')
)
INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT role."Id", 'permission', permission_rows.permission
FROM permission_rows
JOIN "AspNetRoles" role ON lower(role."Name") = lower(permission_rows.role_name)
WHERE NOT EXISTS (
    SELECT 1 FROM "AspNetRoleClaims" existing
    WHERE existing."RoleId" = role."Id" AND existing."ClaimType" = 'permission'
      AND existing."ClaimValue" = permission_rows.permission
);
COMMIT;
