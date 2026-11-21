-- Thu hồi riêng quyền create mới bổ sung; giữ quyền view và mọi quyền manage hiện có.
BEGIN;
DELETE FROM "AspNetRoleClaims" c USING "AspNetRoles" r
WHERE c."RoleId" = r."Id" AND r."Name" IN ('PLPUUser', 'LabUser', 'Developer')
AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'plm.material-processing.create';
COMMIT;
