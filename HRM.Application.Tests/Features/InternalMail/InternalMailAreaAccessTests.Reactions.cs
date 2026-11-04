using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Commands.SetReaction;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Queries.GetReactions;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public async Task ReactionSetReplaceRemoveAreIdempotentAndDoNotChangeActivityReadOrBadge()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var rows = new List<InternalMessageReaction>();
        var db = ReactionStore.Wrap(store.Db, rows, user.CompanyId!.Value);
        var proxy = (ReactionStore)(object)db;
        var access = new InternalMailAreaAccessService(db, user);
        var reader = new InternalMessageReactionReader(db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var events = new ReactionEvents(proxy);
        var handler = new SetInternalMessageReactionCommandHandler(db, user, access, reader, new ReactionClock(), events);
        var message = db.InternalMessages.Single();
        var originalSent = message.SentAt;
        var activity = store.Child.LastMessageAt;
        var command = new SetInternalMessageReactionCommand(store.Child.InternalConversationId, message.InternalMessageId, 1);
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.Single(rows);
        Assert.Equal(1, proxy.Saves);
        Assert.Equal(1, events.Count);
        Assert.True((await handler.Handle(command with { ReactionType = 2 }, default)).Success);
        Assert.Equal(InternalMessageReactionType.Like, Assert.Single(rows).ReactionType);
        Assert.True((await handler.Handle(command with { ReactionType = null }, default)).Success);
        Assert.True((await handler.Handle(command with { ReactionType = null }, default)).Success);
        Assert.Empty(rows);
        Assert.Equal(3, proxy.Saves);
        Assert.Equal(3, events.Count);
        Assert.Equal(5, proxy.Commits);
        Assert.Equal(originalSent, message.SentAt);
        Assert.Equal(activity, store.Child.LastMessageAt);
        Assert.Empty(message.ReadStates);
        Assert.Equal(0, proxy.NotificationReads);
    }

    [Theory]
    [InlineData("SaleUser", false, false, false)]
    [InlineData("President", false, false, false)]
    [InlineData("Developer", false, false, true)]
    [InlineData("SaleUser", true, false, true)]
    [InlineData("SaleUser", true, true, false)]
    public async Task ReactionsRespectMembershipObserverAndDeletedMessage(string role, bool member, bool deleted, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, member);
        var db = ReactionStore.Wrap(store.Db, [], user.CompanyId!.Value);
        var access = new InternalMailAreaAccessService(db, user);
        var reader = new InternalMessageReactionReader(db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var message = db.InternalMessages.Single();
        message.IsDeleted = deleted;
        var events = new ReactionEvents((ReactionStore)(object)db);
        var handler = new SetInternalMessageReactionCommandHandler(db, user, access, reader, new ReactionClock(), events);
        var result = await handler.Handle(new(store.Child.InternalConversationId, message.InternalMessageId, 1), default);
        Assert.Equal(expected, result.Success);
        Assert.Equal(expected ? 1 : 0, events.Count);
        store.Child.CompanyId = Guid.NewGuid();
        Assert.False((await handler.Handle(new(store.Child.InternalConversationId, message.InternalMessageId, 1), default)).Success);
    }

    [Fact]
    public async Task ReactionNotificationSynchronizationRunsAfterReactionSaveBeforeCommitAndOnlyForActualChanges()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var db = ReactionStore.Wrap(store.Db, [], user.CompanyId!.Value);
        var proxy = (ReactionStore)(object)db;
        var access = new InternalMailAreaAccessService(db, user);
        var reader = new InternalMessageReactionReader(db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var sync = new RecordingReactionNotifications(proxy);
        var handler = new SetInternalMessageReactionCommandHandler(db, user, access, reader, new ReactionClock(), new ReactionEvents(proxy), sync);
        var command = new SetInternalMessageReactionCommand(store.Child.InternalConversationId, db.InternalMessages.Single().InternalMessageId, 1);
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.True((await handler.Handle(command with { ReactionType = 2 }, default)).Success);
        Assert.True((await handler.Handle(command with { ReactionType = null }, default)).Success);
        Assert.Equal(new[] { true, false, false }, sync.NewActors);
        Assert.Equal(6, proxy.Saves);
        Assert.Equal(4, proxy.Commits);
    }

    private sealed class RecordingReactionNotifications(ReactionStore store) : IInternalMessageReactionNotifications
    {
        public List<bool> NewActors { get; } = [];
        public Task SynchronizeAsync(Guid companyId, Guid conversationId, Guid messageId, Guid actorId, bool isNewActor, CancellationToken ct)
        {
            Assert.True(store.InTransaction);
            Assert.Equal(1 + NewActors.Count * 2, store.Saves);
            NewActors.Add(isNewActor);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DisabledReactionsNeverReadNewTableAndInvalidTypeNeverStartsTransaction()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var reader = new InternalMessageReactionReader(store.Db, user, access, Options.Create(new InternalMailReactionOptions()));
        var item = new InternalMessageDto { MessageId = Guid.NewGuid(), ConversationId = store.Child.InternalConversationId };
        await reader.AttachAsync([item], default);
        Assert.False(item.ReactionsEnabled);
        Assert.Empty((await reader.SummaryAsync(item.MessageId, default)).Items);
        var handler = new SetInternalMessageReactionCommandHandler(store.Db, user, access, reader, new ReactionClock(), new ReactionEvents(null));
        Assert.False((await handler.Handle(new(store.Child.InternalConversationId, item.MessageId, 1), default)).Success);
        reader = new(store.Db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        handler = new(store.Db, user, access, reader, new ReactionClock(), new ReactionEvents(null));
        Assert.False((await handler.Handle(new(store.Child.InternalConversationId, item.MessageId, 99), default)).Success);
    }

    [Fact]
    public async Task ReactionCountsAndPeopleHideDeveloperAndRejectForeignConversation()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var people = ObserverParticipantFixture((MemoryStore)(object)store.Db, user.CompanyId!.Value);
        var message = store.Db.InternalMessages.Single();
        var rows = people.Select(p => new InternalMessageReaction
        {
            MessageId = message.InternalMessageId, EmployeeId = p.EmployeeId,
            Employee = store.Db.Employees.Single(e => e.EmployeeId == p.EmployeeId),
            ReactionType = InternalMessageReactionType.Heart
        }).ToList();
        var db = ReactionStore.Wrap(store.Db, rows, user.CompanyId.Value);
        var access = new InternalMailAreaAccessService(db, user);
        var reader = new InternalMessageReactionReader(db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var summary = await reader.SummaryAsync(message.InternalMessageId, default);
        Assert.Equal(1, Assert.Single(summary.Items).Count);
        var handler = new GetInternalMessageReactionsQueryHandler(access, reader);
        var list = await handler.Handle(new(store.Child.InternalConversationId, message.InternalMessageId), default);
        Assert.Equal(people[1].EmployeeId, Assert.Single(list!.Items).EmployeeId);
        Assert.Null(await handler.Handle(new(Guid.NewGuid(), message.InternalMessageId), default));
        store.Child.Participants.Single().IsActive = false;
        Assert.Null(await handler.Handle(new(store.Child.InternalConversationId, message.InternalMessageId), default));
    }

    [Fact]
    public void ReactionSqlKeepsCompanyPrivacyAndBoundedGroupScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var access = new InternalMailAreaAccessService(db, user);
        var reader = new InternalMessageReactionReader(db, user, access, Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var sql = reader.VisibleReactions().ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("InternalConversationParticipants", sql);
        Assert.Contains("AspNetUserRoles", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.NotEmpty(access.ReactionReaders(user.CompanyId!.Value, Guid.NewGuid()).ToQueryString());
        var entity = db.Model.FindEntityType(typeof(InternalMessageReaction))!;
        Assert.Equal(new[] { "MessageId", "EmployeeId" }, entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    private sealed class ReactionClock : IDateTimeProvider { public DateTime Now => new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc); }
    private sealed class ReactionEvents(ReactionStore? store) : IInternalMessageReactionEvents
    {
        public int Count { get; private set; }
        public void Enqueue(Guid companyId, Guid conversationId, Guid messageId)
        {
            Assert.NotNull(store);
            Assert.True(store.InTransaction);
            Count++;
        }
    }
    public class ReactionStore : DispatchProxy
    {
        private IInternalMailDbContext _inner = default!;
        private ReactionSet _rows = default!;
        public int Saves, Commits, NotificationReads;
        public bool InTransaction;
        public static IInternalMailDbContext Wrap(IInternalMailDbContext inner, List<InternalMessageReaction> rows, Guid companyId)
        {
            var db = Create<IInternalMailDbContext, ReactionStore>();
            var proxy = (ReactionStore)(object)db;
            proxy._inner = inner;
            proxy._rows = new(rows, companyId);
            return db;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_InternalMessageReactions") return _rows;
            if (method.Name.Contains("Notification") || method.Name.Contains("ReadStates")) NotificationReads++;
            if (method.Name == nameof(IInternalMailDbContext.BeginTransactionAsync))
            { InTransaction = true; return Task.FromResult<IDbContextTransaction>(new ReactionTransaction(this)); }
            if (method.Name == nameof(IInternalMailDbContext.LockInternalConversationAsync)) return Task.CompletedTask;
            if (method.Name == nameof(IInternalMailDbContext.SaveChangesAsync)) { Assert.True(InTransaction); Saves++; return Task.FromResult(1); }
            return method.Invoke(_inner, args);
        }
    }
    private sealed class ReactionSet(List<InternalMessageReaction> rows, Guid companyId) : MemorySet<InternalMessageReaction>(rows)
    {
        public override EntityEntry<InternalMessageReaction> Add(InternalMessageReaction entity)
        { entity.Employee = new() { EmployeeId = entity.EmployeeId, CompanyId = companyId }; rows.Add(entity); return null!; }
        public override EntityEntry<InternalMessageReaction> Remove(InternalMessageReaction entity) { rows.Remove(entity); return null!; }
    }
    private sealed class ReactionTransaction(ReactionStore store) : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool SupportsSavepoints => false;
        public void Commit() { store.Commits++; store.InTransaction = false; }
        public Task CommitAsync(CancellationToken ct = default) { Commit(); return Task.CompletedTask; }
        public void Rollback() => store.InTransaction = false;
        public Task RollbackAsync(CancellationToken ct = default) { Rollback(); return Task.CompletedTask; }
        public void Dispose() => store.InTransaction = false;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
