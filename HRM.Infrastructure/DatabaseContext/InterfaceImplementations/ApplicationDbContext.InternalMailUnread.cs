using HRM.Application.Abstractions.Persistence.InternalMail;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    private const string InternalMailGuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
    public async Task LockInternalConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        // Same lock order as the synchronization API/live writer: company first, conversation row second.
        await HRM.Infrastructure.Services.InternalMail.InternalConversationGroupStore.LockCompanyAsync(this, companyId, cancellationToken);
        await Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "InternalConversationId" FROM "InternalMail"."InternalConversations"
            WHERE "CompanyId" = {companyId} AND "InternalConversationId" = {conversationId} FOR UPDATE
            """, cancellationToken);
    }

    public IQueryable<InternalMailNotificationLink> QueryInternalMailNotificationLinks(Guid companyId)
        => Database.SqlQuery<InternalMailNotificationLink>($$"""
            SELECT n.id AS "NotificationId", c."InternalConversationId" AS "ConversationId",
                (link.value IS NOT NULL) AS "HasConversation",
                EXISTS (SELECT 1 FROM jsonb_each(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                    THEN n.payload_json ELSE jsonb_build_object() END) property
                    WHERE lower(property.key) = 'messageid' AND property.value <> 'null'::jsonb) AS "HasMessageValue",
                CASE WHEN message_link.value ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                    THEN message_link.value::uuid ELSE NULL END AS "MessageId"
            FROM notification.notifications n
            LEFT JOIN LATERAL (
                SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                    THEN n.payload_json ELSE jsonb_build_object() END) metadata
                WHERE lower(metadata.key) = 'conversationid' LIMIT 1
            ) link ON TRUE
            LEFT JOIN LATERAL (
                SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                    THEN n.payload_json ELSE jsonb_build_object() END) metadata
                WHERE lower(metadata.key) = 'messageid' LIMIT 1
            ) message_link ON TRUE
            LEFT JOIN "InternalMail"."InternalConversations" c
                ON c."InternalConversationId" = CASE
                    WHEN link.value ~* {{InternalMailGuidPattern}} THEN link.value::uuid ELSE NULL END
                    AND c."CompanyId" = {{companyId}}
            WHERE n.company_id = {{companyId}}
            """);

    // The caller supplies message keys from the authorized page. A delivery only
    // represents that message if both its payload keys match and this reader has a state.
    public IQueryable<Guid> QueryRepresentedInternalMessageIds(Guid companyId, Guid employeeId, Guid[] messageIds)
        => Database.SqlQuery<Guid>($"""
            WITH delivered AS MATERIALIZED (
                SELECT DISTINCT lower(link.value) AS conversation_key, lower(message_link.value) AS message_key
                FROM notification.notification_user_states s
                JOIN notification.notifications n ON n.id = s.notification_id AND n.company_id = {companyId}
                JOIN LATERAL (
                    SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                        THEN n.payload_json ELSE jsonb_build_object() END) metadata
                    WHERE lower(metadata.key) = 'messageid' LIMIT 1
                ) message_link ON TRUE
                JOIN LATERAL (
                    SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                        THEN n.payload_json ELSE jsonb_build_object() END) metadata
                    WHERE lower(metadata.key) = 'conversationid' LIMIT 1
                ) link ON TRUE
                WHERE s.user_id = {employeeId}
            )
            SELECT m."InternalMessageId" AS "Value"
            FROM "InternalMail"."InternalMessages" m
            JOIN "InternalMail"."InternalConversations" c ON c."InternalConversationId" = m."InternalConversationId"
                AND c."CompanyId" = {companyId}
            JOIN delivered ON delivered.conversation_key = c."InternalConversationId"::text
                AND delivered.message_key = m."InternalMessageId"::text
            WHERE m."InternalMessageId" = ANY({messageIds})
            """);

    public IQueryable<InternalMailBusinessEventRow> QueryLatestInternalMailBusinessEvents(Guid companyId, Guid[] conversationIds, int[] topics)
        => Database.SqlQuery<InternalMailBusinessEventRow>($$"""
            SELECT DISTINCT ON (m."InternalConversationId") n.id AS "Id", m."InternalConversationId" AS "ConversationId",
                n.created_date AS "CreatedDate", n.topic AS "Topic"
            FROM notification.notifications n
            JOIN LATERAL (
                SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                    THEN n.payload_json ELSE jsonb_build_object() END) metadata
                WHERE lower(metadata.key) = 'conversationid' LIMIT 1
            ) link ON TRUE
            JOIN LATERAL (
                SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json) = 'object'
                    THEN n.payload_json ELSE jsonb_build_object() END) metadata
                WHERE lower(metadata.key) = 'messageid' LIMIT 1
            ) message_link ON TRUE
            JOIN "InternalMail"."InternalMessages" m ON m."InternalMessageId" = CASE
                WHEN message_link.value ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                THEN message_link.value::uuid ELSE NULL END
                AND m."InternalConversationId"::text = lower(link.value)
                AND NOT m."IsDeleted" AND m."InternalConversationId" = ANY({{conversationIds}})
            JOIN "InternalMail"."InternalConversations" c ON c."InternalConversationId" = m."InternalConversationId"
                AND c."CompanyId" = {{companyId}}
            WHERE n.company_id = {{companyId}} AND n.topic = ANY({{topics}})
            ORDER BY m."InternalConversationId", n.created_date DESC, n.id DESC
            """);

    public IQueryable<Guid> QueryQuotationBadgeSuppressedNotificationIds(Guid companyId, Guid employeeId)
        => Database.SqlQuery<Guid>($"""
            WITH candidate AS (
                SELECT n.id, m."InternalMessageId" AS message_id,
                    row_number() OVER (PARTITION BY m."InternalMessageId" ORDER BY n.created_date DESC,n.id DESC) AS rank
                FROM notification.notification_user_states state
                JOIN notification.notifications n ON n.id=state.notification_id AND n.company_id={companyId}
                JOIN LATERAL (
                    SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json)='object'
                        THEN n.payload_json ELSE jsonb_build_object() END) metadata
                    WHERE lower(metadata.key)='conversationid' LIMIT 1
                ) link ON TRUE
                JOIN "InternalMail"."InternalConversations" c ON c."InternalConversationId"=CASE
                    WHEN link.value ~* {InternalMailGuidPattern} THEN link.value::uuid ELSE NULL END
                    AND c."CompanyId"={companyId} AND c."IsActive"
                LEFT JOIN "InternalMail"."InternalConversations" parent ON parent."InternalConversationId"=c."RelatedId"
                    AND parent."CompanyId"={companyId} AND parent."IsActive"
                    AND parent."RelatedType"={(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation}
                    AND c."RelatedType"=ANY({new[] { (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical, (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing, (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom }})
                LEFT JOIN LATERAL (
                    SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(n.payload_json)='object'
                        THEN n.payload_json ELSE jsonb_build_object() END) metadata
                    WHERE lower(metadata.key)='messageid' LIMIT 1
                ) message_link ON TRUE
                LEFT JOIN "InternalMail"."InternalMessages" m ON m."InternalMessageId"=CASE
                    WHEN message_link.value ~* {InternalMailGuidPattern} THEN message_link.value::uuid ELSE NULL END
                    AND m."InternalConversationId"=c."InternalConversationId" AND NOT m."IsDeleted" AND m."SenderEmployeeId"<>{employeeId}
                WHERE state.user_id={employeeId} AND NOT state.is_read AND NOT state.is_archived
                    AND n.topic<>ALL({HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null,"reaction").Select(t=>(int)t).ToArray()})
                    AND (c."RelatedType"={(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation} OR parent."InternalConversationId" IS NOT NULL)
            )
            SELECT id AS "Value" FROM candidate WHERE message_id IS NULL OR rank<>1
            """);

    public IQueryable<InternalConversationNotificationUnreadCount> QueryInternalConversationNotificationUnreadCounts(
        Guid companyId, Guid employeeId, Guid[]? conversationIds = null, bool allowCompanyObserver = false)
        => Database.SqlQuery<InternalConversationNotificationUnreadCount>($"""
            SELECT conversation."InternalConversationId" AS "ConversationId", COUNT(DISTINCT CASE WHEN
                (conversation."RelatedType" = {(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation} OR quote_parent."InternalConversationId" IS NOT NULL)
                THEN message."InternalMessageId" ELSE notification.id END)::int AS "UnreadCount"
            FROM notification.notification_user_states AS state
            JOIN notification.notifications AS notification ON notification.id = state.notification_id
            JOIN LATERAL (
                SELECT metadata.value
                FROM jsonb_each_text(CASE WHEN jsonb_typeof(notification.payload_json) = 'object'
                    THEN notification.payload_json ELSE jsonb_build_object() END) AS metadata
                WHERE lower(metadata.key) = 'conversationid'
                LIMIT 1
            ) AS link ON TRUE
            JOIN "InternalMail"."InternalConversations" AS conversation
                ON conversation."InternalConversationId" = CASE
                    WHEN link.value ~* {InternalMailGuidPattern} THEN link.value::uuid ELSE NULL END
            LEFT JOIN "InternalMail"."InternalConversations" quote_parent
                ON conversation."RelatedId" = quote_parent."InternalConversationId"
                AND conversation."RelatedType" = ANY({new[] { (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical, (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing, (int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom }})
                AND quote_parent."CompanyId" = {companyId} AND quote_parent."IsActive"
                AND quote_parent."RelatedType" = {(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation}
            LEFT JOIN LATERAL (
                SELECT metadata.value FROM jsonb_each_text(CASE WHEN jsonb_typeof(notification.payload_json) = 'object'
                    THEN notification.payload_json ELSE jsonb_build_object() END) metadata
                WHERE (conversation."RelatedType" = {(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation}
                    OR quote_parent."InternalConversationId" IS NOT NULL)
                    AND lower(metadata.key) = 'messageid' LIMIT 1
            ) message_link ON TRUE
            LEFT JOIN "InternalMail"."InternalMessages" message
                ON message."InternalMessageId" = CASE
                    WHEN message_link.value ~* {InternalMailGuidPattern} THEN message_link.value::uuid ELSE NULL END
                AND message."InternalConversationId" = conversation."InternalConversationId"
                AND NOT message."IsDeleted" AND message."SenderEmployeeId" <> {employeeId}
            WHERE state.user_id = {employeeId}
                AND ((conversation."RelatedType" IS DISTINCT FROM {(int)HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation}
                    AND quote_parent."InternalConversationId" IS NULL) OR message."InternalMessageId" IS NOT NULL)
                AND NOT state.is_read AND NOT state.is_archived
                AND notification.company_id = {companyId}
                AND notification.topic <> ALL({HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "reaction").Select(t => (int)t).ToArray()})
                AND conversation."CompanyId" = {companyId}
                AND conversation."IsActive"
                AND ({conversationIds == null} OR conversation."InternalConversationId" = ANY({conversationIds ?? Array.Empty<Guid>()}))
                AND ({allowCompanyObserver} OR EXISTS (
                    SELECT 1 FROM "InternalMail"."InternalConversationParticipants" AS participant
                    WHERE participant."InternalConversationId" = conversation."InternalConversationId"
                        AND participant."EmployeeId" = {employeeId}
                        AND participant."IsActive"
                ))
            GROUP BY conversation."InternalConversationId"
            """);
}
