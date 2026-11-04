-- GROUP-STORAGE-TRANSITION: read-only migration audit. Never includes message bodies or employee names.
-- The runner substitutes __MAPPING_SOURCE__ with one of two fixed, reviewed SELECTs, never user input.
WITH mapping AS (__MAPPING_SOURCE__),
counts AS (
 SELECT "InternalConversationId", count(*) AS total_messages,
        count(*) FILTER (WHERE NOT "IsDeleted") AS visible_messages,
        count(*) FILTER (WHERE "IsDeleted") AS deleted_messages
 FROM "InternalMail"."InternalMessages" GROUP BY 1
), rows AS (
 SELECT c.*, CASE WHEN c."RelatedType" IN (17,18,19) THEN c."RelatedId" ELSE c."InternalConversationId" END AS root_id,
        CASE c."RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 WHEN 19 THEN 3 ELSE 0 END AS group_type
 FROM "InternalMail"."InternalConversations" c WHERE c."RelatedType" IS DISTINCT FROM 20
)
SELECT jsonb_build_object(
 'root_id',root."InternalConversationId", 'conversation_id',c."InternalConversationId", 'company_id',c."CompanyId",
 'request_code',coalesce(s."ExternalId",root."RelatedExternalId",root."Subject"), 'colour_code',p."ColourCode",
 'group_type',c.group_type,'group_name',CASE c.group_type WHEN 0 THEN 'Chung' WHEN 1 THEN 'Kỹ thuật' WHEN 2 THEN 'Giá' ELSE c."Subject" END,
 'group_active',c."IsActive",'root_active',root."IsActive",
 'total_messages',coalesce(n.total_messages,0),'visible_messages',coalesce(n.visible_messages,0),'deleted_messages',coalesce(n.deleted_messages,0),
 'mapping_exists',m."ConversationId" IS NOT NULL,
 'mapping_matches',m."ConversationId" IS NOT NULL AND m."CompanyId"=c."CompanyId" AND m."RootConversationId"=c.root_id
      AND m."GroupType"=c.group_type AND m."IsActive"=c."IsActive",
 'stored_name',m."DisplayName")::text
FROM rows c
JOIN "InternalMail"."InternalConversations" root ON root."InternalConversationId"=c.root_id AND root."CompanyId"=c."CompanyId"
LEFT JOIN "SampleRequests"."SampleRequests" s ON s."SampleRequestId"=root."RelatedId" AND s."CompanyId"=root."CompanyId"
LEFT JOIN "SampleRequests"."Products" p ON p."ProductId"=s."ProductId" AND p."CompanyId"=root."CompanyId"
LEFT JOIN counts n ON n."InternalConversationId"=c."InternalConversationId"
LEFT JOIN mapping m ON m."ConversationId"=c."InternalConversationId"
WHERE root."RelatedType"=5
ORDER BY root."CreatedAt" DESC,root."InternalConversationId",c.group_type,c."InternalConversationId"
LIMIT 20000;
