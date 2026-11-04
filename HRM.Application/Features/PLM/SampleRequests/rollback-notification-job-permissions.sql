-- Run only to reverse seed-notification-job-permissions.sql when these claims were introduced by that seed.
BEGIN;
DELETE FROM "AspNetRoleClaims" c USING "AspNetRoles" r
WHERE c."RoleId"=r."Id" AND r."Name"='Developer' AND c."ClaimType"='permission'
 AND c."ClaimValue" IN ('dev.workflow-notification-jobs.view', 'dev.workflow-notification-jobs.retry');
COMMIT;
