-- SELECT-only. Re-run before an approved deployment; numbers in README are a dated snapshot.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '15s';
SET LOCAL lock_timeout = '1s';

SELECT current_setting('transaction_read_only') AS read_only,
       current_setting('transaction_isolation') AS isolation,
       to_regclass('"InternalMail"."InternalConversationGroups"') IS NOT NULL AS mapping_already_exists;

SELECT "RelatedType", count(*) AS conversations, count(*) FILTER (WHERE "IsActive") AS active,
       count(DISTINCT "CompanyId") AS companies
FROM "InternalMail"."InternalConversations" GROUP BY 1 ORDER BY 1;

SELECT count(*) FILTER (WHERE p."InternalConversationId" IS NULL) AS missing_parent,
       count(*) FILTER (WHERE c."CompanyId" <> p."CompanyId") AS cross_company,
       count(*) FILTER (WHERE c."InternalConversationId" = c."RelatedId") AS self_parent,
       count(*) FILTER (WHERE p."RelatedType" IN (17,18,19)) AS nested_child,
       count(*) FILTER (WHERE c."IsActive" AND NOT p."IsActive") AS active_under_inactive,
       count(*) FILTER (WHERE c."RelatedType" = 19 AND
         (btrim(c."Subject") = '' OR char_length(c."Subject") > 100)) AS invalid_custom_name
FROM "InternalMail"."InternalConversations" c
LEFT JOIN "InternalMail"."InternalConversations" p ON p."InternalConversationId" = c."RelatedId"
WHERE c."RelatedType" IN (17,18,19);

SELECT count(*) AS duplicate_active_default_sets FROM (
  SELECT "CompanyId", "RelatedId", "RelatedType"
  FROM "InternalMail"."InternalConversations"
  WHERE "RelatedType" IN (17,18) AND "IsActive"
  GROUP BY 1,2,3 HAVING count(*) > 1
) duplicates;

SELECT p."RelatedType" AS root_type, c."RelatedType" AS child_type, count(*) AS children
FROM "InternalMail"."InternalConversations" c
JOIN "InternalMail"."InternalConversations" p ON p."InternalConversationId" = c."RelatedId"
WHERE c."RelatedType" IN (17,18,19) GROUP BY 1,2 ORDER BY 1,2;

SELECT count(*) AS unsupported_conversation_types
FROM "InternalMail"."InternalConversations"
WHERE "RelatedType" IS NOT NULL AND "RelatedType" NOT IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,9999);

SELECT count(*) AS cross_company_members
FROM "InternalMail"."InternalConversationParticipants" p
JOIN "InternalMail"."InternalConversations" c USING ("InternalConversationId")
JOIN hr."Employees" e ON e."EmployeeID" = p."EmployeeId"
WHERE c."CompanyId" <> e."CompanyId";

-- No synthetic groups for roots without defaults; existence is not membership or authorization.
SELECT count(*) AS expected_mapping_rows FROM "InternalMail"."InternalConversations";
ROLLBACK;
-- GROUP-STORAGE-TRANSITION: audit-only — retain snapshots; see TRANSITION_CLEANUP.md before removing this tool.
