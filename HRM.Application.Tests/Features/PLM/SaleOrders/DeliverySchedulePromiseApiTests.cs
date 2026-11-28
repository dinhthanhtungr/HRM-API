using System.Text.Json;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed partial class DeliveryScheduleServiceTests
{
    [Theory]
    [InlineData(0, DeliveryDateMode.Single, DeliveryDateSource.Legacy)]
    [InlineData(1, DeliveryDateMode.Single, DeliveryDateSource.Schedules)]
    [InlineData(2, DeliveryDateMode.Split, DeliveryDateSource.Schedules)]
    public async Task ReadExplicitlyDistinguishesLegacySingleStoredSingleAndSplit(int count, DeliveryDateMode mode, DeliveryDateSource source)
    {
        var f = new Fixture();
        if (count < 2) f.B.IsActive = false;
        if (count == 0) f.A.IsActive = false;
        if (count == 1) f.A.Quantity = 1000;
        f.Detail.ExpectedDeliveryDate = new DateTime(2026, 12, 5);
        f.A.PromisedDate = new DateTime(2026, 12, 2);
        var workspace = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var summary = workspace.DeliveryDates;
        Assert.Equal(mode, summary.Mode);
        Assert.Equal(source, summary.Source);
        Assert.Equal(count > 0, summary.CanUpdatePromises);
        Assert.Equal(Math.Max(1, count), summary.Items.Count);
        Assert.Equal(1000m, summary.Items.Sum(x => x.Quantity));
        if (count == 0)
        {
            var row = Assert.Single(summary.Items);
            Assert.Null(row.ScheduleId);
            Assert.False(row.IsStored);
            Assert.Equal(workspace.LegacyRequestedDate, row.RequestedDate);
            Assert.Equal(new DateOnly(2026, 12, 5), row.PromisedDate);
            Assert.Empty(workspace.Items);
        }
        else
        {
            Assert.All(summary.Items, x => Assert.True(x.IsStored));
            var row = Assert.Single(summary.Items, x => x.ScheduleId == f.A.MerchandiseOrderDeliveryScheduleId);
            Assert.Equal(new DateOnly(2026, 12, 2), row.PromisedDate);
            Assert.Equal(DateOnly.FromDateTime(f.A.RequestedDate), row.RequestedDate);
            if (count == 2) Assert.Null(Assert.Single(summary.Items, x => x.ScheduleId == f.B.MerchandiseOrderDeliveryScheduleId).PromisedDate);
        }
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(workspace, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(mode.ToString(), json.RootElement.GetProperty("deliveryDates").GetProperty("mode").GetString());
        Assert.Equal(source.ToString(), json.RootElement.GetProperty("deliveryDates").GetProperty("source").GetString());
        Assert.True(json.RootElement.TryGetProperty("legacyRequestedDate", out _));
    }

    [Fact]
    public async Task DisabledReadHasNoSyntheticLegacyDate()
    {
        var f = new Fixture(); f.Options.Value.EnableMultiScheduleUi = false;
        var workspace = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        Assert.Equal(DeliveryDateMode.Unavailable, workspace.DeliveryDates.Mode);
        Assert.Equal(DeliveryDateSource.Unavailable, workspace.DeliveryDates.Source);
        Assert.Empty(workspace.DeliveryDates.Items);
        Assert.False(workspace.DeliveryDates.CanUpdatePromises);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromiseApiPreservesRequestAndUnmentionedSchedulesAndNotifiesStoredSingleOrSplit(bool single)
    {
        var f = new Fixture(true); f.User.Roles = ["PLPUUser"];
        if (single) { f.B.IsActive = false; f.A.Quantity = 1000; }
        f.A.RequestNote = "request note"; f.A.PromiseNote = "promise note";
        f.B.PromisedDate = new DateTime(2026, 12, 3);
        var originalRequest = f.A.RequestedDate;
        var originalQuantity = f.A.Quantity;
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var result = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 1))]), default);
        Assert.True(result.Success, result.Message);
        Assert.True(f.Store.Transaction.Committed);
        Assert.NotEqual(current.Revision, result.Data!.Revision);
        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(result.Data.Items, x => x.Id == f.A.MerchandiseOrderDeliveryScheduleId).PromisedDate);
        Assert.Equal(originalRequest, f.A.RequestedDate);
        Assert.Equal(originalQuantity, f.A.Quantity);
        Assert.Equal("request note", f.A.RequestNote); Assert.Equal("promise note", f.A.PromiseNote);
        Assert.Equal(new DateTime(2026, 12, 3), f.B.PromisedDate);
        Assert.Equal(12345, f.Detail.UnitPriceAgreed);
        Assert.Single(f.Notifications!.Published);
    }

    [Fact]
    public async Task PromiseApiClearsOnlyExplicitNullAndRetryDoesNotNotifyAgain()
    {
        var f = new Fixture(true);
        f.A.PromisedDate = new DateTime(2026, 12, 1);
        f.B.PromisedDate = new DateTime(2026, 12, 3);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var changed = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, null)]), default);
        Assert.True(changed.Success, changed.Message);
        Assert.Null(f.A.PromisedDate);
        Assert.Equal(new DateTime(2026, 12, 3), f.B.PromisedDate);
        var repeat = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(changed.Data!.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, null)]), default);
        Assert.True(repeat.Success, repeat.Message);
        Assert.Single(f.Notifications!.Published);
        Assert.Equal(1, f.Store.Saves);
    }

    [Theory]
    [InlineData("sale")]
    [InlineData("anonymous")]
    [InlineData("explicit-no-permission")]
    [InlineData("company")]
    [InlineData("ownership")]
    [InlineData("closed")]
    [InlineData("disabled")]
    [InlineData("stale")]
    [InlineData("unknown-id")]
    [InlineData("duplicate-id")]
    [InlineData("legacy")]
    [InlineData("unreconciled")]
    public async Task PromiseApiRejectsInvalidScopeAndStaleChangesBeforeWriting(string scenario)
    {
        var f = new Fixture(true);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        if (scenario == "sale") f.User.Roles = ["SaleUser"];
        if (scenario == "anonymous") f.User.IsAuthenticated = false;
        if (scenario == "explicit-no-permission") f.User.HasExplicitPermissionSet = true;
        if (scenario == "company") f.Order.CompanyId = Guid.NewGuid();
        if (scenario == "ownership")
        {
            f.User.Roles = ["SaleUser"]; f.User.HasExplicitPermissionSet = true;
            f.User.Permissions = [HRM.Application.Commons.Authorization.ApplicationPermissions.PLM.EditPromisedDeliverySchedule];
            f.Scope.CustomerId = Guid.NewGuid();
        }
        if (scenario == "closed") f.Order.Status = "Completed";
        if (scenario == "disabled") f.Options.Value.EnableMultiScheduleUi = false;
        if (scenario == "legacy") { f.A.IsActive = false; f.B.IsActive = false; }
        if (scenario == "unreconciled")
        {
            var delivery = new HRM.Domain.Entities.DeliverySchema.DeliveryOrder { Id = Guid.NewGuid(), CompanyId = f.Company, Status = "Pending" };
            f.Store.Seed(nameof(f.Db.DeliveryOrderDetails), new HRM.Domain.Entities.DeliverySchema.DeliveryOrderDetail
            { Id = Guid.NewGuid(), DeliveryOrderId = delivery.Id, DeliveryOrder = delivery, MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, Quantity = 100 });
        }
        // Use the latest revision for scope/legacy/lifecycle cases, so stale revision does not hide a missing guard.
        var latest = await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default);
        var input = new DeliveryPromiseDateInput(scenario == "unknown-id" ? Guid.NewGuid() : f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 1));
        var result = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(scenario == "stale" ? new string('0', 64) : latest?.Revision ?? current.Revision,
                scenario == "duplicate-id" ? [input, input]
                    : scenario == "unknown-id" ? [new(f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 2)), input] : [input]), default);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(0, f.Store.Saves);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Empty(f.Notifications!.Published);
    }

    [Fact]
    public async Task MultiplePromiseChangesCommitTogetherAndPreserveSameDaySeparateSchedules()
    {
        var f = new Fixture(true);
        f.B.RequestedDate = f.A.RequestedDate;
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        Assert.Equal(DeliveryDateMode.Split, current.DeliveryDates.Mode);
        var result = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 1)),
                new(f.B.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 3))]), default);
        Assert.True(result.Success, result.Message);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(2, f.Notifications!.Published.Count);
        Assert.Equal(1, f.Store.Saves);
        Assert.Equal(DeliveryDateMode.Split, result.Data!.DeliveryDates.Mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromiseApiRollsBackWhenPersistenceOrNotificationFails(bool publishFailure)
    {
        var f = new Fixture(true);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        f.Store.ThrowOnSave = !publishFailure;
        f.Notifications!.ThrowOnPublish = publishFailure;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 1))]), default));
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.Empty(f.Notifications.Published);
    }

    [Fact]
    public async Task PromiseApiDoesNotCommitOrNotifyWhenFinalLifecycleCheckFails()
    {
        var f = new Fixture(true);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        f.Store.BeforeSave = () => f.Order.Status = "Delivered";
        var result = await f.Service.UpdatePromisesAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new DateOnly(2026, 12, 1))]), default);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.Empty(f.Notifications!.Published);
    }

    [Fact]
    public void PromisePatchRequiresDatePresenceButAllowsExplicitNull()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var id = Guid.NewGuid();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateDeliveryPromisesRequest>("{}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateDeliveryPromisesRequest>(
            $$"""{"revision":"r","items":[{"scheduleId":"{{id}}"}]}""", options));
        var input = JsonSerializer.Deserialize<UpdateDeliveryPromisesRequest>(
            $$"""{"revision":"r","items":[{"scheduleId":"{{id}}","promisedDate":null}]}""", options)!;
        Assert.Null(Assert.Single(input.Items).PromisedDate);
    }
}
