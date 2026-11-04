using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class IqcResultCatalogSyncTests
{
    [Theory]
    [InlineData("Current")]
    [InlineData("Selected")]
    [InlineData("Company")]
    public async Task SyncReplacesSnapshotOnlyWithinScope(string scope)
    {
        var f = Fixture();
        var request = new IqcResultCatalogSyncRequest(scope, scope == "Company" ? null : [f.Qc.QCInputByQCId], ["a"]);
        var preview = await f.Service.PreviewAsync(request, default);
        Assert.True(preview.CanApply);
        Assert.Equal(scope == "Company" ? 2 : 1, preview.ChangedResultCount);
        Assert.Equal("Old", f.Row.CriterionName);
        Assert.Equal(0, f.Store.Saves);
        var result = await f.Service.ApplyAsync(request, preview.PreviewToken, default);
        Assert.True(result.Applied);
        Assert.Equal("New", f.Row.CriterionName);
        Assert.Equal("English", f.Row.CriterionNameEn);
        Assert.Null(f.Row.Unit); // Catalog null thay override.
        Assert.Null(f.Row.InputConfigJson);
        Assert.Equal("2.00", f.Row.Value); // Không chuẩn hóa lại giá trị đo.
        Assert.Equal(7, f.Row.SortOrder);
        Assert.Equal("Old", f.ForeignRow.CriterionName);
        Assert.Equal(scope == "Company" ? "New" : "Old", f.SecondRow.CriterionName);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task InvalidRowBlocksWholeCompanyBeforeAnyMutation()
    {
        var f = Fixture();
        f.SecondRow.Value = "not-a-number";
        var request = new IqcResultCatalogSyncRequest("Company", Codes: ["A"]);
        var preview = await f.Service.PreviewAsync(request, default);
        Assert.False(preview.CanApply);
        Assert.Single(preview.Issues);
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.ApplyAsync(request, preview.PreviewToken, default));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("Old", f.Row.CriterionName);
        Assert.Equal(0, f.Store.Saves);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StalePreviewRejectsCatalogOrResultChange(bool catalog)
    {
        var f = Fixture();
        var request = new IqcResultCatalogSyncRequest("Current", [f.Qc.QCInputByQCId], ["A"]);
        var preview = await f.Service.PreviewAsync(request, default);
        if (catalog) f.Standard.Name = "Changed again"; else f.Row.Value = "3";
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.ApplyAsync(request, preview.PreviewToken, default));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(0, f.Store.Saves);
        Assert.Equal("Old", f.Row.CriterionName);
    }

    [Fact]
    public async Task AttachmentSyncKeepsCollectionAndBlocksIncompatibleTypeOrLimits()
    {
        var f = Fixture();
        f.Standard.DataType = EvaluationDataType.Attachment;
        f.Row.DataType = EvaluationDataType.Attachment;
        f.Row.Value = null;
        f.Row.AttachmentCollectionId = Guid.NewGuid();
        var file = new AttachmentModel { AttachmentId = Guid.NewGuid(), AttachmentCollectionId = f.Row.AttachmentCollectionId.Value,
            Slot = AttachmentSlot.EvaluationAttachment, FileName = "photo.png", SizeBytes = 100, IsActive = true };
        f.Store.Seed("AttachmentModels", file);
        var request = new IqcResultCatalogSyncRequest("Current", [f.Qc.QCInputByQCId], AllCriteria: true);
        var preview = await f.Service.PreviewAsync(request, default);
        Assert.True(preview.CanApply);
        await f.Service.ApplyAsync(request, preview.PreviewToken, default);
        Assert.Equal(file.AttachmentCollectionId, f.Row.AttachmentCollectionId);
        Assert.True(file.IsActive);
        f.Standard.DataType = EvaluationDataType.Text;
        Assert.False((await f.Service.PreviewAsync(request, default)).CanApply);
        f.Standard.DataType = EvaluationDataType.Attachment;
        f.Standard.InputConfigJson = "{\"maxFileSizeBytes\":50}";
        Assert.False((await f.Service.PreviewAsync(request, default)).CanApply);
    }

    [Fact]
    public async Task InactiveMissingAndForeignSourcesCannotBeSynchronized()
    {
        var f = Fixture();
        var request = new IqcResultCatalogSyncRequest("Current", [f.Qc.QCInputByQCId], ["A"]);
        f.Standard.IsActive = false;
        Assert.False((await f.Service.PreviewAsync(request, default)).CanApply);
        f.Standard.CompanyId = Guid.NewGuid();
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.PreviewAsync(request, default))).StatusCode);
        request = request with { QcInputByQCIds = [f.ForeignRow.IqcInspectionEvaluation.QCInputByQCId] };
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.PreviewAsync(request, default))).StatusCode);
    }

    [Theory]
    [InlineData("QCUser", true)]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    [InlineData("KHOUser", false)]
    public async Task PermissionsAreEnforced(string role, bool allowed)
    {
        var f = Fixture(role);
        var request = new IqcResultCatalogSyncRequest("Company", AllCriteria: true);
        if (allowed) Assert.True((await f.Service.PreviewAsync(request, default)).CanApply);
        else Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.PreviewAsync(request, default))).StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedAndInvalidSelectionsFailClosed()
    {
        var f = Fixture(authenticated: false);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.PreviewAsync(new("Company", AllCriteria: true), default))).StatusCode);
        f = Fixture();
        foreach (var request in new IqcResultCatalogSyncRequest[] {
            new("Current", Codes: ["A"]), new("Selected", [], ["A"]), new("Company", [f.Qc.QCInputByQCId], ["A"]),
            new("Company", Codes: ["A", "a"]), new("Company", Codes: ["A"], AllCriteria: true), new("Company") })
            await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.PreviewAsync(request, default));
    }

    [Fact]
    public async Task CatalogCodeCollisionBlocksSyncAndDoesNotAddOrDeleteRows()
    {
        var f = Fixture();
        var extra = new IqcInspectionResult { Id = Guid.NewGuid(), EvaluationStandardId = Guid.NewGuid(), HasConfigurationSnapshot = true,
            ExternalId = "A", CriterionCode = "A", CriterionName = "Other", DataType = EvaluationDataType.Decimal };
        f.Row.ExternalId = "OLD_A";
        f.Row.CriterionCode = "OLD_A";
        f.Row.IqcInspectionEvaluation.Results.Add(extra);
        var preview = await f.Service.PreviewAsync(new("Current", [f.Qc.QCInputByQCId], ["A"]), default);
        Assert.False(preview.CanApply);
        Assert.Empty(f.Store.Added);
        Assert.Empty(f.Store.Removed);
    }

    private sealed record TestFixture(IqcResultCatalogSyncService Service, IqcTestStore Store, EvaluationStandard Standard,
        QCInputByQC Qc, IqcInspectionResult Row, IqcInspectionResult SecondRow, IqcInspectionResult ForeignRow);

    private static TestFixture Fixture(string role = "QCUser", bool authenticated = true)
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role], IsAuthenticated = authenticated };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "A",
            Name = "New", NameEn = "English", DataType = EvaluationDataType.Decimal, IsActive = true };
        QCInputByQC Qc(Guid company) => new() { QCInputByQCId = Guid.NewGuid(),
            WarehouseVoucherDetail = new() { Voucher = new() { CompanyId = company } } };
        var qc = Qc(user.CompanyId.Value);
        var second = Qc(user.CompanyId.Value);
        var foreign = Qc(Guid.NewGuid());
        IqcInspectionEvaluation Evaluation(QCInputByQC parent)
        {
            var evaluation = new IqcInspectionEvaluation { Id = Guid.NewGuid(), QCInputByQCId = parent.QCInputByQCId, QCInputByQC = parent };
            evaluation.Results.Add(new IqcInspectionResult { Id = Guid.NewGuid(), IqcInspectionEvaluationId = evaluation.Id,
                IqcInspectionEvaluation = evaluation, EvaluationStandardId = standard.Id, HasConfigurationSnapshot = true,
                CriterionCode = "A", ExternalId = "A", CriterionName = "Old", Unit = "old-unit", DataType = EvaluationDataType.Decimal,
                Value = "2.00", SortOrder = 7, InputConfigJson = "{\"decimalPlaces\":2}" });
            return evaluation;
        }
        var eval = Evaluation(qc); var eval2 = Evaluation(second); var eval3 = Evaluation(foreign);
        store.Seed("QCInputByQCs", qc, second, foreign);
        store.Seed("IqcInspectionEvaluations", eval, eval2, eval3);
        store.Seed("EvaluationStandards", standard);
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var access = new EvaluationAccessService(db, user, new CurrentUserPermissionService(user));
        return new(new(db, access, new()), store, standard, qc, eval.Results.Single(), eval2.Results.Single(), eval3.Results.Single());
    }
}
