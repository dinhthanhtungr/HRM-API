using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    private async Task CompareBoundedInboxStagesAsync(ApplicationDbContext db, InternalMailAreaAccessService areas,
        NotificationService service, UnreadPerformanceUser user, string role)
    {
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var inbox = db.Notifications.Where(n => n.CompanyId == user.CompanyId &&
            n.UserStates.Any(s => s.UserId == user.EmployeeId && !s.IsArchived) && !reactions.Contains(n.Topic));
        async Task<string> Feed(bool bounded)
        {
            var scope = bounded
                ? await (Task<InternalMailAreaAccessService.InboxNotificationReadScope>)typeof(NotificationService)
                    .GetMethod("PlainFeedWindowAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(service, [inbox, 30, null, null, null, null, CancellationToken.None])!
                : await areas.DefaultLabInboxSelectionAsync(inbox, default, excludeReactions: true);
            var ids = await scope.Query.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
                .Take(30).Select(n => n.Id).ToArrayAsync();
            return JsonSerializer.Serialize(ids);
        }
        async Task<string> Topics(bool bounded)
        {
            var states = bounded
                ? areas.BadgeStates(await areas.UnreadBadgeStatesAsync(default))
                : areas.BadgeStates();
            var counts = await states.Where(s => s.UserId == user.EmployeeId && !s.IsRead && !s.IsArchived &&
                    s.Notification.CompanyId == user.CompanyId && !reactions.Contains(s.Notification.Topic))
                .GroupBy(s => new { s.Notification.Topic, Legacy = s.Notification.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate })
                .Select(g => new { g.Key.Topic, g.Key.Legacy, Count = g.Count() })
                .OrderBy(g => g.Topic).ThenBy(g => g.Legacy).ToArrayAsync();
            return JsonSerializer.Serialize(counts);
        }
        var results = new List<object>();
        foreach (var stage in new[] { "plain-feed-selection", "summary-topics-with-scope" })
        {
            var before = new List<double>();
            var after = new List<double>();
            string? expected = null;
            for (var run = 0; run < 6; run++)
            foreach (var bounded in run % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var watch = Stopwatch.StartNew();
                var value = stage == "plain-feed-selection" ? await Feed(bounded) : await Topics(bounded);
                watch.Stop();
                expected ??= value;
                Assert.Equal(expected, value);
                if (run > 0) (bounded ? after : before).Add(watch.Elapsed.TotalMilliseconds);
            }
            results.Add(new { stage, baselineSamplesMs = before, boundedSamplesMs = after,
                baselineMedianMs = before.Order().ElementAt(2), boundedMedianMs = after.Order().ElementAt(2), equal = true });
        }
        var directory = Path.Combine(postgres.RepositoryRoot, "artifacts", "bounded-inbox-performance");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, role + ".json"), JsonSerializer.Serialize(new
        {
            role, syntheticLoopbackOnly = true, includesCandidatePreparation = true,
            fullHttpMeasured = false, results
        }));
    }
}
