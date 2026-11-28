using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class BasicLabTestTests
{
    [Fact]
    public async Task FreeSupplierNamePersistsWithoutCatalogAndCanBeCleared()
    {
        var f = Fixture();
        f.Store.ReflectAddedOnSave = true;
        var saved = await f.Service.SaveAsync(null,
            new("Sample", new(2026, 11, 14), SupplierName: "  NCC tự do  "), default);
        Assert.Null(saved.SupplierId);
        Assert.Equal("NCC tự do", saved.SupplierName);
        Assert.Equal("NCC tự do", (await f.Service.DetailAsync(saved.Id, default)).SupplierName);
        Assert.Equal("NCC tự do", Assert.Single((await f.Service.ListAsync(1, 15, default)).Items).SupplierName);
        Assert.Equal("NCC tự do", (await f.Service.SaveAsync(saved.Id, new(Note: "Note"), default)).SupplierName);
        Assert.Null((await f.Service.SaveAsync(saved.Id, new(ClearFields: ["supplierName"]), default)).SupplierName);
    }

    [Fact]
    public async Task ManualBatchAndWeightPersistPatchAndClearWithoutWarehouseLinks()
    {
        var f = Fixture();
        f.Store.ReflectAddedOnSave = true;
        var saved = await f.Service.SaveAsync(null, new("Manual sample", new(2026, 11, 15),
            Results: [], BatchNumber: " LOT-01 ", WeightKg: 12.345678m), default);
        Assert.Equal("LOT-01", saved.BatchNumber);
        Assert.Equal(12.345678m, saved.WeightKg);
        var unchanged = await f.Service.SaveAsync(saved.Id, new(Note: "Note"), default);
        Assert.Equal(saved.BatchNumber, unchanged.BatchNumber);
        Assert.Equal(saved.WeightKg, unchanged.WeightKg);
        var zero = await f.Service.SaveAsync(saved.Id, new(WeightKg: 0), default);
        Assert.Equal(0, zero.WeightKg);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.SaveAsync(saved.Id,
            new(BatchNumber: "LOT", ClearFields: ["batchNumber"]), default));
        var cleared = await f.Service.SaveAsync(saved.Id, new(ClearFields: ["batchNumber", "weightKg"]), default);
        Assert.Null(cleared.BatchNumber);
        Assert.Null(cleared.WeightKg);
        Assert.DoesNotContain(f.Store.Added, x => x is QCInputByQC or HRM.Domain.Entities.MaterialSchema.Material);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1000000000000")]
    [InlineData("1.1234567")]
    public async Task InvalidManualWeightDoesNotSave(string value)
    {
        var f = Fixture();
        var weight = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.SaveAsync(null,
            new("Invalid", new(2026, 11, 15), WeightKg: weight), default));
        Assert.Equal(0, f.Store.Saves);
    }
    [Fact]
    public async Task CreatesIndependentSampleWithDefaultIqcSnapshotAndSequentialCode()
    {
        var f = Fixture();
        f.Store.ReflectAddedOnSave = true;
        var first = await f.Service.SaveAsync(null, new("Sample A", new(2026, 11, 14), Results: [new(f.Standard.Id, "0.9")]), default);
        var second = await f.Service.SaveAsync(null, new("Sample B", new(2026, 11, 14)), default);
        Assert.Equal("NVL_TEST_00001", first.Code);
        Assert.Equal("NVL_TEST_00002", second.Code);
        Assert.Equal(f.Template.Id, first.EvaluationTemplateId);
        Assert.Equal("Template density", Assert.Single(first.Results).CriterionName);
        Assert.Equal("0.9", first.Results.Single().Value);
        Assert.Null(second.Results.Single().Value);
        Assert.DoesNotContain(f.Store.Added, x => x is HRM.Domain.Entities.MaterialSchema.Material or QCInputByQC);
        f.Standard.Name = "Catalog changed";
        f.Template.Items.Single().CriterionName = "Template changed";
        var edited = await f.Service.SaveAsync(first.Id, new(Results: [new(f.Standard.Id, "1.1")]), default);
        Assert.Equal("Template density", edited.Results.Single().CriterionName);
        Assert.Equal(first.Results.Single().Id, edited.Results.Single().Id);
    }

    [Fact]
    public async Task EmptySelectionDoesNotForceOrActivateCatalogCriteria()
    {
        var f = Fixture();
        var saved = await f.Service.SaveAsync(null, new("No measurements yet", new(2026, 11, 14), Results: []), default);
        Assert.Empty(saved.Results);
        Assert.True(f.Standard.IsActive);
        Assert.Empty(f.Store.Added.OfType<EvaluationStandard>());
    }

    [Fact]
    public async Task NoDefaultAllowsExplicitCatalogCriteriaAndRejectsInactiveNewSource()
    {
        var f = Fixture(); f.Template.IsDefault = false;
        var result = await f.Service.SaveAsync(null, new("Extra", new(2026, 11, 14), Results: [new(f.Standard.Id, "2")]), default);
        Assert.Null(result.EvaluationTemplateId);
        Assert.Equal(f.Standard.Name, result.Results.Single().CriterionName);
        f.Standard.IsActive = false;
        await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.SaveAsync(null,
            new("Inactive", new(2026, 11, 14), Results: [new(f.Standard.Id, "2")]), default));
    }

    [Fact]
    public async Task InvalidScalarOrForeignTemplateDoesNotSave()
    {
        var f = Fixture();
        await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.SaveAsync(null,
            new("Bad", new(2026, 11, 14), Results: [new(f.Standard.Id, "not a number")]), default));
        Assert.Equal(0, f.Store.Saves);
        f.Template.CompanyId = Guid.NewGuid();
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.SaveAsync(null,
            new("Foreign", new(2026, 11, 14), EvaluationTemplateId: f.Template.Id), default))).StatusCode);
    }

    [Fact]
    public async Task BasicPatchClearNoteAndCompanyScopeAreEnforced()
    {
        var f = Fixture(); f.Store.ReflectAddedOnSave = true;
        var saved = await f.Service.SaveAsync(null, new("Sample", new(2026, 11, 14), Note: "Note"), default);
        var cleared = await f.Service.SaveAsync(saved.Id, new(ClearFields: ["note"]), default);
        Assert.Null(cleared.Note);
        var page = await f.Service.ListAsync(1, 15, default);
        Assert.Equal(1, page.TotalCount);
        f.Store.Added.OfType<LabTest>().Single().CompanyId = Guid.NewGuid();
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => f.Service.DetailAsync(saved.Id, default))).StatusCode);
        Assert.Empty((await f.Service.ListAsync(1, 15, default)).Items);
    }

    [Theory]
    [InlineData("QCUser", true)]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    [InlineData("KHOUser", false)]
    public async Task ManageIqcPermissionIsRequired(string role, bool allowed)
    {
        var f = Fixture(role);
        var task = () => f.Service.SaveAsync(null, new("Sample", new(2026, 11, 14), Results: []), default);
        if (allowed) await task();
        else Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(task)).StatusCode);
    }

    [Fact]
    public async Task FileResolverScopesLabResultsAndRejectsSharedCollections()
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationFileSaveDbContext>();
        var user = new IqcWorkflowTests.User();
        var collection = Guid.NewGuid();
        var row = new LabTestResult { Id = Guid.NewGuid(), AttachmentCollectionId = collection, DataType = EvaluationDataType.Attachment };
        var lab = new LabTest { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Results = [row] };
        store.Seed("LabTests", lab); store.Seed("LabTestResults", row);
        var resolver = new EvaluationFileOwnerResolver(db, new(db, user, new CurrentUserPermissionService(user)));
        var owner = Assert.Single(await resolver.ResolveAsync(EvaluationFileResource.Lab, lab.Id, false, default));
        await resolver.RequireExclusiveCollectionAsync(owner, default);
        store.Seed("IqcInspectionResults", new IqcInspectionResult { AttachmentCollectionId = collection });
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => resolver.RequireExclusiveCollectionAsync(owner, default))).StatusCode);
        lab.CompanyId = Guid.NewGuid();
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => resolver.ResolveAsync(EvaluationFileResource.Lab, lab.Id, false, default))).StatusCode);
    }

    private sealed record TestFixture(BasicLabTestService Service, IqcTestStore Store, EvaluationStandard Standard, EvaluationTemplate Template);
    private static TestFixture Fixture(string role = "QCUser")
    {
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "DENSITY", Name = "Density", IsActive = true, DataType = EvaluationDataType.Decimal };
        var item = new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationStandardId = standard.Id, CriterionCode = standard.Code,
            CriterionName = "Template density", DataType = EvaluationDataType.Decimal, SortOrder = 1 };
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId.Value, Type = EvaluationTemplateType.Iqc,
            Status = EvaluationTemplateStatus.Published, IsDefault = true, Code = "IQC", Name = "Default IQC", Version = 1, Items = [item] };
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        store.Seed("EvaluationStandards", standard); store.Seed("EvaluationTemplates", template);
        return new(new(db, new(db, user, new CurrentUserPermissionService(user)), new()), store, standard, template);
    }
}
