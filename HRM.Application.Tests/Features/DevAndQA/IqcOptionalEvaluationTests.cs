using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Commands.AddIqcEvaluationResult;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public interface ICombinedIqcTestContext : IIqcDbContext, IEvaluationDbContext;

public sealed class IqcOptionalEvaluationTests
{
    private static (IqcWriter Writer, IqcEvaluationService Service, IqcTestStore Store,
        IqcWorkflowTests.User User, WarehouseVoucherDetail Detail, EvaluationStandard Standard) Fixture()
    {
        var (db, store) = IqcTestStore.CreateContext<ICombinedIqcTestContext>();
        var user = new IqcWorkflowTests.User();
        var permissions = new CurrentUserPermissionService(user);
        var mutationLock = new KeyedMutationLock<string>();
        var service = new EvaluationServicesFixture(db, user, permissions, mutationLock).Iqc;
        var detail = new WarehouseVoucherDetail { VoucherDetailId = 17,
            Voucher = new WarehouseVoucher { CompanyId = user.CompanyId!.Value } };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
            Code = "MOISTURE", Name = "Độ ẩm", DataType = EvaluationDataType.Decimal, IsActive = true };
        store.Seed("WarehouseVoucherDetails", detail);
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        store.Seed("EvaluationStandards", standard);
        return (new IqcWriter(db, user, mutationLock, permissions, service), service, store, user, detail, standard);
    }

    private static EvaluationTemplate SeedTemplate(IqcTestStore store, Guid company, EvaluationStandard standard)
    {
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = company, Type = EvaluationTemplateType.Iqc,
            Status = EvaluationTemplateStatus.Draft, IsDefault = true, Code = "IQC", Name = "Mẫu chung", Version = 1 };
        template.Items.Add(EvaluationSnapshotFactory.CreateTemplateItem(standard, template));
        template.Status = EvaluationTemplateStatus.Published;
        store.Seed("EvaluationTemplates", template);
        return template;
    }

    [Fact]
    public async Task OpenWithoutQcReturnsDefaultPreviewAndDoesNotWrite()
    {
        var f = Fixture();
        var template = SeedTemplate(f.Store, f.User.CompanyId!.Value, f.Standard);
        var result = await f.Service.GetIqcEvaluationAsync(f.Detail.VoucherDetailId, default);
        Assert.False(result.IsStored);
        Assert.Null(result.Id);
        Assert.Null(result.QCInputByQCId);
        Assert.Equal(template.Id, result.EvaluationTemplateId);
        Assert.Equal(Guid.Empty, Assert.Single(result.Results).Id);
        Assert.Null(result.Results[0].Value);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task OpenWithoutDefaultReturnsEmptyPreviewAndKeepsQcUsable()
    {
        var f = Fixture();
        var result = await f.Service.GetIqcEvaluationAsync(f.Detail.VoucherDetailId, default);
        Assert.False(result.IsStored);
        Assert.Empty(result.Results);
        Assert.Null(result.EvaluationTemplateId);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task OpenForeignVoucherFails404InsteadOfShowingPreview()
    {
        var f = Fixture();
        f.Detail.Voucher.CompanyId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            f.Service.GetIqcEvaluationAsync(f.Detail.VoucherDetailId, default));
        Assert.Equal(404, error.StatusCode);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task SavedSnapshotWinsOverCurrentTemplate()
    {
        var f = Fixture();
        SeedTemplate(f.Store, f.User.CompanyId!.Value, f.Standard);
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = f.Detail.VoucherDetailId,
            WarehouseVoucherDetail = f.Detail };
        var evaluation = new IqcInspectionEvaluation { Id = Guid.NewGuid(), QCInputByQCId = qc.QCInputByQCId };
        var row = EvaluationSnapshotFactory.CreateAdditionalIqc(f.Standard, evaluation.Id, f.User.CompanyId.Value);
        row.Value = "0.15";
        row.CriterionName = "Tên đã lưu";
        evaluation.Results.Add(row);
        f.Store.Seed("QCInputByQCs", qc);
        f.Store.Seed("IqcInspectionEvaluations", evaluation);
        var result = await f.Service.GetIqcEvaluationAsync(f.Detail.VoucherDetailId, default);
        Assert.True(result.IsStored);
        Assert.Equal(evaluation.Id, result.Id);
        Assert.Equal("Tên đã lưu", Assert.Single(result.Results).CriterionName);
        Assert.Equal("0.15", result.Results[0].Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveQcWithoutSupplementNeverCreatesEvaluation(bool empty)
    {
        var f = Fixture();
        SeedTemplate(f.Store, f.User.CompanyId!.Value, f.Standard);
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = empty ? new(Results: []) : null }, default);
        Assert.True(result.Success);
        Assert.Single(f.Store.Added.OfType<QCInputByQC>());
        Assert.Empty(f.Store.Added.OfType<IqcInspectionEvaluation>());
        Assert.True(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveQcAndSupplementCommitsTogether(bool fromTemplate)
    {
        var f = Fixture();
        var template = fromTemplate ? SeedTemplate(f.Store, f.User.CompanyId!.Value, f.Standard) : null;
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = new(template?.Id, [new(f.Standard.Id, SourceTemplateItemId: template?.Items.Single().Id, Value: "0.15")]) }, default);
        Assert.True(result.Success);
        var qc = Assert.Single(f.Store.Added.OfType<QCInputByQC>());
        var evaluation = Assert.Single(f.Store.Added.OfType<IqcInspectionEvaluation>());
        Assert.Equal(qc.QCInputByQCId, evaluation.QCInputByQCId);
        Assert.Equal("0.15", Assert.Single(evaluation.Results).Value);
        Assert.True(evaluation.Results.Single().HasConfigurationSnapshot);
        Assert.Equal(template?.Id, evaluation.EvaluationTemplateId);
        Assert.Equal(1, f.Store.Saves);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task InvalidSupplementDoesNotSaveQcOrEvaluation()
    {
        var f = Fixture();
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = new(Results: [new(f.Standard.Id, Value: "invalid")]) }, default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task PatchInvalidSupplementPreservesQcFieldsAndSnapshot()
    {
        var f = Fixture();
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = f.Detail.VoucherDetailId,
            WarehouseVoucherDetail = f.Detail, Note = "Keep" };
        f.Store.Seed("QCInputByQCs", qc);
        var result = await f.Writer.PatchAsync(f.Detail.VoucherDetailId, new PatchQCInputByQC { Note = "Changed",
            Evaluation = new(Results: [new(f.Standard.Id, Value: "invalid")]) }, default);
        Assert.False(result.Success);
        Assert.Equal("Keep", qc.Note);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task SavingUnfilledPreviewDoesNotCreateEvaluation()
    {
        var f = Fixture();
        var template = SeedTemplate(f.Store, f.User.CompanyId!.Value, f.Standard);
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = new(template.Id, [new(f.Standard.Id, SourceTemplateItemId: template.Items.Single().Id)]) }, default);
        Assert.True(result.Success);
        Assert.Empty(f.Store.Added.OfType<IqcInspectionEvaluation>());
        Assert.Single(f.Store.Added.OfType<QCInputByQC>());
    }

    [Fact]
    public async Task PatchExistingQcCanCreateSupplementInSameTransaction()
    {
        var f = Fixture();
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = f.Detail.VoucherDetailId,
            WarehouseVoucherDetail = f.Detail };
        f.Store.Seed("QCInputByQCs", qc);
        var result = await f.Writer.PatchAsync(f.Detail.VoucherDetailId, new PatchQCInputByQC { Note = "Updated",
            Evaluation = new(Results: [new(f.Standard.Id, Value: "0")]) }, default);
        Assert.True(result.Success);
        Assert.Equal("Updated", qc.Note);
        var evaluation = Assert.Single(f.Store.Added.OfType<IqcInspectionEvaluation>());
        Assert.Equal(qc.QCInputByQCId, evaluation.QCInputByQCId);
        Assert.Equal("0", Assert.Single(evaluation.Results).Value);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task PatchStoredResultPreservesNullAndSupportsExplicitClear()
    {
        var f = Fixture();
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = f.Detail.VoucherDetailId,
            WarehouseVoucherDetail = f.Detail };
        var evaluation = new IqcInspectionEvaluation { Id = Guid.NewGuid(), QCInputByQCId = qc.QCInputByQCId };
        var row = EvaluationSnapshotFactory.CreateAdditionalIqc(f.Standard, evaluation.Id, f.User.CompanyId!.Value);
        row.Value = "0.15";
        evaluation.Results.Add(row);
        f.Store.Seed("QCInputByQCs", qc);
        f.Store.Seed("IqcInspectionEvaluations", evaluation);
        await f.Service.StageIqcSaveAsync(qc, new(Results: [new(f.Standard.Id, ResultId: row.Id)]), f.User.CompanyId.Value, default);
        Assert.Equal("0.15", row.Value);
        await f.Service.StageIqcSaveAsync(qc, new(Results: [new(f.Standard.Id, ResultId: row.Id, ClearValue: true)]), f.User.CompanyId.Value, default);
        Assert.Null(row.Value);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task ForeignResultIdCannotBeSavedUnderCurrentQc()
    {
        var f = Fixture();
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = new(Results: [new(f.Standard.Id, ResultId: Guid.NewGuid(), Value: "0.15")]) }, default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task DatabaseSaveFailureDoesNotCommitCombinedQcAndSupplement()
    {
        var f = Fixture();
        f.Store.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Writer.CreateAsync(new PostQCInputByQC {
            VoucherDetailId = f.Detail.VoucherDetailId, Evaluation = new(Results: [new(f.Standard.Id, Value: "0.15")]) }, default));
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task AddSingleCriterionOnSavedQcDoesNotRequireInitializationOrTemplate()
    {
        var f = Fixture();
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = f.Detail.VoucherDetailId,
            WarehouseVoucherDetail = f.Detail };
        f.Store.Seed("QCInputByQCs", qc);
        var result = await f.Service.AddIqcResultAsync(new AddIqcEvaluationResultCommand(
            f.Detail.VoucherDetailId, f.Standard.Id), default);
        var evaluation = Assert.Single(f.Store.Added.OfType<IqcInspectionEvaluation>());
        Assert.Equal(qc.QCInputByQCId, evaluation.QCInputByQCId);
        Assert.Null(evaluation.EvaluationTemplateId);
        Assert.Null(result.Value);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task ForeignStandardRejectsCombinedSaveBeforeQcMutation()
    {
        var f = Fixture();
        f.Standard.CompanyId = Guid.NewGuid();
        var result = await f.Writer.CreateAsync(new PostQCInputByQC { VoucherDetailId = f.Detail.VoucherDetailId,
            Evaluation = new(Results: [new(f.Standard.Id, Value: "0.15")]) }, default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
    }
}
