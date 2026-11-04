using HRM.Application.Abstractions.Persistence.InternalMail;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
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
                ON c."InternalConversationId"::text = lower(link.value) AND c."CompanyId" = {{companyId}}
            WHERE n.company_id = {{companyId}}
            """);

    public IQueryable<InternalConversationNotificationUnreadCount> QueryInternalConversationNotificationUnreadCounts(
        Guid companyId, Guid employeeId, Guid[]? conversationIds = null, bool allowCompanyObserver = false)
        => Database.SqlQuery<InternalConversationNotificationUnreadCount>($"""
            SELECT conversation."InternalConversationId" AS "ConversationId", COUNT(*)::int AS "UnreadCount"
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
                ON conversation."InternalConversationId"::text = lower(link.value)
            WHERE state.user_id = {employeeId}
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
