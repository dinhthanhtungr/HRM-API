using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.DeactivateSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestDeactivationTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Employee = Guid.NewGuid();
    private static SampleRequest NewRequest() => new()
    {
        CompanyId = Company, ManagerBy = Employee, IsActive = true, Status = "New"
    };

    private static bool Eligible(SampleRequest request, Guid? company = null, Guid? employee = null)
        => SampleRequestDeactivationRules.Eligible(new[] { request }.AsQueryable(),
            company ?? Company, employee ?? Employee).Any();

    [Theory]
    [InlineData("New", true)]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Cancelled", true)]
    [InlineData("SampleSent", false)]
    [InlineData("samplesent", false)]
    [InlineData("Completed", false)]
    [InlineData("COMPLETED", false)]
    public void StatusGuard(string status, bool allowed)
    {
        var request = NewRequest();
        request.Status = status;
        Assert.Equal(allowed, Eligible(request));
    }

    [Fact]
    public void RequiresActiveOwnedRecordAndNonEmptyCompanyEmployee()
    {
        var request = NewRequest();
        Assert.True(Eligible(request));
        Assert.False(Eligible(request, Guid.NewGuid()));
        Assert.False(Eligible(request, employee: Guid.NewGuid()));
        Assert.False(Eligible(request, Guid.Empty));
        Assert.False(Eligible(request, employee: Guid.Empty));
        request.IsActive = false;
        Assert.False(Eligible(request));
    }

    [Fact]
    public void LegacySendEvidenceBlocksEvenAfterStatusChanged()
    {
        var request = NewRequest();
        request.SendDate = DateTime.Now;
        Assert.False(Eligible(request));
        request.SendDate = null;
        request.RealDeliveryDate = DateTime.Now;
        Assert.False(Eligible(request));
    }

    [Theory]
    [InlineData(SampleTrialStatus.Draft, true)]
    [InlineData(SampleTrialStatus.PriceQuote, true)]
    [InlineData(SampleTrialStatus.SampleSent, false)]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback, false)]
    [InlineData(SampleTrialStatus.Approved, false)]
    [InlineData(SampleTrialStatus.Failed, false)]
    [InlineData(SampleTrialStatus.ReworkRequested, false)]
    public void TrialHistoryCannotBeBypassedByInactivation(SampleTrialStatus status, bool allowed)
    {
        var request = NewRequest();
        request.SampleRequestSampleTrials.Add(new() { Status = status, IsActive = false });
        Assert.Equal(allowed, Eligible(request));
    }

    [Fact]
    public void CancelledTrialWithSendOrReceiptDateStillBlocks()
    {
        var request = NewRequest();
        var trial = new SampleRequestSampleTrial { Status = SampleTrialStatus.Cancelled, SentDate = DateTime.Now };
        request.SampleRequestSampleTrials.Add(trial);
        Assert.False(Eligible(request));
        trial.SentDate = null;
        trial.RequestReceivedDate = DateTime.Now;
        Assert.False(Eligible(request));
    }

    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser, true)]
    [InlineData(ApplicationRoles.Lab.LabUser, false)]
    [InlineData(ApplicationRoles.President, false)]
    [InlineData(ApplicationRoles.Developer, false)]
    public void PermissionDefaultsOnlyToSale(string role, bool allowed)
    {
        var user = new User { Roles = [role] };
        Assert.Equal(allowed, new CurrentUserPermissionService(user)
            .HasPermission(ApplicationPermissions.PLM.DeactivateSampleRequests));
    }

    [Fact]
    public async Task UnauthenticatedOrExplicitlyDeniedFailsBeforeDatabaseAccess()
    {
        foreach (var user in new[]
        {
            new User { IsAuthenticated = false },
            new User { HasExplicitPermissionSet = true },
            new User { Roles = [ApplicationRoles.Lab.LabUser] }
        })
        {
            var permissions = new CurrentUserPermissionService(user);
            Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.DeactivateSampleRequests));
            Assert.False(permissions.HasPermission("unknown.permission"));
            var handler = new DeactivateSampleRequestCommandHandler(null!, user, permissions, null!);
            var result = await handler.Handle(new() { SampleRequestId = Guid.NewGuid() }, default);
            Assert.False(result.Success);
        }
    }

    [Fact]
    public void PredicateTranslatesCompanyOwnerAndTrialGuardToPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SampleRequestDeactivationRules.Eligible(db.SampleRequests, Company, Employee).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("ManagerBy", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("SentDate", sql);
        Assert.Contains("RealDeliveryDate", sql);
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => Employee;
        public Guid? CompanyId => Company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Sales.SaleUser];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
