using System.Reflection;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public async Task DeliveryStatusStreamGroupsPauseAndResumeAndKeepsScopeAndOtherStreamsSeparate()
    {
        var (service, rows) = SalesOrderStreamFixture(new User("President"), 7);
        rows[0].Topic = TopicNotifications.MerchandiseOrderDeliveryPaused;
        rows[1].Topic = TopicNotifications.MerchandiseOrderDeliveryResumed;
        rows[2].Topic = TopicNotifications.MerchandiseOrderPromisedDeliveryNotified;
        rows[4].Topic = TopicNotifications.MerchandiseOrderDeliveryPaused;
        rows[4].CompanyId = Guid.NewGuid();
        rows[5].Topic = TopicNotifications.MerchandiseOrderDeliveryResumed;
        rows[5].UserStates.Single().IsArchived = true;
        rows[6].Topic = TopicNotifications.MerchandiseOrderDeliveryPaused;
        rows[6].UserStates.Single().UserId = Guid.NewGuid();
        var page = await service.GetSalesOrderStreamAsync(streamKey: "sales-order-delivery-status-stream");
        Assert.Equal(new[] { rows[0].Id, rows[1].Id }, page.Items.Select(item => item.Id));
        Assert.Equal(2, page.UnreadCount);
        Assert.Equal(rows[0].Id, page.FirstUnreadId);
        Assert.False(rows[0].UserStates.Single().IsRead);
        var pricing = await service.GetSalesOrderStreamAsync();
        Assert.Equal(rows[3].Id, Assert.Single(pricing.Items).Id);
        var wrongCursor = await service.GetSalesOrderStreamAsync(rows[2].Id, "older", streamKey: "sales-order-delivery-status-stream");
        Assert.Empty(wrongCursor.Items);
    }

    [Fact]
    public async Task SalesOrderStreamsSeparateDeliveryFromPriceAndRejectCrossStreamCursor()
    {
        var (service, rows) = SalesOrderStreamFixture(new User("President"), 3);
        rows[1].Topic = TopicNotifications.MerchandiseOrderPromisedDeliveryNotified;
        var pricing = await service.GetSalesOrderStreamAsync();
        var delivery = await service.GetSalesOrderStreamAsync(streamKey: "sales-order-delivery-stream");
        Assert.Equal(new[] { rows[0].Id, rows[2].Id }, pricing.Items.Select(n => n.Id));
        Assert.Equal(2, pricing.UnreadCount);
        Assert.Equal(rows[1].Id, Assert.Single(delivery.Items).Id);
        Assert.Equal(1, delivery.UnreadCount);
        var wrongCursor = await service.GetSalesOrderStreamAsync(rows[0].Id, "older",
            streamKey: "sales-order-delivery-stream");
        Assert.Empty(wrongCursor.Items);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSalesOrderStreamAsync(streamKey: "unknown"));
    }

    [Theory]
    [InlineData("President")]
    [InlineData("Developer")]
    [InlineData("LabUser")]
    public async Task SalesOrderStreamKeepsRecipientCompanyArchiveAndConversationBoundaries(string role)
    {
        var user = new User(role);
        var (service, rows) = SalesOrderStreamFixture(user, 7, linkedIndex: 6);
        rows[1].CompanyId = Guid.NewGuid();
        rows[2].UserStates.Single().UserId = Guid.NewGuid();
        rows[3].UserStates.Single().IsArchived = true;
        rows[4].CreatedDate = new DateTime(2025, 1, 1);
        rows[5].Topic = TopicNotifications.SampleRequestPriceQuoteRequested;
        var result = await service.GetSalesOrderStreamAsync();
        Assert.Equal(rows[0].Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.UnreadCount);
        Assert.False(rows[0].UserStates.Single().IsRead); // GET must never mark the stream read.
        var inaccessible = await service.GetSalesOrderStreamAsync(rows[1].Id, "older");
        Assert.Empty(inaccessible.Items);
    }

    [Fact]
    public async Task SalesOrderStreamStartsAtFirstUnreadAndPagesWithoutDuplicatesAtSameTimestamp()
    {
        var user = new User("President");
        var (service, rows) = SalesOrderStreamFixture(user, 65);
        foreach (var row in rows.Take(10)) row.UserStates.Single().IsRead = true;
        var initial = await service.GetSalesOrderStreamAsync();
        Assert.Equal(rows[10].Id, initial.FirstUnreadId);
        Assert.Equal(rows.Skip(10).Take(30).Select(n => n.Id), initial.Items.Select(n => n.Id));
        Assert.Equal(55, initial.UnreadCount);
        Assert.True(initial.HasOlder);
        Assert.True(initial.HasNewer);
        var older = await service.GetSalesOrderStreamAsync(initial.Items[0].Id, "older");
        Assert.Equal(rows.Take(10).Select(n => n.Id), older.Items.Select(n => n.Id));
        var newer = await service.GetSalesOrderStreamAsync(initial.Items[^1].Id, "newer");
        Assert.Equal(rows.Skip(40).Select(n => n.Id), newer.Items.Select(n => n.Id));
        Assert.False(newer.HasNewer);
        Assert.Equal(65, older.Items.Concat(initial.Items).Concat(newer.Items).Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public async Task SalesOrderStreamWithoutUnreadOpensLatestPage()
    {
        var (service, rows) = SalesOrderStreamFixture(new User("President"), 35);
        foreach (var row in rows) row.UserStates.Single().IsRead = true;
        var result = await service.GetSalesOrderStreamAsync();
        Assert.Null(result.FirstUnreadId);
        Assert.Equal(0, result.UnreadCount);
        Assert.Equal(rows.Skip(5).Select(n => n.Id), result.Items.Select(n => n.Id));
        Assert.True(result.HasOlder);
        Assert.False(result.HasNewer);
    }

    private static (NotificationService Service, Notification[] Rows) SalesOrderStreamFixture(User user, int count, int? linkedIndex = null)
    {
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var rows = Enumerable.Range(0, count).Select(i => new Notification {
            Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:D12}"), CompanyId = user.CompanyId!.Value,
            Topic = TopicNotifications.MerchandiseOrderBelowStandardPriceApproved,
            CreatedDate = new DateTime(2026, 11, 8), Title = "DHG test", Message = "Synthetic order notification"
        }).ToArray();
        foreach (var row in rows) row.UserStates.Add(new NotificationUserState {
            NotificationId = row.Id, Notification = row, UserId = user.EmployeeId!.Value
        });
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Notifications"] = new MemorySet<Notification>(rows);
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>(
            rows.Select((row, index) => new InternalMailNotificationLink { NotificationId = row.Id,
                HasConversation = index == linkedIndex,
                ConversationId = index == linkedIndex ? store.Child.InternalConversationId : null }));
        var db = DispatchProxy.Create<INotificationDbContext, MemoryStore>();
        foreach (var entry in memory.Values) ((MemoryStore)(object)db).Values[entry.Key] = entry.Value;
        return (new NotificationService(db, user, null!, null!, new InternalMailAreaAccessService(store.Db, user), null!), rows);
    }
}
