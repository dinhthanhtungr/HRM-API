using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    /// <summary>Repairs missing recipient states while acknowledging only an authorized message snapshot.</summary>
    public Task<int> UpsertInternalMessageReadStatesAsync(
        Guid companyId, Guid employeeId, Guid conversationId, Guid[] messageIds,
        DateTime readAt, CancellationToken cancellationToken = default)
    {
        if (messageIds.Length == 0) return Task.FromResult(0);
        // Raw SQL does not inherit ConfigureConventions' timestamp-without-time-zone mapping.
        // Preserve the application's local wall-clock time instead of inferring timestamptz.
        var readAtParameter = new NpgsqlParameter("read_at", NpgsqlDbType.Timestamp) { Value = readAt };
        // ExecuteSqlInterpolated parameterizes all values, including the UUID array.
        // ON CONFLICT makes parallel reads safe without losing an existing read timestamp.
        return Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InternalMail"."InternalMessageReadStates" AS existing
                ("InternalMessageId", "EmployeeId", "IsRead", "ReadAt")
            SELECT message."InternalMessageId", {employeeId}, TRUE, {readAtParameter}
            FROM "InternalMail"."InternalMessages" AS message
            JOIN "InternalMail"."InternalConversations" AS conversation
                ON conversation."InternalConversationId" = message."InternalConversationId"
            WHERE message."InternalMessageId" = ANY({messageIds})
                AND message."InternalConversationId" = {conversationId}
                AND NOT message."IsDeleted"
                AND conversation."CompanyId" = {companyId}
                AND conversation."IsActive"
                AND EXISTS (
                    SELECT 1 FROM "InternalMail"."InternalConversationParticipants" AS participant
                    WHERE participant."InternalConversationId" = conversation."InternalConversationId"
                        AND participant."EmployeeId" = {employeeId}
                        AND participant."IsActive"
                )
            ON CONFLICT ("InternalMessageId", "EmployeeId")
            DO UPDATE SET "IsRead" = TRUE,
                "ReadAt" = COALESCE(existing."ReadAt", EXCLUDED."ReadAt")
            WHERE NOT existing."IsRead";
            """, cancellationToken);
    }
}
