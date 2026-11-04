-- Run only AFTER approved expansion on the clone or target. No writes, no handlers or mark-read.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '15s';
SET LOCAL lock_timeout = '1s';

-- Run this script as the real runtime role as well as the deployer before resuming API/workers.
SELECT has_table_privilege('"InternalMail"."InternalConversationGroups"', 'SELECT') AS can_read_mapping,
       has_table_privilege('"InternalMail"."InternalConversationGroups"', 'INSERT') AS bridge_can_register_mapping,
       has_table_privilege('"InternalMail"."InternalConversationGroups"', 'UPDATE') AS bridge_can_sync_mapping;

SELECT count(*) AS mapping_mismatches
FROM "InternalMail"."InternalConversations" c
FULL JOIN "InternalMail"."InternalConversationGroups" g ON g."ConversationId" = c."InternalConversationId"
WHERE c."InternalConversationId" IS NULL OR g."ConversationId" IS NULL
   OR g."CompanyId" IS DISTINCT FROM c."CompanyId" OR g."IsActive" IS DISTINCT FROM c."IsActive"
   OR g."GroupType" <> CASE c."RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 WHEN 19 THEN 3 ELSE 0 END
   OR g."RootConversationId" IS DISTINCT FROM CASE WHEN c."RelatedType" IN (17,18,19) THEN c."RelatedId" ELSE c."InternalConversationId" END;

SELECT count(*) AS invalid_roots FROM "InternalMail"."InternalConversationGroups" child
LEFT JOIN "InternalMail"."InternalConversationGroups" root
  ON root."ConversationId" = child."RootConversationId" AND root."CompanyId" = child."CompanyId"
WHERE root."ConversationId" IS NULL OR root."GroupType" <> 0;

SELECT "GroupType", count(*) AS total, count(*) FILTER (WHERE "IsActive") AS active
FROM "InternalMail"."InternalConversationGroups" GROUP BY 1 ORDER BY 1;

SELECT count(*) AS custom_name_mismatches
FROM "InternalMail"."InternalConversationGroups" g
JOIN "InternalMail"."InternalConversations" c ON c."InternalConversationId" = g."ConversationId"
WHERE g."GroupType" = 3 AND g."DisplayName" IS DISTINCT FROM c."Subject";

-- These are mapping integrity checks, not an assertion of UI badge/permission equivalence.
-- Compare existing/new runtime queries under the SAME principal and snapshot before cutover.
ROLLBACK;
-- GROUP-STORAGE-TRANSITION: audit-only — verification does not mutate messages, membership or badge state.
