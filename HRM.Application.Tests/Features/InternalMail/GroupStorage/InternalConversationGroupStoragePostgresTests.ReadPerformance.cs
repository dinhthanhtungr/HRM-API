using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer", 100)]
    [InlineData("Developer", 1000)]
    [InlineData("SaleUser", 1000)]
    [InlineData("LabUser", 1000)]
    [InlineData("President", 1000)]
    public async Task ReadLargeHistoryPreservesBoundaryAndOtherGroups(string role, int unread)
    {
        await PresidentBusinessSchemaAsync();
        // Synthetic, guarded loopback cluster only. No application configuration is read.
        await postgres.ExecuteAsync($$"""
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL;
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body")
              VALUES ('f0000000-0000-0000-0000-000000000001','{{Technical}}','{{MissingPresident}}','Unopened');
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
              VALUES ('f0000000-0000-0000-0000-000000000001','{{Employee}}');
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
            SELECT md5('read-perf-' || i)::uuid,'{{Root}}','{{MissingPresident}}','Synthetic read performance',
              '2026-11-01'::timestamp + i * interval '1 second' FROM generate_series(1,20001) i;
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId","IsRead")
            SELECT md5('read-perf-' || i)::uuid,'{{Employee}}',i <= 20000-{{unread}}
              FROM generate_series(1,20001) i;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            SELECT md5('read-perf-event-' || i || '-' || j)::uuid,'{{Company}}',
              jsonb_build_object('conversationId','{{Root}}','messageId',md5('read-perf-' || i)::uuid),
              '2026-11-02'::timestamp + i * interval '1 second',38
              FROM generate_series(20001-{{unread}},20001) i CROSS JOIN generate_series(1,8) j;
            INSERT INTO notification.notification_user_states (notification_id,user_id)
            SELECT n.id,'{{Employee}}' FROM notification.notifications n WHERE created_date >= '2026-11-02';
            ANALYZE;
            """);
        await using var connection = await postgres.OpenAsync();
        var metrics = new ReadCommandMetrics();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(metrics).Options);
        db.Database.SetCommandTimeout(60);
        var user = new UnreadPerformanceUser(role);
        var handler = new MarkInternalConversationReadCommandHandler(db, user, new ReadPerformanceClock(),
            new InternalMailAreaAccessService(db, user));
        var baseline = new Baselines.BeforeBoundedMarkConversationReadHandler(db, user, new ReadPerformanceClock(),
            new InternalMailAreaAccessService(db, user));
        var command = new MarkInternalConversationReadCommand
        {
            ConversationId = Guid.Parse(Root),
            ThroughMessageId = await db.InternalMessages.Where(m => m.Body == "Synthetic read performance")
                .OrderByDescending(m => m.SentAt).Skip(1).Select(m => m.InternalMessageId).FirstAsync()
        };
        var durations = new List<double>();
        var queryCounts = new List<int>();
        var before = new List<double>();
        var beforeQueries = new List<int>();
        string? expectedState = null;
        for (var sample = -2; sample < 10; sample++)
        {
            await postgres.ExecuteAsync($$"""
                UPDATE "InternalMail"."InternalMessageReadStates" s SET "IsRead"=false,"ReadAt"=NULL
                  FROM "InternalMail"."InternalMessages" m
                  WHERE m."InternalMessageId"=s."InternalMessageId" AND m."Body"='Synthetic read performance'
                    AND m."SentAt">='2026-11-01'::timestamp + (20001-{{unread}})*interval '1 second';
                UPDATE notification.notification_user_states s SET is_read=false,read_date=NULL
                  FROM notification.notifications n WHERE n.id=s.notification_id AND n.created_date>='2026-11-02';
                """);
            metrics.Count = 0;
            var timer = Stopwatch.StartNew();
            var isBaseline = sample % 2 == 0;
            Assert.True((await (isBaseline ? baseline.Handle(command, default) : handler.Handle(command, default))).Success);
            timer.Stop();
            if (sample >= 0)
            {
                (isBaseline ? before : durations).Add(timer.Elapsed.TotalMilliseconds);
                (isBaseline ? beforeQueries : queryCounts).Add(metrics.Count);
            }
            var actualState = await ScalarAsync<string>("""
                SELECT jsonb_agg(row_to_json(s) ORDER BY notification_id,user_id)::text
                FROM notification.notification_user_states s
                """);
            expectedState ??= actualState;
            Assert.Equal(expectedState, actualState);
            Assert.Equal(8, await db.NotificationUserStates.CountAsync(s => !s.IsRead &&
                s.Notification.CreatedDate >= new DateTime(2026, 11, 2)));
            Assert.Equal(1, await db.InternalMessageReadStates.CountAsync(s => !s.IsRead &&
                s.Message.Body == "Synthetic read performance"));
            // Every duplicate notification of the acknowledged messages must be read.
            Assert.Equal(unread * 8, await db.NotificationUserStates.CountAsync(s => s.IsRead &&
                s.Notification.CreatedDate >= new DateTime(2026, 11, 2)));
            Assert.True(await db.InternalMessageReadStates.AnyAsync(s => !s.IsRead &&
                s.Message.InternalConversationId == Guid.Parse(Technical)));
        }
        var directory = Path.Combine(postgres.RepositoryRoot, "artifacts", "notification-read-performance");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"compare-{role}-{unread}.json"),
            JsonSerializer.Serialize(new { benchmark = "before-bounded-scope", role, history = 20000, unread, notificationRows = unread * 8,
                baseline = new { samples = before, medianMs = Median(before), queryCounts = beforeQueries },
                optimized = new { samples = durations, medianMs = Median(durations), queryCounts },
                boundaryAndCountsCorrect = true }));
    }

    private sealed class ReadPerformanceClock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 11, 26, 12, 0, 0);
    }

    private sealed class ReadCommandMetrics : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }
}
