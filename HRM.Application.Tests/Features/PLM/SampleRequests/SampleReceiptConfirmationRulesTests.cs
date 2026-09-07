using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleReceiptConfirmationRulesTests
{
    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser)]
    [InlineData(ApplicationRoles.Developer)]
    [InlineData(ApplicationRoles.President)]
    [InlineData(ApplicationRoles.Sales.SaleAdmin)]
    public void CanConfirm_AllowsOnlyConfiguredRoles(string role)
        => Assert.True(SampleReceiptConfirmationRules.CanConfirm(new CurrentUser(role)));

    [Theory]
    [InlineData(ApplicationRoles.Lab.LabUser)]
    [InlineData(ApplicationRoles.Leader)]
    [InlineData(ApplicationRoles.Admin)]
    public void CanConfirm_RejectsOtherRoles(string role)
        => Assert.False(SampleReceiptConfirmationRules.CanConfirm(new CurrentUser(role)));

    [Fact]
    public void CanConfirm_AllowsOnlyTeamLeaderThroughScopedOverload()
    {
        var teamLeader = new CurrentUser(ApplicationRoles.User);

        Assert.True(SampleReceiptConfirmationRules.CanConfirm(teamLeader, isSampleRequestTeamLeader: true));
        Assert.False(SampleReceiptConfirmationRules.CanConfirm(teamLeader, isSampleRequestTeamLeader: false));
    }

    [Fact]
    public void ResolveReceivedDate_DefaultsToBackendNow()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.Equal(now, SampleReceiptConfirmationRules.ResolveReceivedDate(null, now));
    }

    [Fact]
    public void ResolveReceivedDate_PreservesSelectedDate()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);
        var selected = new DateTime(2026, 8, 12, 9, 15, 0);

        Assert.Equal(selected, SampleReceiptConfirmationRules.ResolveReceivedDate(selected, now));
    }

    [Theory]
    [InlineData(SampleTrialStatus.SampleSent)]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback)]
    public void Validate_AllowsSentTrialsAwaitingFeedback(SampleTrialStatus status)
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.Null(SampleReceiptConfirmationRules.Validate(status, now, now));
    }

    [Fact]
    public void Validate_RejectsFutureDateBeyondClockTolerance()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.NotNull(SampleReceiptConfirmationRules.Validate(
            SampleTrialStatus.SampleSent,
            now.AddMinutes(6),
            now));
    }

    [Fact]
    public void Validate_RejectsTerminalTrial()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.NotNull(SampleReceiptConfirmationRules.Validate(
            SampleTrialStatus.Approved,
            now,
            now));
    }

    private sealed class CurrentUser(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => "test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = [role];
        public bool IsInRole(string targetRole)
            => Roles.Contains(targetRole, StringComparer.OrdinalIgnoreCase);
    }
}
