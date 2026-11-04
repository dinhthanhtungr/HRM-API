using ClosedXML.Excel;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Commands.SynchronizeGroupStorage;
using HRM.Application.Features.InternalMail.GroupStorage;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.Documents.Excels;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("Developer", false, true)]
    [InlineData("Developer", true, true)]
    [InlineData("President", true, false)]
    [InlineData("President", false, false)]
    [InlineData("Admin", true, false)]
    [InlineData("SaleUser", true, false)]
    [InlineData("LabUser", false, false)]
    [InlineData("PLPUUser", false, false)]
    public async Task GroupSynchronizationPermissionIsTechnicalCompanyScopedAndCheckedBeforeQueries(string role, bool explicitToken, bool expected)
    {
        var user = new User(role) { HasExplicitPermissionSet = explicitToken,
            Permissions = [ApplicationPermissions.InternalMail.SynchronizeGroupStorage] };
        var service = new SyncRecorder();
        var handler = new SynchronizeGroupStorageCommandHandler(user, new CurrentUserPermissionService(user), service);
        var result = await handler.Handle(new(), default);
        Assert.Equal(expected, result is not null);
        Assert.Equal(expected ? 1 : 0, service.Calls);
        Assert.Equal(expected ? user.CompanyId : null, service.Company);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("no-company")]
    [InlineData("empty-company")]
    [InlineData("no-employee")]
    public async Task GroupSynchronizationRejectsMissingContextBeforeDatabaseOrExport(string failure)
    {
        var user = new User("Developer") { IsAuthenticated = failure != "anonymous",
            CompanyId = failure == "no-company" ? null : failure == "empty-company" ? Guid.Empty : Guid.NewGuid(),
            EmployeeId = failure == "no-employee" ? null : Guid.NewGuid() };
        var service = new SyncRecorder();
        var result = await new SynchronizeGroupStorageCommandHandler(user, new CurrentUserPermissionService(user), service).Handle(new(), default);
        Assert.Null(result);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public void SynchronizationExcelHasThreeSheetsTypedCountsAndNoExecutableFormulaFromUserText()
    {
        var report = new GroupStorageSyncReport { CompanyId = Guid.NewGuid(), Outcome = "Completed" };
        report.Rows.Add(new(Guid.NewGuid(), Guid.NewGuid(), "=1+1", "COLOR_TEST", InternalConversationGroupType.Custom,
            "=HYPERLINK(\"bad\")", true, "Mới đồng bộ", 3, 2));
        report.PresidentMemberships.Add(new(report.Rows[0].ConversationId, report.Rows[0].RootConversationId, Guid.NewGuid(),
            PresidentMembershipSyncStatus.Skipped, "CustomRequiresExplicitInvitation"));
        var bytes = new GroupStorageSyncExcelRenderer().Render(report);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(3, workbook.Worksheets.Count);
        var detail = workbook.Worksheet("Chi tiết nhóm");
        Assert.False(detail.Cell(7, 1).HasFormula);
        Assert.False(detail.Cell(7, 3).HasFormula);
        Assert.Equal("=1+1", detail.Cell(7, 1).GetString());
        Assert.Equal(3, detail.Cell(7, 6).GetValue<int>());
        Assert.Equal(2, detail.Cell(7, 7).GetValue<int>());
        Assert.Equal(5, detail.Cell(7, 8).GetValue<int>());
        Assert.False(workbook.Worksheet("Bù President").Cell(7, 1).HasFormula);
        Assert.Equal("=1+1", workbook.Worksheet("Bù President").Cell(7, 1).GetString());
    }

    [Fact]
    public void GroupSynchronizationRequestHasNoCallerControlledCompany()
    {
        Assert.DoesNotContain(typeof(SynchronizeGroupStorageCommand).GetProperties(), p => p.Name is "CompanyId" or "EmployeeId");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GroupSynchronizationPassesOnlyTheRequestedModeAndPrincipalCompany(bool includePresidents)
    {
        var user = new User("Developer");
        var service = new SyncRecorder();
        await new SynchronizeGroupStorageCommandHandler(user, new CurrentUserPermissionService(user), service)
            .Handle(new() { IncludePresidentMemberships = includePresidents }, default);
        Assert.Equal(user.CompanyId, service.Company);
        Assert.Equal(includePresidents, service.Members);
    }

    private sealed class SyncRecorder : IGroupStorageSynchronization
    {
        public int Calls { get; private set; }
        public Guid? Company { get; private set; }
        public bool? Members { get; private set; }
        public Task<GroupStorageSyncFile> SynchronizeAsync(Guid companyId, CancellationToken ct, bool includePresidentMemberships = true)
        {
            Calls++; Company = companyId; Members = includePresidentMemberships;
            return Task.FromResult(new GroupStorageSyncFile([1, 2], "test.xlsx", "Completed", Guid.NewGuid()));
        }
    }
}
