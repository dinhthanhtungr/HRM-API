using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;

namespace HRM.Application.Tests.Features.Notifications;

public sealed class NotificationArchiveMatchTests
{
    [Fact]
    public void ThreadArchive_MatchesAllItsEventsButDeleteOnlyMatchesItsMessage()
    {
        var thread = Guid.NewGuid();
        var message = Guid.NewGuid();
        var target = Payload(new { conversationId = thread, messageId = message });
        Assert.True(NotificationArchiveMatch.Conversation(target, thread));
        Assert.True(NotificationArchiveMatch.Conversation(target, thread, message));
        Assert.False(NotificationArchiveMatch.Conversation(target, Guid.NewGuid()));
        Assert.False(NotificationArchiveMatch.Conversation(target, thread, Guid.NewGuid()));
        Assert.True(NotificationArchiveMatch.Conversation(Payload(new { ConversationId = thread, MessageId = message }), thread, message));
        Assert.False(NotificationArchiveMatch.Conversation(new NotificationDto { PayloadJson = "{bad" }, thread));
    }

    [Fact]
    public void GroupArchive_MatchesOtherPagesButExcludesOtherAggregatesAndThreads()
    {
        var aggregateId = Guid.NewGuid();
        var anchor = Payload(new { context = new { aggregateType = "SampleRequest", aggregateId } });
        Assert.True(NotificationArchiveMatch.Group(Payload(new { context = new { aggregateType = "SampleRequest", aggregateId } }), anchor));
        Assert.False(NotificationArchiveMatch.Group(Payload(new { context = new { aggregateType = "Quotation", aggregateId } }), anchor));
        Assert.False(NotificationArchiveMatch.Group(Payload(new { context = new { aggregateType = "SampleRequest", aggregateId = Guid.NewGuid() } }), anchor));
        Assert.False(NotificationArchiveMatch.Group(Payload(new { conversationId = Guid.NewGuid(), context = new { aggregateType = "SampleRequest", aggregateId } }), anchor));
        Assert.False(NotificationArchiveMatch.Group(new NotificationDto { Id = Guid.NewGuid(), PayloadJson = "invalid" }, anchor));
    }

    [Fact]
    public void UngroupedNotification_OnlyArchivesItself()
    {
        var anchor = new NotificationDto { Id = Guid.NewGuid() };
        Assert.True(NotificationArchiveMatch.Group(anchor, anchor));
        Assert.False(NotificationArchiveMatch.Group(new NotificationDto { Id = Guid.NewGuid() }, anchor));
    }

    private static NotificationDto Payload(object payload) => new()
    {
        Id = Guid.NewGuid(),
        PayloadJson = JsonSerializer.Serialize(payload)
    };
}
