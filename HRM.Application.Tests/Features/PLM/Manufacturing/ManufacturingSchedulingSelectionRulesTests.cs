using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingSchedulingSelectionRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0);

    [Fact]
    public void Scheduling_ActivatesPendingSelectionAndRetryPreservesTimestamp()
    {
        var selection = new ProductionSelectVersion();
        Assert.True(ManufacturingSchedulingSelectionRules.Activate([selection], Now).Success);
        Assert.Equal(Now, selection.ValidFrom);
        Assert.True(ManufacturingSchedulingSelectionRules.Activate([selection], Now.AddDays(1)).Success);
        Assert.Equal(Now, selection.ValidFrom);
    }

    [Fact]
    public void ExistingEffectiveSelection_IsPreservedWithoutActivatingOtherPendingVa()
    {
        var selected = new ProductionSelectVersion { ValidFrom = Now.AddDays(-1) };
        var pending = new ProductionSelectVersion();
        Assert.True(ManufacturingSchedulingSelectionRules.Activate([pending, selected], Now).Success);
        Assert.Equal(Now.AddDays(-1), selected.ValidFrom);
        Assert.Null(pending.ValidFrom);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MissingOrAmbiguousSelection_FailsWithoutActivating(int count)
    {
        var rows = Enumerable.Range(0, count).Select(_ => new ProductionSelectVersion()).ToArray();
        Assert.False(ManufacturingSchedulingSelectionRules.Activate(rows, Now).Success);
        Assert.All(rows, row => Assert.Null(row.ValidFrom));
    }

    [Fact]
    public void MultipleEffectiveSelections_Fail()
        => Assert.False(ManufacturingSchedulingSelectionRules.Activate([
            new() { ValidFrom = Now }, new() { ValidFrom = Now.AddDays(-1) }], Now).Success);
}
