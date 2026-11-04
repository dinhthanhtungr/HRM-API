using System.Reflection;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("Developer", InternalMailRelatedType.ConversationPricing, false)]
    [InlineData("Developer", InternalMailRelatedType.ConversationPricing, true)]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData("President", InternalMailRelatedType.ConversationPricing, false)]
    [InlineData("President", InternalMailRelatedType.ConversationPricing, true)]
    [InlineData("President", InternalMailRelatedType.ConversationCustom, false)]
    [InlineData("President", InternalMailRelatedType.ConversationCustom, true)]
    [InlineData("President", InternalMailRelatedType.SampleRequest, false)]
    [InlineData("President", InternalMailRelatedType.SampleRequest, true)]
    [InlineData("ACCUser", InternalMailRelatedType.SampleRequest, false)]
    [InlineData("ACCUser", InternalMailRelatedType.SampleRequest, true)]
    public async Task AuthorizedObserverWithoutMembershipCanAcknowledgeVisibleThreadWithoutCreatingReadStates(
        string role, InternalMailRelatedType type, bool withBoundary)
    {
        var user = new User(role);
        var store = CreateStore(user, type, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var memory = (MemoryStore)(object)store.Db;
        var transaction = DispatchProxy.Create<IDbContextTransaction, ReadReceiptTransaction>();
        memory.Values[nameof(store.Db.BeginTransactionAsync)] = Task.FromResult(transaction);
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] =
            new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalMailNotificationLink>([]);
        var handler = new MarkInternalConversationReadCommandHandler(store.Db, user, new SendTestClock(),
            new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new()
        {
            ConversationId = store.Child.InternalConversationId, AreaCode = InternalMailAreaAccessService.AreaOf(type),
            ThroughMessageId = withBoundary ? store.Db.InternalMessages.Single().InternalMessageId : null
        }, default);
        if (role != "Developer")
        {
            Assert.False(result.Success);
            Assert.Empty(store.Db.InternalConversationParticipants);
            Assert.Empty(store.Db.InternalMessageReadStates);
            return;
        }
        Assert.True(result.Success, result.Message);
        Assert.True(((ReadReceiptTransaction)(object)transaction).Committed);
        Assert.Empty(store.Db.InternalConversationParticipants);
        Assert.Empty(store.Db.InternalMessageReadStates);
    }

    [Theory]
    [InlineData("President")]
    [InlineData("ACCUser")]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("Admin")]
    public async Task ReadReceiptDoesNotGrantAccessToUnrelatedPrivateConversation(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        var handler = new MarkInternalConversationReadCommandHandler(store.Db, user, new SendTestClock(),
            new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId }, default);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive-parent")]
    [InlineData("wrong-area")]
    [InlineData("unknown-boundary")]
    [InlineData("anonymous")]
    public async Task DeveloperReadReceiptStillRejectsInvalidScopeOrBoundary(string invalid)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        if (invalid == "foreign") store.Child.CompanyId = Guid.NewGuid();
        if (invalid == "inactive-parent") store.Root.IsActive = false;
        if (invalid == "anonymous") user.IsAuthenticated = false;
        var handler = new MarkInternalConversationReadCommandHandler(store.Db, user, new SendTestClock(),
            new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new()
        {
            ConversationId = store.Child.InternalConversationId,
            AreaCode = invalid == "wrong-area" ? "technical" : "pricing",
            ThroughMessageId = invalid == "unknown-boundary" ? Guid.NewGuid() : null
        }, default);
        Assert.False(result.Success);
    }

    public class ReadReceiptTransaction : DispatchProxy
    {
        public bool Committed { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IDbContextTransaction.CommitAsync))
            {
                Committed = true;
                return Task.CompletedTask;
            }
            if (method.Name == nameof(IAsyncDisposable.DisposeAsync)) return ValueTask.CompletedTask;
            throw new NotSupportedException(method.Name);
        }
    }
}
