using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class WorkflowNotificationClaimPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task WorkersSkipEachOthersClaimAndLeaveOtherTypesProcessedAndFailedRowsUntouched()
    {
        await using var first = await postgres.OpenAsync();
        await using (var setup = new NpgsqlCommand("""
            CREATE SCHEMA notification;
            CREATE TABLE notification.outbox_messages (id bigint PRIMARY KEY, type text NOT NULL, payload_json jsonb NOT NULL,
                created_at timestamp NOT NULL, processed_at timestamp NULL, attempts integer NOT NULL, error text NULL);
            INSERT INTO notification.outbox_messages VALUES
              (1,'InAppPush','{}',localtimestamp,NULL,0,NULL),
              (2,'SampleRequestWorkflowNotification','{}',localtimestamp,NULL,0,NULL),
              (3,'SampleWorkChanged','{}',localtimestamp,NULL,0,NULL),
              (4,'SampleRequestWorkflowNotification','{}',localtimestamp,localtimestamp,1,NULL),
              (5,'SampleRequestWorkflowNotification','{}',localtimestamp,NULL,5,'WorkflowNotification.Unknown'),
              (6,'UnknownFutureType','{}',localtimestamp,NULL,0,NULL);
            """, first)) await setup.ExecuteNonQueryAsync();
        await using var db1 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(first).Options);
        await using var tx1 = await db1.Database.BeginTransactionAsync();
        Assert.Equal(2, Assert.Single(await WorkflowNotificationJobQueries.ClaimPending(db1.OutboxMessages).ToListAsync()).Id);
        await using var second = await postgres.OpenAsync();
        await using var db2 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(second).Options);
        await using var tx2 = await db2.Database.BeginTransactionAsync();
        Assert.Equal(3, Assert.Single(await WorkflowNotificationJobQueries.ClaimPending(db2.OutboxMessages).ToListAsync()).Id);
        await using var third = await postgres.OpenAsync();
        await using var db3 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(third).Options);
        Assert.Empty(await WorkflowNotificationJobQueries.ClaimPending(db3.OutboxMessages).ToListAsync());
        var other = await db3.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == 6);
        Assert.Null(other.ProcessedAt);
        Assert.Null(other.Error);
        Assert.Equal(0, other.Attempts);
        await tx2.RollbackAsync();
        await tx1.RollbackAsync();
        Assert.Equal(2, Assert.Single(await WorkflowNotificationJobQueries.ClaimPending(db3.OutboxMessages).ToListAsync()).Id);
    }
}
