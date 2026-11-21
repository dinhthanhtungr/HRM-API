using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RankedIconsKeepExactMessageTieBreakAndCurrentAccess(bool mapped)
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        SeedMappings(store.Db);
        var message = store.Db.InternalMessages.Single();
        var first = new Notification { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CompanyId = user.CompanyId!.Value, CreatedDate = new DateTime(2026, 11, 1), Topic = TopicNotifications.SampleRequestPriceQuoteRequested };
        var winner = new Notification { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            CompanyId = first.CompanyId, CreatedDate = first.CreatedDate, Topic = TopicNotifications.QuotationSent };
        var chat = new Notification { Id = Guid.NewGuid(), CompanyId = first.CompanyId,
            CreatedDate = first.CreatedDate.AddDays(1), Topic = TopicNotifications.SampleRequestMessageCreated };
        var unbound = new Notification { Id = Guid.NewGuid(), CompanyId = first.CompanyId,
            CreatedDate = first.CreatedDate.AddDays(2), Topic = TopicNotifications.SampleRequestPriceQuoteRequested };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.Notifications)] = new MemorySet<Notification>([first, winner, chat, unbound]);
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>(
            new[] { first, winner, chat, unbound }.Select(n => new InternalMailNotificationLink {
                NotificationId = n.Id, ConversationId = message.InternalConversationId, HasConversation = true,
                MessageId = n == unbound ? Guid.NewGuid() : message.InternalMessageId }));
        var access = new InternalMailAreaAccessService(store.Db, user,
            Options.Create(new InternalMailGroupStorageOptions { Enabled = mapped }));
        var ids = new[] { store.Child.InternalConversationId };
        var selected = Assert.Single(access.LatestVisibleMessageBusinessEvents(ids));
        Assert.Equal(winner.Id, selected.Id);
        Assert.Equal(winner.Topic, selected.Topic);
        Assert.Empty(access.LatestVisibleMessageBusinessEvents([]));
        message.IsDeleted = true;
        Assert.Empty(access.LatestVisibleMessageBusinessEvents(ids));
        message.IsDeleted = false;
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.LatestVisibleMessageBusinessEvents(ids));
        store.Child.Participants.Single().IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.LatestVisibleMessageBusinessEvents(ids));
    }

    [Fact]
    public void RankedIconSqlKeepsParametersAndChoosesWinnerOnce()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = db.QueryLatestInternalMailBusinessEvents(Guid.NewGuid(), [Guid.NewGuid()], [23, 38]).ToQueryString();
        Assert.Contains("DISTINCT ON", sql);
        Assert.Contains("ANY", sql);
        Assert.Contains("\"IsDeleted\"", sql);
        Assert.Contains("created_date DESC, n.id DESC", sql);
    }
}
