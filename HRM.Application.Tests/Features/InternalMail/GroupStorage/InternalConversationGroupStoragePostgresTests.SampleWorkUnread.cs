using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("Developer")]
    public async Task SampleUnreadPageMatchesBaselineOnPostgres(string role)
    {
        await PresidentBusinessSchemaAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(new PageBadgeBaselineSql()).Options);
        var user = new UnreadPerformanceUser(role);
        var areas = new InternalMailAreaAccessService(db, user);
        var ids = await areas.Conversations().Where(c => c.RelatedType == InternalMailRelatedType.SampleRequest ||
            c.RelatedId == Guid.Parse(Root)).Select(c => c.InternalConversationId).ToArrayAsync();
        Assert.NotEmpty(ids);
        Task<Dictionary<Guid, int>> Before() => areas.NotificationUnreadCounts(ids)
            .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount);
        Task<Dictionary<Guid, int>> After() => areas.PageNotificationUnreadCountsAsync(ids, default);
        var expected = await Before();
        Assert.NotEmpty(expected);
        Assert.Equal(expected.OrderBy(x => x.Key), (await After()).OrderBy(x => x.Key));
        var before = new List<double>();
        var after = new List<double>();
        async Task Measure(bool baseline)
        {
            var clock = Stopwatch.StartNew();
            var actual = baseline ? await Before() : await After();
            (baseline ? before : after).Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(expected.OrderBy(x => x.Key), actual.OrderBy(x => x.Key));
        }
        for (var i = 0; i < 5; i++)
        {
            await Measure(i % 2 == 0);
            await Measure(i % 2 != 0);
        }
        var directory = Path.Combine(postgres.RepositoryRoot, "artifacts", "sample-unread-performance");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, role + ".json"), JsonSerializer.Serialize(new
        {
            role, samples = 5, identicalCounts = true, conversations = ids.Length,
            beforeMedianMs = before.Order().ElementAt(2), afterMedianMs = after.Order().ElementAt(2)
        }));
    }
}
