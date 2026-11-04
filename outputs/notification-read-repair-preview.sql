-- Read-only preview: only notifications linked to a message already read by the same employee.
-- No LastReadAt inference: sending a message can also update that conversation field.
-- Required psql argument: -v company_id=<approved-company-uuid>
\set ON_ERROR_STOP on
BEGIN READ ONLY;
SET LOCAL statement_timeout = '30s';
WITH payload AS (
    SELECT s.notification_id, s.user_id, s.is_read, s.read_date, n.company_id, n.topic,
        coalesce(n.payload_json->>'conversationId', n.payload_json->>'ConversationId') AS cid,
        coalesce(n.payload_json->>'messageId', n.payload_json->>'MessageId') AS mid
    FROM notification.notification_user_states s
    JOIN notification.notifications n ON n.id = s.notification_id
    WHERE NOT s.is_read AND NOT s.is_archived
        AND n.company_id = :'company_id'::uuid
        AND jsonb_typeof(n.payload_json) = 'object'
), linked AS (
    SELECT *,
        CASE WHEN cid ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN cid::uuid END AS conversation_id,
        CASE WHEN mid ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN mid::uuid END AS message_id
    FROM payload
)
SELECT l.company_id, l.user_id, l.notification_id, l.topic, l.conversation_id, l.message_id,
    l.is_read AS old_is_read, l.read_date AS old_read_date, r."ReadAt" AS message_read_at,
    count(*) OVER () AS total_candidates
FROM linked l
JOIN "InternalMail"."InternalConversations" c
    ON c."InternalConversationId" = l.conversation_id AND c."CompanyId" = l.company_id AND c."IsActive"
JOIN "InternalMail"."InternalConversationParticipants" p
    ON p."InternalConversationId" = l.conversation_id AND p."EmployeeId" = l.user_id AND p."IsActive"
JOIN "InternalMail"."InternalMessages" m
    ON m."InternalMessageId" = l.message_id AND m."InternalConversationId" = l.conversation_id AND NOT m."IsDeleted"
JOIN "InternalMail"."InternalMessageReadStates" r
    ON r."InternalMessageId" = m."InternalMessageId" AND r."EmployeeId" = l.user_id AND r."IsRead"
ORDER BY l.user_id, l.notification_id;
COMMIT;
