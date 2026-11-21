using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task SampleCanonicalRankedScalarIconsPreserveWinnersAndMeasureQueries()
        => await MeasureSampleAllAsync("LabUser", true, 64, compareIconRanking: true);

    private static async Task CompareSampleIconRankingAsync(ApplicationDbContext db, InternalMailAreaAccessService areas,
        GetInternalConversationsQueryHandler handler, GetInternalConversationsQuery request, Guid[] pageIds, string repositoryRoot)
    {
        // Vary business topics so winner parity cannot pass merely because every row has the same icon.
        await using (var changeTopics = new Npgsql.NpgsqlCommand("""
            UPDATE notification.notifications SET topic=CASE WHEN (hashtext(id::text)&1)=0 THEN 23 ELSE 38 END
            WHERE created_date >= '2026-11-05'
            """, (Npgsql.NpgsqlConnection)db.Database.GetDbConnection()))
            await changeTopics.ExecuteNonQueryAsync();
        await using (var tiedEvents = new Npgsql.NpgsqlCommand("""
            WITH tied AS (
              SELECT id,row_number() OVER (ORDER BY id DESC) AS position
              FROM notification.notifications WHERE payload_json->>'conversationId'=@conversation
              ORDER BY id DESC LIMIT 2
            )
            UPDATE notification.notifications AS notification
            SET created_date='2030-01-01',topic=CASE WHEN tied.position=1 THEN 23 ELSE 38 END
            FROM tied WHERE notification.id=tied.id
            """, (Npgsql.NpgsqlConnection)db.Database.GetDbConnection()))
        {
            tiedEvents.Parameters.AddWithValue("conversation", pageIds[0].ToString());
            Assert.Equal(2, await tiedEvents.ExecuteNonQueryAsync());
        }
        var events = areas.VisibleMessageBusinessEvents(pageIds);
        var oldQuery = events.GroupBy(n => n.ConversationId)
            .Select(group => new
            {
                ConversationId = group.Key,
                Topic = group.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Topic).First()
            });
        var rankedQuery = events.GroupBy(n => n.ConversationId)
            .Select(group => group.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).First());
        Task<Dictionary<Guid, TopicNotifications>> Old() => oldQuery.ToDictionaryAsync(n => n.ConversationId, n => n.Topic);
        Task<Dictionary<Guid, TopicNotifications>> Ranked() => rankedQuery.ToDictionaryAsync(n => n.ConversationId, n => n.Topic);
        var expected = await Old();
        Assert.Equal(TopicNotifications.SampleRequestPriceQuoteRequested, expected[pageIds[0]]);
        Assert.Equal(expected.OrderBy(n => n.Key), (await Ranked()).OrderBy(n => n.Key));
        var actualPage = await handler.Handle(request, default);
        Assert.Equal(pageIds, actualPage.Items.Select(n => n.ConversationId));
        Assert.All(actualPage.Items, item =>
        {
            Assert.Null(item.RepresentativeTopicCode);
            Assert.Null(item.RepresentativeEventGroupCode);
        });
        var oldTimes = new List<double>();
        var rankedTimes = new List<double>();
        for (var sample = 0; sample < 5; sample++)
        {
            foreach (var ranked in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var timer = Stopwatch.StartNew();
                var result = ranked ? await Ranked() : await Old();
                (ranked ? rankedTimes : oldTimes).Add(timer.Elapsed.TotalMilliseconds);
                Assert.Equal(expected.OrderBy(n => n.Key), result.OrderBy(n => n.Key));
            }
        }
        var oldSql = oldQuery.ToQueryString();
        var rankedSql = rankedQuery.ToQueryString();
        var destination = Path.Combine(repositoryRoot, "artifacts", "sample-all-performance", "ranked-scalar-icons.json");
        await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new
        {
            samples = 5, pageConversations = pageIds.Length, notifications = 38400, winnerParity = true,
            old = new { medianMs = oldTimes.Order().ElementAt(2), minMs = oldTimes.Min(), maxMs = oldTimes.Max(),
                sqlCharacters = oldSql.Length, jsonLinkParsers = Regex.Matches(oldSql,"jsonb_each_text").Count,
                hasRowNumber = oldSql.Contains("ROW_NUMBER", StringComparison.OrdinalIgnoreCase),
                scalarTopOne = Regex.Matches(oldSql,"LIMIT 1").Count },
            ranked = new { medianMs = rankedTimes.Order().ElementAt(2), minMs = rankedTimes.Min(), maxMs = rankedTimes.Max(),
                sqlCharacters = rankedSql.Length, jsonLinkParsers = Regex.Matches(rankedSql,"jsonb_each_text").Count,
                hasRowNumber = rankedSql.Contains("ROW_NUMBER", StringComparison.OrdinalIgnoreCase),
                scalarTopOne = Regex.Matches(rankedSql,"LIMIT 1").Count }
        }));
    }
}
