using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", true)]
    [InlineData("President", false)]
    public Task UnreadSummaryPreservesRoleCountsAndMeasuresBaseline(string role, bool storage)
        => MeasureSampleAllAsync(role, storage, 64, compareSummary: true);

    private async Task CompareSummaryAsync(ApplicationDbContext db, InternalMailAreaAccessService areas, string role, bool storage)
    {
        // Leave a mixture of linked deliveries, read history, unlinked legacy messages and archived inbox states.
        await postgres.ExecuteAsync("""
            UPDATE "InternalMail"."InternalMessageReadStates" SET "IsRead"=true
              WHERE substring("InternalMessageId"::text,1,1) IN ('a','b','c');
            DELETE FROM notification.notification_user_states s USING notification.notifications n WHERE s.notification_id=n.id AND substring(n.payload_json->>'conversationId',1,1) IN ('d','e','f');
            DELETE FROM notification.notifications WHERE substring(payload_json->>'conversationId',1,1) IN ('d','e','f');
            UPDATE "InternalMail"."InternalConversationParticipants" SET "IsMuted"=true
              WHERE substring("InternalConversationId"::text,1,1)='c';
            ANALYZE;
            """);
        var company = Guid.Parse(Company);
        var reactionBefore = new List<double>(); var reactionAfter = new List<double>();
        async Task<int> MeasureReactions(bool baseline, bool record)
        {
            var clock = Stopwatch.StartNew();
            var topics = InternalMailPersonalActivity.ReactionTopics;
            var count = baseline
                ? await areas.States().CountAsync(s => !s.IsRead && !s.IsArchived && topics.Contains(s.Notification.Topic))
                : await areas.PersonalReactionUnreadCountAsync(default);
            if (record) (baseline ? reactionBefore : reactionAfter).Add(clock.Elapsed.TotalMilliseconds);
            return count;
        }
        await MeasureReactions(true, false); await MeasureReactions(false, false);
        for (var sample = 0; sample < 5; sample++)
            Assert.Equal(await MeasureReactions(sample % 2 == 0, true), await MeasureReactions(sample % 2 != 0, true));
        async Task<Dictionary<InternalMailRelatedType,int>> Old()
        {
            var roots = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive);
            return await (from message in areas.LegacyUnreadMessages()
                          join scope in areas.GroupScopes(company) on message.InternalConversationId equals scope.ConversationId
                          join root in roots on scope.RootConversationId equals root.InternalConversationId
                          group message by (root.RelatedType ?? InternalMailRelatedType.Other) into counts
                          select new { Type=counts.Key, Count=counts.Count() })
                .ToDictionaryAsync(c=>c.Type,c=>c.Count);
        }
        await Old(); await areas.LegacyUnreadCountsByTypeAsync(default);
        var before = new List<double>(); var after = new List<double>();
        async Task<Dictionary<InternalMailRelatedType,int>> Run(bool old)
        {
            var timer=Stopwatch.StartNew();
            var result=old ? await Old() : await areas.LegacyUnreadCountsByTypeAsync(default);
            (old ? before : after).Add(timer.Elapsed.TotalMilliseconds);
            return result;
        }
        for(var i=0;i<5;i++)
        {
            var left=await Run(i%2==0); var right=await Run(i%2!=0);
            Assert.Equal(left.OrderBy(x=>x.Key),right.OrderBy(x=>x.Key));
            Assert.Equal(left.Values.Sum(),right.Values.Sum());
        }
        // Frozen pre-optimization composition: compare the complete public DTO, including all categories/event groups/mentions.
        async Task<NotificationUnreadSummaryDto> OldSummary()
        {
        var companyId = Guid.Parse(Company);
        var employeeId = Guid.Parse(Employee);

        var reactionTopics = InternalMailPersonalActivity.ReactionTopics;
        var personalReactionCount = await areas.States().CountAsync(s => !s.IsRead && !s.IsArchived &&
            reactionTopics.Contains(s.Notification.Topic), default);
        var topicCounts = await areas.States().Where(s => !reactionTopics.Contains(s.Notification.Topic))
            .AsNoTracking()
            .Where(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .GroupBy(x => new
            {
                x.Notification.Topic,
                IsLegacyData = x.Notification.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate
            })
            .Select(group => new
            {
                group.Key.Topic,
                group.Key.IsLegacyData,
                UnreadCount = group.Count()
            })
            .ToListAsync(default);

        var categoryCounts = NotificationTopicCatalog.GetCategoryCodes()
            .ToDictionary(categoryCode => categoryCode, _ => 0, StringComparer.Ordinal);
        var eventGroupCounts = new Dictionary<(string CategoryCode, string EventGroupCode), int>();

        foreach (var topicCount in topicCounts)
        {
            var definition = NotificationTopicCatalog.GetDefinition(topicCount.Topic);
            var categoryCode = topicCount.IsLegacyData
                ? NotificationCategoryCodes.LegacyData
                : definition.CategoryCode;

            categoryCounts[categoryCode] += topicCount.UnreadCount;

            if (!topicCount.IsLegacyData)
            {
                var key = (categoryCode, definition.EventGroupCode);
                eventGroupCounts[key] = eventGroupCounts.GetValueOrDefault(key) + topicCount.UnreadCount;
            }
        }

        // Same disjoint legacy fallback as list/detail/quick groups, never all participants' raw read states.
        var legacyCounts = await Old();
        foreach (var legacy in legacyCounts)
        {
            var category = legacy.Key switch
            {
                InternalMailRelatedType.SampleRequest => NotificationCategoryCodes.SampleRequest,
                InternalMailRelatedType.Quotation => NotificationCategoryCodes.Quotation,
                InternalMailRelatedType.MfgProductionOrder => NotificationCategoryCodes.Production,
                InternalMailRelatedType.MerchandiseOrder => NotificationCategoryCodes.SalesOrder,
                InternalMailRelatedType.Customer => NotificationCategoryCodes.Customer,
                _ => NotificationCategoryCodes.InternalMail
            };
            categoryCounts[category] += legacy.Value;
            var key = (category, "message");
            eventGroupCounts[key] = eventGroupCounts.GetValueOrDefault(key) + legacy.Value;
        }

        return new NotificationUnreadSummaryDto
        {
            TotalUnread = topicCounts.Sum(x => x.UnreadCount) + legacyCounts.Values.Sum(),
            UnreadMentionCount = personalReactionCount + await areas.MentionedMessages().CountAsync(
                m => m.ReadStates.Any(s => s.EmployeeId == employeeId && !s.IsRead), default),
            Categories = NotificationTopicCatalog.GetCategoryCodes()
                .Select(categoryCode => new NotificationCategoryUnreadCountDto
                {
                    CategoryCode = categoryCode,
                    UnreadCount = categoryCounts[categoryCode],
                    EventGroups = NotificationTopicCatalog.GetEventGroupCodes(categoryCode)
                        .Select(eventGroupCode => new NotificationEventGroupUnreadCountDto
                        {
                            EventGroupCode = eventGroupCode,
                            UnreadCount = eventGroupCounts.GetValueOrDefault((categoryCode, eventGroupCode))
                        })
                        .ToArray()
                })
                .ToList()
        };
        }
        var service = new NotificationService(db, new UnreadPerformanceUser(role), null!, null!, areas,
            new NotificationFeedConversationInfoResolver(db, areas,
                new InternalConversationSampleRequestInfoResolver(db), new InternalConversationQuotationInfoResolver(db)));
        var fullTimer = Stopwatch.StartNew();
        var expectedSummary = await OldSummary();
        var oldFullMs = fullTimer.Elapsed.TotalMilliseconds;
        fullTimer.Restart();
        var actualSummary = await service.GetUnreadSummaryAsync();
        var newFullMs = fullTimer.Elapsed.TotalMilliseconds;
        Assert.Equal(JsonSerializer.Serialize(expectedSummary), JsonSerializer.Serialize(actualSummary));
        var result=await areas.LegacyUnreadCountsByTypeAsync(default);
        Assert.True(result.Values.Sum()>0); // Comparison must exercise real fallback rows.
        var folder=Path.Combine(postgres.RepositoryRoot,"artifacts","unread-summary-comparison");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,role+"-"+storage+".json"),JsonSerializer.Serialize(new {
            role, storage, oldFullMs, newFullMs, fullSummaryMatched=true,
            reactionBeforeMedianMs=reactionBefore.Order().ElementAt(2), reactionAfterMedianMs=reactionAfter.Order().ElementAt(2),
            reactionBeforeMin=reactionBefore.Min(), reactionBeforeMax=reactionBefore.Max(),
            reactionAfterMin=reactionAfter.Min(), reactionAfterMax=reactionAfter.Max(),
            beforeMedianMs=before.Order().ElementAt(2), afterMedianMs=after.Order().ElementAt(2),
            beforeMin=before.Min(),beforeMax=before.Max(),afterMin=after.Min(),afterMax=after.Max(),
            total=result.Values.Sum(),categories=result.OrderBy(x=>x.Key).Select(x=>new {type=x.Key.ToString(),count=x.Value})
        }));
        await postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalMessageReadStates\" SET \"IsRead\"=true;");
        Assert.Empty(await Old());
        Assert.Empty(await areas.LegacyUnreadCountsByTypeAsync(default));
    }
}
