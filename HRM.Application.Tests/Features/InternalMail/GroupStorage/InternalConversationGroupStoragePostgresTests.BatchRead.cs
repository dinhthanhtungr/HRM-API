using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task BatchReadReturnsOnlyVisibleSnapshotIdsAndKeepsNewArrivalsUnread()
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync($$"""
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
              SELECT ('a0000000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid,
                CASE WHEN i=4 THEN '10000000-0000-0000-0000-000000000002'::uuid ELSE '{{Company}}'::uuid END,
                CASE WHEN i=7 THEN jsonb_build_object('conversationId','70000000-0000-4000-8000-000000000007') END,
                '2026-11-02',1 FROM generate_series(1,7) i;
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read,is_archived)
              SELECT ('a0000000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid,
                CASE WHEN i=3 THEN '{{MissingPresident}}'::uuid ELSE '{{Employee}}'::uuid END,false,i=5
              FROM generate_series(1,7) i;
            """);
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var user = new UnreadPerformanceUser("SaleUser");
        var areas = new InternalMailAreaAccessService(db, user);
        var service = new NotificationService(db, user, null!, null!, areas, null!);
        var ids = Enumerable.Range(1, 7).Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i:D12}")).ToArray();
        var requested = ids.Take(5).Append(ids[6]).Append(ids[0]).ToArray();
        var expected = ids.Take(2).ToArray();
        Assert.Equal(expected, (await service.MarkBatchReadAsync(requested)).Order().ToArray());
        Assert.Equal(expected, (await service.MarkBatchReadAsync(requested)).Order().ToArray());
        Assert.Equal(expected, await db.NotificationUserStates.Where(s => ids.Contains(s.NotificationId) && s.IsRead)
            .OrderBy(s => s.NotificationId).Select(s => s.NotificationId).ToArrayAsync());
    }
}

