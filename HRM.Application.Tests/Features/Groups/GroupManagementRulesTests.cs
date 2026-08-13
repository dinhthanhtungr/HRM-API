using HRM.Application.Features.Groups;

namespace HRM.Application.Tests.Features.Groups;

public sealed class GroupManagementRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Last_leader_is_protected(
        int activeLeaderCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            GroupManagementRules.CanRemoveLeader(activeLeaderCount));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" Sales Team ", "Sales Team")]
    public void Optional_group_text_is_normalized(string? input, string? expected)
    {
        Assert.Equal(expected, GroupManagementRules.TrimToNull(input));
    }
}
