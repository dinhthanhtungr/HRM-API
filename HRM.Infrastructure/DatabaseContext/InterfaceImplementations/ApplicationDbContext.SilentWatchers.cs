using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.InternalMailEnums;
using Npgsql;
using NpgsqlTypes;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public Task<int> InsertMissingSilentWatchersAsync(Guid companyId, Guid conversationId, Guid[] employeeIds,
        DateTime joinedAt, CancellationToken cancellationToken)
        => Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InternalMail"."InternalConversationParticipants"
              ("InternalConversationId","EmployeeId","Role","IsActive","IsMuted","IsArchived","JoinedAt")
            SELECT c."InternalConversationId",e."EmployeeID",{(int)InternalConversationParticipantRole.Watcher},TRUE,TRUE,FALSE,{new NpgsqlParameter<DateTime> { TypedValue = DateTime.SpecifyKind(joinedAt, DateTimeKind.Unspecified), NpgsqlDbType = NpgsqlDbType.Timestamp }}
            FROM unnest({employeeIds}) candidate(id)
            JOIN hr."Employees" e ON e."EmployeeID"=candidate.id AND e."CompanyId"={companyId} AND e."IsActive"
            JOIN "InternalMail"."InternalConversations" c ON c."InternalConversationId"={conversationId}
              AND c."CompanyId"={companyId} AND c."IsActive" AND c."RelatedType"={(int)InternalMailRelatedType.SampleRequest}
            WHERE EXISTS (SELECT 1 FROM "SampleRequests"."SampleRequests" s
                WHERE s."SampleRequestId"=c."RelatedId" AND s."CompanyId"={companyId} AND s."IsActive")
            ON CONFLICT ("InternalConversationId","EmployeeId") DO NOTHING
            """, cancellationToken);
}
