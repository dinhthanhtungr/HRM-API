-- Optional narrow rollout for environments that previously seeded the LabAdmin technical claim.
-- Review first: do not run if that role claim is an intentional local grant.
-- Existing Identity tables only; no schema change. Never executed by application startup.
BEGIN;
DELETE FROM "AspNetRoleClaims" claims
USING "AspNetRoles" roles
WHERE claims."RoleId" = roles."Id"
  AND roles."Name" = 'LabAdmin'
  AND claims."ClaimType" = 'permission'
  AND claims."ClaimValue" = 'plm.sample-request.workflow.lab';
COMMIT;
