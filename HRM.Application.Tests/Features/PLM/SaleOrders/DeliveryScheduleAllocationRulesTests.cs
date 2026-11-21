using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class DeliveryScheduleAllocationRulesTests
{
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateOnly Date = new(2026, 11, 20);
    private static DeliveryScheduleCapacity[] Capacities(decimal reserved = 0) =>
        [new(First, 400, reserved, Date, Date), new(Second, 600, 0, Date.AddDays(5), null)];

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void UiRequiresStorageAndExplicitUiOptIn(bool storage, bool ui, bool expected)
        => Assert.Equal(expected, new DeliveryScheduleOptions { StorageReady = storage, EnableMultiScheduleUi = ui }.CanShowMultiScheduleUi);

    [Fact]
    public void DefaultConfigurationKeepsLegacyFlow()
    {
        var options = new DeliveryScheduleOptions();
        Assert.False(options.StorageReady);
        Assert.False(options.CanShowMultiScheduleUi);
    }

    [Fact]
    public void OneDeliveryLineCanCoverTwoDatesWithoutSplittingLotsOrBags()
    {
        var result = DeliveryScheduleAllocationRules.Suggest(500, Capacities());
        Assert.Equal(new[] { new DeliveryScheduleAllocationPart(First, 400), new DeliveryScheduleAllocationPart(Second, 100) }, result.Items);
        Assert.Equal(0, result.UnscheduledQuantity);
    }

    [Fact]
    public void ExistingPendingAllocationReducesCapacity()
    {
        var result = DeliveryScheduleAllocationRules.Suggest(500, Capacities(150));
        Assert.Equal(250, result.Items[0].Quantity);
        Assert.Equal(250, result.Items[1].Quantity);
    }

    [Fact]
    public void SourceScheduleIsPreferredWhenCreatingFromASpecificPlan()
    {
        var result = DeliveryScheduleAllocationRules.Suggest(100, Capacities(), Second);
        Assert.Equal(new DeliveryScheduleAllocationPart(Second, 100), Assert.Single(result.Items));
    }

    [Fact]
    public void ExcessDeliveryRemainsVisibleInsteadOfSilentlyOverbookingSchedule()
    {
        var result = DeliveryScheduleAllocationRules.Suggest(1200, Capacities());
        Assert.Equal(1000, result.Items.Sum(x => x.Quantity));
        Assert.Equal(200, result.UnscheduledQuantity);
    }

    [Fact]
    public void NoSchedulesMeansLegacyQuantityIsUnscheduledNotRejected()
    {
        var result = DeliveryScheduleAllocationRules.Suggest(500, []);
        Assert.Empty(result.Items);
        Assert.Equal(500, result.UnscheduledQuantity);
    }

    [Fact]
    public void PromisedDateTakesPrecedenceAndMissingPromiseFallsBackToRequestedDate()
    {
        DeliveryScheduleCapacity[] schedules = [new(First, 400, 0, Date, Date.AddDays(10)), new(Second, 600, 0, Date.AddDays(5), null)];
        Assert.Equal(Second, DeliveryScheduleAllocationRules.Suggest(100, schedules).Items[0].ScheduleId);
    }

    [Fact]
    public void FullyAllocatedScheduleIsSkippedAndInputOrderDoesNotChangeResult()
    {
        var input = Capacities(400);
        Assert.Equal(Second, Assert.Single(DeliveryScheduleAllocationRules.Suggest(100, input.Reverse().ToArray()).Items).ScheduleId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0001)]
    public void InvalidDeliveryQuantityIsRejected(decimal quantity)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryScheduleAllocationRules.Suggest(quantity, Capacities()));

    [Fact]
    public void DuplicateAndForeignScheduleReferencesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.Suggest(100, [Capacities()[0], Capacities()[0]]));
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.Suggest(100, Capacities(), Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.ValidateManual(100, Capacities(), [new(Guid.NewGuid(), 100)]));
    }

    [Fact]
    public void ManualAllocationCanUseAnAlternativeScheduleButCannotOverbook()
    {
        var result = DeliveryScheduleAllocationRules.ValidateManual(100, Capacities(), [new(Second, 100)]);
        Assert.Equal(Second, Assert.Single(result.Items).ScheduleId);
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.ValidateManual(500, Capacities(), [new(First, 500)]));
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.ValidateManual(100, Capacities(), [new(First, 101)]));
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.ValidateManual(100, Capacities(), [new(First, 50), new(First, 50)]));
    }
}
