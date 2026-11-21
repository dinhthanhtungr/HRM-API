using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore.Storage;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using HRM.Application.Abstractions.Persistence.InternalMail;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(InternalConversationParticipantRole.Watcher, true, false)]
    [InlineData(InternalConversationParticipantRole.Watcher, true, true)]
    [InlineData(InternalConversationParticipantRole.Member, false, false)]
    public async Task SendingGeneralLabMessagePromotesSystemWatcherButPreservesMemberMute(
        InternalConversationParticipantRole role, bool shouldNotify, bool outerTransaction)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        var company = user.CompanyId!.Value;
        var employee = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Product = new() { CompanyId = company, IsActive = true,
                Category = new() { CompanyId = company, ExternalId = "CMP" } } };
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = sample.SampleRequestId;
        var participant = new InternalConversationParticipant { InternalConversationId = store.Root.InternalConversationId,
            EmployeeId = employee.EmployeeId, IsActive = true, IsMuted = true, Role = role };
        store.Root.Participants.Add(participant);
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([employee]);
        memory.Values[nameof(store.Db.SampleRequests)] = new MemorySet<SampleRequest>([sample]);
        memory.Values[nameof(store.Db.MemberInGroups)] = new MemorySet<MemberInGroup>([
            new() { Profile = employee.EmployeeId, ProfileNavigation = employee, IsActive = true,
                Group = new() { CompanyId = company, GroupType = "QAQC.RD" } }]);
        memory.Values[nameof(store.Db.InternalConversationParticipants)] = new SendRecordingSet<InternalConversationParticipant>([participant]);
        memory.Values[nameof(store.Db.InternalMessages)] = new SendRecordingSet<InternalMessage>([]);
        memory.Values[nameof(store.Db.InternalMessageReadStates)] = new SendRecordingSet<InternalMessageReadState>([]);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(0);
        var transaction = DispatchProxy.Create<IDbContextTransaction, ReadReceiptTransaction>();
        if (!outerTransaction)
            memory.Values[nameof(store.Db.BeginTransactionAsync)] = Task.FromResult(transaction);
        memory.Values[nameof(store.Db.LockInternalConversationAsync)] = Task.CompletedTask;
        var notifications = DispatchProxy.Create<INotificationService, SendNotificationRecorder>();
        var access = new InternalMailAreaAccessService(store.Db, user);
        var audience = new SampleRequestNotificationAudienceResolver(
            DispatchProxy.Create<ICRMReadDbContext, MemoryStore>(), store.Db);
        var handler = new SendInternalMessageCommandHandler(store.Db, user, new SendTestClock(), notifications,
            null!, null!, access, new InternalMailMentionResolver(store.Db, access), audience);
        var result = await handler.Handle(new() { ConversationId = store.Root.InternalConversationId,
            Body = "Test", HasGroupTransaction = outerTransaction }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(shouldNotify, ((SendNotificationRecorder)(object)notifications).Request!.TargetUserIds!.Contains(employee.EmployeeId));
        Assert.Equal(!shouldNotify, participant.IsMuted);
        Assert.Equal(InternalConversationParticipantRole.Member, participant.Role);
        Assert.Equal(shouldNotify && !outerTransaction, ((ReadReceiptTransaction)(object)transaction).Committed);
    }

    [Theory]
    [InlineData("QAQC.RD", "CMP", true)]
    [InlineData("QAQC.RD", "PIG", false)]
    [InlineData("QAQC.MAU", "PIG", true)]
    [InlineData("QAQC.MAU", "CMP", false)]
    [InlineData("QAQC.RD", "GCO", true)]
    [InlineData("QAQC.MAU", "GCO", true)]
    [InlineData("QAQC.MAU", " pig ", true)]
    [InlineData("QAQC.RD", "unknown", false)]
    public async Task DefaultInboxFiltersByActiveTeamWithoutRemovingAuthorizedSearchAccess(
        string team, string category, bool expected)
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var company = user.CompanyId!.Value;
        var employee = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = company, IsActive = true };
        var membership = new MemberInGroup { Profile = employee.EmployeeId, ProfileNavigation = employee,
            IsActive = true, IsAdmin = false, Group = new Group { CompanyId = company, GroupType = team } };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Product = new() { CompanyId = company, Category = new() { CompanyId = company, ExternalId = category } } };
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = sample.SampleRequestId;
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.MemberInGroups)] = new MemorySet<MemberInGroup>([membership]);
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([employee]);
        memory.Values[nameof(store.Db.SampleRequests)] = new MemorySet<SampleRequest>([sample]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(expected, access.DefaultLabInboxConversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        // Searching skips the presentation filter but never skips the authorized read scope.
        Assert.Contains(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Contains(access.Conversations(), c => c.InternalConversationId == store.Root.InternalConversationId);
        var events = new[] {
            new Notification { Id = Guid.NewGuid(), CompanyId = company, Topic = TopicNotifications.SampleRequestMessageCreated },
            new Notification { Id = Guid.NewGuid(), CompanyId = company, Topic = TopicNotifications.SampleRequestMessageCreated },
            new Notification { Id = Guid.NewGuid(), CompanyId = company, Topic = TopicNotifications.WorkTaskDue },
            new Notification { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Topic = TopicNotifications.WorkTaskDue }
        }.AsQueryable();
        var rows = events.ToArray();
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new[] {
            new InternalMailNotificationLink { NotificationId = rows[0].Id, ConversationId = store.Root.InternalConversationId, HasConversation = true },
            new InternalMailNotificationLink { NotificationId = rows[1].Id, ConversationId = store.Child.InternalConversationId, HasConversation = true },
            new InternalMailNotificationLink { NotificationId = rows[2].Id, HasConversation = false },
            new InternalMailNotificationLink { NotificationId = rows[3].Id, HasConversation = false }
        }.AsQueryable();
        var baseline = access.DefaultLabInboxNotifications(access.Notifications(events)).Select(n => n.Id).ToArray();
        var optimized = await access.DefaultLabInboxNotificationsAsync(events, default);
        Assert.Equal(baseline, optimized.Select(n => n.Id).ToArray());
        var candidates = new[] { store.Root.InternalConversationId, store.Child.InternalConversationId };
        var eventIds = rows.Select(n => n.Id).ToArray();
        var boundedEvents = new AsyncQuery<Notification>(rows);
        Assert.Equal(baseline, (await access.DefaultLabInboxNotificationsAsync(boundedEvents, default,
            candidateConversationIds: candidates, candidateEventIds: eventIds)).Select(n => n.Id).ToArray());
        var childEvents = new AsyncQuery<Notification>([rows[1], rows[2]]);
        Assert.Equal(baseline.Where(id => id != rows[0].Id),
            (await access.DefaultLabInboxNotificationsAsync(childEvents, default,
                candidateConversationIds: [store.Child.InternalConversationId], candidateEventIds: [rows[1].Id, rows[2].Id]))
            .Select(n => n.Id).ToArray());
        var feedOnly = await access.DefaultLabInboxNotificationsAsync(events, default, excludeReactions: true);
        Assert.Equal(baseline, feedOnly.Select(n => n.Id).ToArray());
        Assert.Equal(expected ? 3 : 1, baseline.Length);
        Assert.DoesNotContain(rows[3].Id, baseline);
        membership.IsActive = false;
        Assert.Contains(access.DefaultLabInboxConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Equal(access.DefaultLabInboxNotifications(access.Notifications(events)).Select(n => n.Id).ToArray(),
            (await access.DefaultLabInboxNotificationsAsync(events, default)).Select(n => n.Id).ToArray());
        foreach (var participant in store.Child.Participants) participant.IsActive = false;
        Assert.Equal(access.DefaultLabInboxNotifications(access.Notifications(events)).Select(n => n.Id).ToArray(),
            (await access.DefaultLabInboxNotificationsAsync(boundedEvents, default,
                candidateConversationIds: candidates, candidateEventIds: eventIds)).Select(n => n.Id).ToArray());
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.DefaultLabInboxNotificationsAsync(boundedEvents, default,
            candidateConversationIds: candidates, candidateEventIds: eventIds));
    }

    [Fact]
    public void DefaultLabInboxQueriesTranslateToSqlBeforePaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var access = new InternalMailAreaAccessService(db, new User("LabUser"));
        Assert.Contains("QAQC.MAU", access.DefaultLabInboxConversations().ToQueryString());
        Assert.Contains("QAQC.RD", access.DefaultLabInboxNotifications(db.Notifications).ToQueryString());
    }
}
