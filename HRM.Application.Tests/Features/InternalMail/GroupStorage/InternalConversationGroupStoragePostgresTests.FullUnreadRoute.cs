using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer")]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    public async Task FullProductionUnreadInboxRouteExecutesOnIsolatedPostgres(string role)
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "FullName" text DEFAULT 'Synthetic';
            CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequestWorkflowEvents"
              ("CompanyId" uuid,"MessageId" uuid,"EventType" text);
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL;
            UPDATE "InternalMail"."InternalConversationParticipants" SET "IsArchived"=false,"IsMuted"=false;
            """);
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var user = new UnreadPerformanceUser(role);
        var areas = new InternalMailAreaAccessService(db, user);
        var handler = new GetInternalConversationsQueryHandler(db, user,
            new InternalConversationSampleRequestInfoResolver(db), new InternalConversationQuotationInfoResolver(db), areas);
        var result = await handler.Handle(new GetInternalConversationsQuery
        {
            Archived = false, UnreadOnly = true, PageNumber = 1, PageSize = 50
        }, default);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.True(item.UnreadCount > 0));
        var durations = new List<double>();
        for (var sample = 0; sample < 5; sample++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var next = await handler.Handle(new GetInternalConversationsQuery
            {
                Archived = false, UnreadOnly = true, PageNumber = 1, PageSize = 50
            }, default);
            durations.Add(timer.Elapsed.TotalMilliseconds);
            Assert.Equal(result.Items.Select(i => i.ConversationId), next.Items.Select(i => i.ConversationId));
        }
        var report = new { role, samples = 5, rows = result.Items.Count,
            medianMs = durations.Order().ElementAt(2), minMs = durations.Min(), maxMs = durations.Max() };
        var destination = Path.Combine(postgres.RepositoryRoot, "artifacts", "full-unread-route");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, role + ".json"), System.Text.Json.JsonSerializer.Serialize(report));
    }
}
