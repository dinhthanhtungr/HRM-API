using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Domain.Enums.Deliveries;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderLifecycleRulesTests
{
    [Theory]
    [InlineData(DeliveryOrderStatus.Pending, DeliveryOrderStatus.Pending, true)]
    [InlineData(DeliveryOrderStatus.Pending, DeliveryOrderStatus.InProgress, true)]
    [InlineData(DeliveryOrderStatus.Pending, DeliveryOrderStatus.Completed, false)]
    [InlineData(DeliveryOrderStatus.Pending, DeliveryOrderStatus.Canceled, true)]
    [InlineData(DeliveryOrderStatus.InProgress, DeliveryOrderStatus.Completed, true)]
    [InlineData(DeliveryOrderStatus.InProgress, DeliveryOrderStatus.Canceled, true)]
    [InlineData(DeliveryOrderStatus.InProgress, DeliveryOrderStatus.Pending, false)]
    [InlineData(DeliveryOrderStatus.Completed, DeliveryOrderStatus.Completed, true)]
    [InlineData(DeliveryOrderStatus.Completed, DeliveryOrderStatus.Canceled, false)]
    [InlineData(DeliveryOrderStatus.Canceled, DeliveryOrderStatus.Canceled, true)]
    [InlineData(DeliveryOrderStatus.Canceled, DeliveryOrderStatus.Pending, false)]
    public void CanTransition_UsesExpectedLifecycle(
        DeliveryOrderStatus current,
        DeliveryOrderStatus target,
        bool expected)
    {
        Assert.Equal(expected, DeliveryOrderLifecycleRules.CanTransition(current, target));
    }

    [Theory]
    [InlineData("Pending", DeliveryOrderStatus.Pending)]
    [InlineData("inprogress", DeliveryOrderStatus.InProgress)]
    [InlineData("Cancelled", DeliveryOrderStatus.Canceled)]
    [InlineData("Canceled", DeliveryOrderStatus.Canceled)]
    public void TryNormalizeStatus_AcceptsCanonicalAndLegacyValues(
        string input,
        DeliveryOrderStatus expected)
    {
        Assert.True(DeliveryOrderLifecycleRules.TryNormalizeStatus(input, out var status));
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Canceled", false)]
    [InlineData("Unknown", false)]
    public void CanEditContent_AllowsOnlyPending(string status, bool expected)
    {
        Assert.Equal(expected, DeliveryOrderLifecycleRules.CanEditContent(status));
    }
}
