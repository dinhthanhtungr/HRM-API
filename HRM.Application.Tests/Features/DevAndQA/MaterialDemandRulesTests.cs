using HRM.Application.Features.DevAndQA.MaterialEquivalences;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class MaterialDemandRulesTests
{
    [Theory]
    [InlineData(80, 100, 80, 0)]
    [InlineData(80, 100, 130, 30)]
    [InlineData(80, 0, 80, 80)]
    [InlineData(80, 50, 0, 30)]
    public void DoesNotSubtractThisDemandReservationTwice(decimal need, decimal stock, decimal reserved, decimal expected)
        => Assert.Equal(expected, MaterialDemandRules.Shortage(need, stock, reserved));

    [Fact]
    public void UsesOnlyRemainingOpenWorkAndAllowsPlpuWithoutGrantingOtherRoles()
    {
        Assert.Equal(30m, MaterialDemandRules.Remaining(50, 20));
        Assert.Equal(0m, MaterialDemandRules.Remaining(10, 20));
        Assert.DoesNotContain("Canceled", MaterialDemandRules.ActiveStatuses);
        Assert.DoesNotContain("Finished", MaterialDemandRules.ActiveStatuses);
        Assert.DoesNotContain("Done", MaterialDemandRules.ActiveStatuses);
        Assert.DoesNotContain("Stocked", MaterialDemandRules.ActiveStatuses);
        Assert.Contains("Running", MaterialDemandRules.ActiveStatuses);
        Assert.Contains(ApplicationRoles.Production.PLPUUser, ApplicationRoleSets.DevAndQA.MaterialPlanningViewers);
        Assert.DoesNotContain(ApplicationRoles.Lab.LabUser, ApplicationRoleSets.DevAndQA.MaterialPlanningViewers);
    }
}
