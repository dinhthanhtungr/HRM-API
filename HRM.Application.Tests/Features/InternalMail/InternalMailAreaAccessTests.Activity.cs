using System.Reflection;
using HRM.Application.Features.InternalMail.Commands.EnsureDefaultAreas;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnsureDefaultsAddsPresidentToBothGroupsWithoutReactivatingRemovedMember(bool removed)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var president = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        var account = new HRM.Domain.Identity.ApplicationUser { Id = Guid.NewGuid(), EmployeeId = president.EmployeeId };
        var role = new HRM.Domain.Identity.ApplicationRole { Id = Guid.NewGuid(), Name = "President" };
        if (removed) store.Child.Participants.Add(new() { EmployeeId = president.EmployeeId, IsActive = false });
        var memory = (MemoryStore)(object)store.Db;
        var conversations = new List<InternalConversation> { store.Root, store.Child };
        memory.Values[nameof(store.Db.InternalConversations)] = new ActivityConversationSet(conversations);
        memory.Values["Employees"] = new MemorySet<Employee>([president]);
        memory.Values["Users"] = new MemorySet<HRM.Domain.Identity.ApplicationUser>([account]);
        memory.Values["Roles"] = new MemorySet<HRM.Domain.Identity.ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<HRM.Domain.Identity.ApplicationUserRole>([
            new() { UserId = account.Id, RoleId = role.Id, IsActive = true }]);
        memory.Values[nameof(store.Db.BeginTransactionAsync)] = Task.FromResult(DispatchProxy.Create<IDbContextTransaction, ReadReceiptTransaction>());
        memory.Values[nameof(store.Db.LockInternalConversationAsync)] = Task.CompletedTask;
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        var handler = new EnsureInternalConversationAreasCommandHandler(store.Db, user, new SendTestClock(), new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new(store.Root.InternalConversationId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(!removed, Assert.Single(store.Child.Participants).IsActive);
        Assert.Equal(president.EmployeeId, Assert.Single(conversations.Single(c => c.RelatedType == InternalMailRelatedType.ConversationPricing).Participants).EmployeeId);
        await handler.Handle(new(store.Root.InternalConversationId), default);
        Assert.All(conversations.Where(c => c != store.Root), c => Assert.Single(c.Participants));
    }

    [Theory]
    [InlineData(InternalMailRelatedType.ConversationTechnical)]
    [InlineData(InternalMailRelatedType.ConversationPricing)]
    [InlineData(InternalMailRelatedType.ConversationCustom)]
    public void EmptyLegacyGroupUsesRootBaselineUntilAnActualMessageArrives(InternalMailRelatedType type)
    {
        var user = new User("Developer");
        var store = CreateStore(user, type, false);
        var memory = (MemoryStore)(object)store.Db;
        var original = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
        store.Root.CreatedAt = original;
        store.Child.CreatedAt = now;
        store.Child.LastMessageAt = now; // Historical rows already contain the synthetic timestamp.
        store.Child.CreatedByNavigation = new Employee();
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([]);
        var areas = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(original, areas.InboxConversations().Single(c => c.InternalConversationId == store.Child.InternalConversationId).ActivityAt);
        var projection = InternalConversationDetailProjection.ForEmployee(user.EmployeeId!.Value,
            store.Db.InternalMessages, store.Db.InternalConversations).Compile();
        Assert.Equal(original, projection(store.Child).LastMessageAt);
        Assert.Equal(now, store.Child.CreatedAt); // Preserve creation audit.

        var message = new InternalMessage
        {
            InternalMessageId = Guid.NewGuid(), InternalConversationId = store.Child.InternalConversationId,
            Conversation = store.Child, SentAt = now, SenderEmployeeId = user.EmployeeId.Value
        };
        store.Child.Messages.Add(message);
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([message]);
        Assert.Equal(now, areas.InboxConversations().Single(c => c.InternalConversationId == store.Child.InternalConversationId).ActivityAt);
        Assert.Equal(now, InternalConversationDetailProjection.ForEmployee(user.EmployeeId.Value,
            store.Db.InternalMessages, store.Db.InternalConversations).Compile()(store.Child).LastMessageAt);
    }

    [Fact]
    public async Task EnsuringDefaultsPreservesRootActivityAndDoesNotCreateMessages()
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        var memory = (MemoryStore)(object)store.Db;
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        store.Root.LastMessageAt = store.Root.CreatedAt.AddDays(2);
        var conversations = new List<InternalConversation> { store.Root };
        memory.Values[nameof(store.Db.InternalConversations)] = new ActivityConversationSet(conversations);
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([]);
        memory.Values[nameof(store.Db.BeginTransactionAsync)] = Task.FromResult(DispatchProxy.Create<IDbContextTransaction, ReadReceiptTransaction>());
        memory.Values[nameof(store.Db.LockInternalConversationAsync)] = Task.CompletedTask;
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        var handler = new EnsureInternalConversationAreasCommandHandler(store.Db, user, new SendTestClock(),
            new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new(store.Root.InternalConversationId), default);
        Assert.True(result.Success, result.Message);
        Assert.True(result.Data!.Changed);
        Assert.Equal(3, conversations.Count);
        Assert.All(conversations.Where(c => c != store.Root), c =>
        {
            Assert.Equal(store.Root.CreatedAt, c.LastMessageAt);
            Assert.Equal(new SendTestClock().Now, c.CreatedAt);
        });
        Assert.Equal(store.Root.CreatedAt.AddDays(2), store.Root.LastMessageAt);
        Assert.Empty(store.Db.InternalMessages);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(0);
        var unchanged = await handler.Handle(new(store.Root.InternalConversationId), default);
        Assert.True(unchanged.Success);
        Assert.False(unchanged.Data!.Changed);
        Assert.Equal(3, conversations.Count);
    }

    [Fact]
    public void ActivityProjectionsTranslateToPostgresBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("Developer");
        var areas = new InternalMailAreaAccessService(db, user);
        var sql = areas.InboxConversations().OrderByDescending(c => c.ActivityAt).Take(30)
            .Select(c => new { c.InternalConversationId, c.ActivityAt }).ToQueryString();
        Assert.Contains("COALESCE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.NotEmpty(areas.Conversations().Select(InternalConversationDetailProjection.ForEmployee(user.EmployeeId!.Value,
            db.InternalMessages, db.InternalConversations)).ToQueryString());
    }

    private sealed class ActivityConversationSet(List<InternalConversation> items) : MemorySet<InternalConversation>(items)
    {
        public override EntityEntry<InternalConversation> Add(InternalConversation entity)
        {
            items.Add(entity);
            return null!;
        }
    }
}
