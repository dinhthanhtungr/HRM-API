using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task SampleCanonicalGuardedUuidJoinPreservesLinksAndMeasuresFullHandler()
        => await MeasureSampleAllAsync("LabUser", true, 64, compareUuidJoin: true);

    private static async Task CompareSampleUuidJoinAsync(ApplicationDbContext db,
        GetInternalConversationsQueryHandler handler, GetInternalConversationsQuery request,
        SampleUuidJoinInterceptor rewrite, string repositoryRoot)
    {
        var company = Guid.Parse(Company);
        var employee = Guid.Parse(Employee);
        // Synthetic payload corpus, including formats PostgreSQL's uuid parser accepts but the old text join rejects.
        const string corpusSql = """
            WITH target AS (SELECT md5('sample-root-1')::uuid AS id), corpus AS (
              SELECT 1 AS n,jsonb_build_object('conversationId',id::text) AS payload FROM target UNION ALL
              SELECT 2,jsonb_build_object('conversationId',upper(id::text)) FROM target UNION ALL
              SELECT 3,jsonb_build_object('conversationId',' '||id::text) FROM target UNION ALL
              SELECT 4,jsonb_build_object('conversationId',id::text||chr(10)) FROM target UNION ALL
              SELECT 5,jsonb_build_object('conversationId',id::text||chr(9)) FROM target UNION ALL
              SELECT 6,jsonb_build_object('conversationId','{'||id::text||'}') FROM target UNION ALL
              SELECT 7,jsonb_build_object('conversationId',replace(id::text,'-','')) FROM target UNION ALL
              SELECT 8,'{"conversationId":"invalid"}'::jsonb UNION ALL
              SELECT 9,'{"conversationId":""}'::jsonb UNION ALL
              SELECT 10,'{"conversationId":null}'::jsonb UNION ALL
              SELECT 11,'{}'::jsonb UNION ALL SELECT 12,'null'::jsonb UNION ALL
              SELECT 13,'true'::jsonb UNION ALL SELECT 14,'[]'::jsonb UNION ALL
              SELECT 15,'{"conversationId":true}'::jsonb UNION ALL
              SELECT 16,'{"conversationId":{}}'::jsonb UNION ALL
              SELECT 17,'{"conversationId":"00000000-0000-0000-0000-000000000000"}'::jsonb UNION ALL
              SELECT 18,jsonb_build_object('ConversationId',id::text,'conversationId','invalid') FROM target UNION ALL
              SELECT 19,jsonb_build_object('CONVERSATIONID',upper(id::text),'messageId',upper(md5(id::text||'-message')::uuid::text)) FROM target
            )
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            SELECT md5('uuid-corpus-'||n)::uuid,'10000000-0000-0000-0000-000000000001',payload,'2026-11-05',38 FROM corpus;
            INSERT INTO notification.notification_user_states (notification_id,user_id)
            SELECT id,'00000000-0000-0000-0000-000000000001' FROM notification.notifications
              WHERE id IN (SELECT md5('uuid-corpus-'||i)::uuid FROM generate_series(1,19) AS i);
            """;
        await using (var corpus = new Npgsql.NpgsqlCommand(corpusSql, (Npgsql.NpgsqlConnection)db.Database.GetDbConnection()))
            await corpus.ExecuteNonQueryAsync();
        rewrite.Enabled = false;
        rewrite.CheckHistoricalLinks = true;
        var oldLinks = await db.QueryInternalMailNotificationLinks(company).OrderBy(l => l.NotificationId).ToArrayAsync();
        rewrite.Enabled = true;
        var newLinks = await db.QueryInternalMailNotificationLinks(company).OrderBy(l => l.NotificationId).ToArrayAsync();
        Assert.Equal(JsonSerializer.Serialize(oldLinks), JsonSerializer.Serialize(newLinks));
        var messages = await db.InternalMessages.AsNoTracking().OrderBy(m => m.InternalMessageId)
            .Take(100).Select(m => new { m.InternalMessageId, m.InternalConversationId }).ToArrayAsync();
        var ids = messages.Select(m => m.InternalMessageId).ToArray();
        foreach (var scopeCompany in new[] { company, Guid.Empty })
        foreach (var reader in new[] { employee, Guid.Empty })
        {
            var receipts = await db.NotificationUserStates.Where(s => s.UserId == reader && s.Notification.CompanyId == scopeCompany)
                .Select(s => s.NotificationId).ToArrayAsync();
            var delivered = receipts.ToHashSet();
            var pairs = oldLinks.Where(l => delivered.Contains(l.NotificationId))
                .Select(l => (l.ConversationId, l.MessageId)).ToHashSet();
            var expected = messages.Where(m => pairs.Contains((m.InternalConversationId, m.InternalMessageId)))
                .Select(m => m.InternalMessageId).Order().ToArray();
            var actual = await db.QueryRepresentedInternalMessageIds(scopeCompany, reader, ids).Order().ToArrayAsync();
            Assert.Equal(expected, actual);
        }
        foreach (var observer in new[] { false, true })
        foreach (var scopeCompany in new[] { company, Guid.Parse("10000000-0000-0000-0000-000000000002") })
        {
            rewrite.Enabled = false;
            var oldCounts = await db.QueryInternalConversationNotificationUnreadCounts(scopeCompany, employee,
                allowCompanyObserver: observer).OrderBy(c => c.ConversationId).ToArrayAsync();
            rewrite.Enabled = true;
            var newCounts = await db.QueryInternalConversationNotificationUnreadCounts(scopeCompany, employee,
                allowCompanyObserver: observer).OrderBy(c => c.ConversationId).ToArrayAsync();
            Assert.Equal(JsonSerializer.Serialize(oldCounts), JsonSerializer.Serialize(newCounts));
        }
        rewrite.Enabled = false;
        Assert.True(rewrite.RewrittenCommands > 0);
        rewrite.CheckHistoricalLinks = false;
        var linkCommands = rewrite.RewrittenCommands;
        var oldResult = await handler.Handle(request, default);
        rewrite.Enabled = true;
        var newResult = await handler.Handle(request, default);
        Assert.Equal(JsonSerializer.Serialize(oldResult), JsonSerializer.Serialize(newResult));
        var oldTimes = new List<double>();
        var newTimes = new List<double>();
        for (var sample = 0; sample < 5; sample++)
        {
            foreach (var guarded in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                rewrite.Enabled = guarded;
                var timer = Stopwatch.StartNew();
                var result = await handler.Handle(request, default);
                (guarded ? newTimes : oldTimes).Add(timer.Elapsed.TotalMilliseconds);
                Assert.Equal(JsonSerializer.Serialize(oldResult), JsonSerializer.Serialize(result));
            }
        }
        var destination = Path.Combine(repositoryRoot, "artifacts", "sample-all-performance", "guarded-uuid-join.json");
        Assert.True(rewrite.RewrittenCommands > 0);
        await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new
        {
            samples = 5, roles = "synthetic LabUser", notifications = 38419,
            oldJoin = new { medianMs = oldTimes.Order().ElementAt(2), minMs = oldTimes.Min(), maxMs = oldTimes.Max() },
            guardedJoin = new { medianMs = newTimes.Order().ElementAt(2), minMs = newTimes.Min(), maxMs = newTimes.Max() },
            linkParity = true, countParity = true, presentationParity = true, rewrittenCommands = rewrite.RewrittenCommands
        }));
    }

    private sealed class SampleUuidJoinInterceptor : DbCommandInterceptor
    {
        public bool CheckHistoricalLinks { get; set; }
        public bool Enabled { get; set; }
        public int RewrittenCommands { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (CheckHistoricalLinks && !Enabled)
            {
                var historical = System.Text.RegularExpressions.Regex.Replace(command.CommandText,
                    """(c|conversation)\."InternalConversationId"\s*=\s*CASE\s+WHEN link\.value ~\* @\w+ THEN link\.value::uuid ELSE NULL END""",
                    """$1."InternalConversationId"::text = lower(link.value)""");
                if (historical != command.CommandText) RewrittenCommands++;
                command.CommandText = historical;
            }
            // Run the guarded alternative against the same data as the production text join.
            if (!CheckHistoricalLinks && Enabled)
            {
                var text = command.CommandText.Replace(
                    """delivered.conversation_key = c."InternalConversationId"::text""",
                    """c."InternalConversationId" = CASE WHEN delivered.conversation_key ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN delivered.conversation_key::uuid ELSE NULL END""");
                if (text != command.CommandText) RewrittenCommands++;
                command.CommandText = text;
            }
            return ValueTask.FromResult(result);
        }
    }
}
