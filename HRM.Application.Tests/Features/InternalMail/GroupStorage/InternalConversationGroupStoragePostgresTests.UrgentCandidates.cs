using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task UrgentCandidateScopePreservesInboxAndCursor()
    {
        await PresidentBusinessSchemaAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        db.Database.SetCommandTimeout(15);
        var user = new UnreadPerformanceUser("Developer");
        var areas = new InternalMailAreaAccessService(db, user);
        await db.InternalMessages.Where(message => message.InternalConversationId == Guid.Parse(Custom) &&
                message.SenderEmployeeId != user.EmployeeId && !message.IsDeleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.IsUrgent, true));

        var inbox = db.Notifications.Where(notification => notification.CompanyId == user.CompanyId &&
            notification.UserStates.Any(state => state.UserId == user.EmployeeId && !state.IsArchived));
        var beforeScope = areas.UrgentHubEvents(inbox);
        var before = await beforeScope.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
            .Select(n => new { n.Id, n.CreatedDate }).ToArrayAsync();
        Assert.NotEmpty(before);
        var beforeTimes = new List<double>();
        var afterTimes = new List<double>();
        async Task MeasureBefore()
        {
            var watch = Stopwatch.StartNew();
            var ids = await beforeScope.Select(n => n.Id).ToArrayAsync();
            beforeTimes.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(before.Select(n => n.Id).Order(), ids.Order());
        }
        async Task MeasureAfter()
        {
            var watch = Stopwatch.StartNew();
            var scope = await areas.NarrowUrgentInboxAsync(inbox, default);
            var ids = await scope.Inbox.Select(n => n.Id).ToArrayAsync();
            afterTimes.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(before.Select(n => n.Id).Order(), ids.Order());
        }

        await MeasureBefore();
        await MeasureAfter();
        beforeTimes.Clear();
        afterTimes.Clear();
        for (var i = 0; i < 5; i++)
        {
            if (i % 2 == 0) { await MeasureBefore(); await MeasureAfter(); }
            else { await MeasureAfter(); await MeasureBefore(); }
        }

        var afterScope = (await areas.NarrowUrgentInboxAsync(inbox, default)).Inbox;
        var head = await afterScope.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
            .Select(n => n.Id).Take(1).ToArrayAsync();
        var cursor = before[0];
        var tail = await afterScope.Where(n => n.CreatedDate < cursor.CreatedDate ||
                (n.CreatedDate == cursor.CreatedDate && n.Id.CompareTo(cursor.Id) < 0))
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id).ToArrayAsync();
        Assert.Equal(before.Take(1).Select(n => n.Id), head);
        Assert.Equal(before.Skip(1).Select(n => n.Id), tail);
        Assert.Equal(
            await beforeScope.CountAsync(n => n.UserStates.Any(s => s.UserId == user.EmployeeId && !s.IsRead)),
            await afterScope.CountAsync(n => n.UserStates.Any(s => s.UserId == user.EmployeeId && !s.IsRead)));
        var artifact = Path.Combine(postgres.RepositoryRoot, "artifacts", "notification-urgent-candidates.json");
        await File.WriteAllTextAsync(artifact, JsonSerializer.Serialize(new { beforeTimes, afterTimes }));
    }
}
