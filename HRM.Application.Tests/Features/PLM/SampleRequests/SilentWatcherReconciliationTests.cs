using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SilentWatcherReconciliationTests
{
    [Theory]
    [InlineData(true, false, "Missing")]
    [InlineData(true, true, "NotificationRecipient")]
    [InlineData(false, false, "Ineligible")]
    public void OnlyEligibleNonRecipientWithoutMembershipIsMissing(bool eligible, bool recipient, string expected)
        => Assert.Equal(expected, SilentWatcherReconciliationRules.Status(eligible, recipient, null));

    [Fact]
    public void ExistingMembershipAndRemovalAlwaysWinOverRecalculatedRules()
    {
        Assert.Equal("Removed", SilentWatcherReconciliationRules.Status(true, false, new() { IsActive = false }));
        Assert.Equal("ExistingMember", SilentWatcherReconciliationRules.Status(true, false, new() { IsActive = true, IsMuted = false }));
        Assert.Equal("ExistingWatcher", SilentWatcherReconciliationRules.Status(true, true,
            new() { IsActive = true, IsMuted = true, Role = InternalConversationParticipantRole.Watcher }));
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task UnauthorizedPreviewAndApplyDoNotTouchDatabase(bool authenticated, bool company, bool employee, bool permitted)
    {
        var user = new User(authenticated, company ? Guid.NewGuid() : null, employee ? Guid.NewGuid() : null, permitted);
        var service = new SilentWatcherReconciliationService(null!, null!, user, new CurrentUserPermissionService(user), null!, null!,
            NullLogger<SilentWatcherReconciliationService>.Instance);
        Assert.Null(await service.ExecuteAsync(1, null, false, default));
        Assert.Null(await service.ExecuteAsync(1, new string('A', 64), true, default));
    }

    private sealed record User(bool IsAuthenticated, Guid? CompanyId, Guid? EmployeeId, bool Permitted) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => Permitted ? [ApplicationRoles.Developer] : [ApplicationRoles.Lab.LabUser];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
