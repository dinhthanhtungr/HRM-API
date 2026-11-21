using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.CorrectSampleTrialFeedback;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleTrialFeedbackRevisions;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialFeedbackCorrectionTests
{
    [Fact]
    public async Task AnonymousHistoryAccessIsRejectedBeforeStorage()
    {
        var user = new User("SaleUser", false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetSampleTrialFeedbackRevisionsQueryHandler(null!, user, null!, null!)
                .Handle(new(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }
    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("LabUser", false)]
    [InlineData("LabAdmin", false)]
    [InlineData("Admin", false)]
    [InlineData("Developer", true)]
    public async Task CapabilityIsSeparateFromTechnicalAndAdministrativeRoles(string role, bool allowed)
    {
        var user = new User(role);
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(allowed, permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback));
        if (!allowed)
            Assert.False((await new CorrectSampleTrialFeedbackCommandHandler(null!, user, permissions, null!, null!)
                .Handle(new(), CancellationToken.None)).Success);
        Assert.False(new CurrentUserPermissionService(new User(role, false))
            .HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback));
    }

    [Fact]
    public void OnlyOwnerAndLatestRecordedFeedbackAreEditable()
    {
        var owner = Guid.NewGuid();
        Assert.True(SampleTrialFeedbackCorrectionRules.CanCorrect(true, owner, owner, 2, 2, true));
        Assert.False(SampleTrialFeedbackCorrectionRules.CanCorrect(true, owner, owner, 1, 2, true));
        Assert.False(SampleTrialFeedbackCorrectionRules.CanCorrect(true, Guid.NewGuid(), owner, 2, 2, true));
        Assert.False(SampleTrialFeedbackCorrectionRules.CanCorrect(true, owner, owner, 2, 2, false));
        Assert.False(SampleTrialFeedbackCorrectionRules.CanCorrect(false, owner, owner, 2, 2, true));
        Assert.NotNull(SampleTrialFeedbackCorrectionRules.Validate(default, null, null));
        Assert.NotNull(SampleTrialFeedbackCorrectionRules.Validate(new(2026, 11, 1), new(2026, 11, 2), null));
        Assert.NotNull(SampleTrialFeedbackCorrectionRules.Validate(new(2026, 11, 2), null, new string('a', 5001)));
        Assert.Null(SampleTrialFeedbackCorrectionRules.Validate(new(2026, 11, 2), new(2026, 11, 2, 15, 0, 0), null));
        Assert.DoesNotContain(typeof(CorrectSampleTrialFeedbackCommand).GetProperties(), x =>
            x.Name is "CustomerReplyStatus" or "FormulaId" or "SentDate");
    }

    [Fact]
    public async Task HistoryIsScopedAndPreservesBothValuesAndActor()
    {
        var user = new User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, Status = "Completed", IsActive = true };
        var first = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId,
            SampleRequestSampleTrialId = Guid.NewGuid(), TrialNo = 1, CustomerReplyDate = new(2026, 11, 1) };
        var latest = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId,
            SampleRequestSampleTrialId = Guid.NewGuid(), TrialNo = 2, CustomerReplyDate = new(2026, 11, 2) };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.SampleRequestSampleTrials), first, latest);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = user.CompanyId.Value, FullName = "Sales owner" });
        AuditLog Audit(Guid company) => new() { CompanyId = company, SchemaName = "SampleRequests",
            TableName = SampleTrialFeedbackCorrectionRules.AuditTable, RecordId = latest.SampleRequestSampleTrialId,
            Reason = SampleTrialFeedbackCorrectionRules.AuditReason, ChangedBy = user.EmployeeId, ChangedAt = new(2026, 11, 3),
            OldValues = JsonSerializer.SerializeToDocument(new { CustomerReplyDate = new DateTime(2026, 11, 1), CustomerReplyNote = "Before" }),
            NewValues = JsonSerializer.SerializeToDocument(new { CustomerReplyDate = new DateTime(2026, 11, 2), CustomerReplyNote = "After" }) };
        store.Seed(nameof(db.AuditLogs), Audit(user.CompanyId.Value), Audit(Guid.NewGuid()));
        var permissions = new CurrentUserPermissionService(user);
        var handler = new GetSampleTrialFeedbackRevisionsQueryHandler(db, user, permissions, Visibility.Create(user));
        var result = await handler.Handle(new(request.SampleRequestId, latest.SampleRequestSampleTrialId), CancellationToken.None);
        Assert.True(result!.CanEdit);
        var revision = Assert.Single(result.History.Items);
        Assert.Equal("Before", revision.OldNote);
        Assert.Equal("After", revision.NewNote);
        Assert.Equal("Sales owner", revision.ChangedByName);
        Assert.False((await handler.Handle(new(request.SampleRequestId, first.SampleRequestSampleTrialId), CancellationToken.None))!.CanEdit);
        Assert.Null(await handler.Handle(new(request.SampleRequestId, Guid.NewGuid()), CancellationToken.None));
        request.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(new(request.SampleRequestId, latest.SampleRequestSampleTrialId), CancellationToken.None));
    }

    [Fact]
    public async Task MutationRejectsOtherCompanyOwnerAndOlderTrialBeforeAnyWrite()
    {
        var user = new User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, IsActive = true };
        var trial = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId,
            SampleRequestSampleTrialId = Guid.NewGuid(), TrialNo = 2, CustomerReplyDate = new(2026, 11, 1) };
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.SampleRequestSampleTrials), trial);
        var handler = new CorrectSampleTrialFeedbackCommandHandler(db, user, new CurrentUserPermissionService(user), Visibility.Create(user), null!);
        var command = new CorrectSampleTrialFeedbackCommand { SampleRequestId = request.SampleRequestId, TrialId = Guid.NewGuid(), CustomerReplyDate = new(2026, 11, 2) };
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        command.TrialId = trial.SampleRequestSampleTrialId;
        request.ManagerBy = Guid.NewGuid();
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        request.ManagerBy = user.EmployeeId.Value;
        request.CompanyId = Guid.NewGuid();
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    public class Visibility : DispatchProxy
    {
        private User _user = null!;
        public static ICustomerVisibilityService Create(User user)
        {
            var service = Create<ICustomerVisibilityService, Visibility>();
            ((Visibility)service)._user = user;
            return service;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildScopeAsync" or "BuildReadScopeAsync" => Task.FromResult(new ViewerScope(_user.CompanyId!.Value,
                _user.EmployeeId!.Value, true, false, new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now)),
            "ApplySampleRequestVisibility" or "ApplyCustomerVisibility" => args![0],
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public sealed class User(string role, bool authenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "correction-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) => role == candidate;
    }
}
