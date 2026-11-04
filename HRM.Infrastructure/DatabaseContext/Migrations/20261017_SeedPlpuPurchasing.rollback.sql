-- Use only if the forward seed introduced this claim; preserve pre-existing grants.
-- Also revert the PLPUUser role grant in code to revoke effective purchasing access.
DELETE FROM "AspNetRoleClaims" c
USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" = 'PLPUUser'
  AND c."ClaimType" = 'permission'
  AND lower(c."ClaimValue") = 'purchasing.purchase-order.manage';
