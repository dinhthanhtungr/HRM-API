using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services.Basic;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class BasicLabMaterialTests
{
    [Fact]
    public async Task MaterialIsOptionalAndServerOwnsTheChosenMaterialSnapshot()
    {
        var (service, store, material, _) = Fixture();
        var free = await service.SaveAsync(null, new("Free sample", new(2026, 11, 17), Results: []), default);
        Assert.Null(free.MaterialId);
        Assert.Null(free.MaterialCode);
        var saved = await service.SaveAsync(null,
            new("Client label", new(2026, 11, 17), Results: [], MaterialId: material.MaterialId), default);
        Assert.Equal(material.MaterialId, saved.MaterialId);
        Assert.Equal("NVL_001", saved.MaterialCode);
        Assert.Equal("Original material", saved.SampleName);
        Assert.StartsWith("NVL_TEST_", saved.Code);
        Assert.Empty(store.Added.OfType<Material>());
        Assert.Empty(store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseVoucher>());
    }

    [Fact]
    public async Task ChangingSourceOrClearingItKeepsTestResultsAndDoesNotRefreshSnapshotsImplicitly()
    {
        var (service, store, material, other) = Fixture();
        var standard = new EvaluationStandard
        {
            Id = Guid.NewGuid(), CompanyId = material.CompanyId, Code = "DENSITY", Name = "Density",
            IsActive = true, DataType = EvaluationDataType.Decimal
        };
        store.Seed("EvaluationStandards", standard);
        var saved = await service.SaveAsync(null,
            new(TestDate: new(2026, 11, 17), MaterialId: material.MaterialId, Results: [new(standard.Id, "1.2")]), default);
        var result = Assert.Single(saved.Results);
        material.ExternalId = "RENAMED";
        material.Name = "Renamed material";
        material.IsActive = false;
        var unchanged = await service.SaveAsync(saved.Id, new(MaterialId: material.MaterialId, Note: "Note"), default);
        Assert.Equal(saved.SampleName, unchanged.SampleName);
        Assert.Equal(saved.MaterialCode, unchanged.MaterialCode);
        Assert.Equal(saved.SampleName, (await service.DetailAsync(saved.Id, default)).SampleName);
        var switched = await service.SaveAsync(saved.Id, new(MaterialId: other.MaterialId), default);
        Assert.Equal(other.Name, switched.SampleName);
        Assert.Equal(other.ExternalId, switched.MaterialCode);
        Assert.Equal(result.Id, Assert.Single(switched.Results).Id);
        Assert.Equal("1.2", Assert.Single(switched.Results).Value);
        var free = await service.SaveAsync(saved.Id,
            new(SampleName: "Free again", ClearFields: ["materialId"]), default);
        Assert.Null(free.MaterialId);
        Assert.Null(free.MaterialCode);
        Assert.Equal("Free again", free.SampleName);
        Assert.Equal(result.Id, Assert.Single(free.Results).Id);
    }

    [Fact]
    public async Task InvalidForeignInactiveOrConflictingMaterialIsRejectedWithoutSaving()
    {
        var (service, store, material, other) = Fixture();
        material.IsActive = false;
        other.CompanyId = Guid.NewGuid();
        foreach (var id in new[] { material.MaterialId, other.MaterialId, Guid.NewGuid(), Guid.Empty })
            await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(null,
                new(TestDate: new(2026, 11, 17), MaterialId: id, Results: []), default));
        Assert.Equal(0, store.Saves);
        material.IsActive = true;
        var saved = await service.SaveAsync(null,
            new(TestDate: new(2026, 11, 17), MaterialId: material.MaterialId, Results: []), default);
        var saves = store.Saves;
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id,
            new(MaterialId: material.MaterialId, ClearFields: ["materialId"]), default));
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(saved.Id,
            new(SampleName: "Change protected snapshot"), default));
        Assert.Equal(saves, store.Saves);
        Assert.Equal(saved.SampleName, (await service.DetailAsync(saved.Id, default)).SampleName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaterialLookupScopesCompanyActiveStateAndSelectedId(bool active)
    {
        var (service, _, material, other) = Fixture();
        material.IsActive = active;
        other.CompanyId = Guid.NewGuid();
        var page = await service.LookupAsync(new(LabTestLookupKind.Material), default);
        Assert.Equal(active ? 1 : 0, page.TotalCount);
        if (active)
        {
            var option = Assert.Single(page.Items);
            Assert.Equal(material.MaterialId, option.Id);
            Assert.Equal(material.ExternalId, option.Code);
            Assert.Equal(material.Name, option.Name);
        }
        Assert.Empty((await service.LookupAsync(new(LabTestLookupKind.Material, SelectedId: other.MaterialId), default)).Items);
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("QCUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    public async Task MaterialLookupAndSaveKeepIqcPermissions(string role, bool allowed)
    {
        var (service, _, material, _) = Fixture(role);
        if (allowed)
        {
            Assert.Equal(2, (await service.LookupAsync(new(LabTestLookupKind.Material), default)).TotalCount);
            await service.SaveAsync(null, new(TestDate: new(2026, 11, 17), Results: [], MaterialId: material.MaterialId), default);
        }
        else
        {
            Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
                service.LookupAsync(new(LabTestLookupKind.Material), default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
                service.SaveAsync(null, new(TestDate: new(2026, 11, 17), Results: [], MaterialId: material.MaterialId), default))).StatusCode);
        }
    }

    private static (BasicLabTestService Service, IqcTestStore Store, Material Material, Material Other) Fixture(string role = "LabUser")
    {
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role] };
        store.ReflectAddedOnSave = true;
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ExternalId = "NVL_001", Name = "Original material", IsActive = true };
        var other = new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
            ExternalId = "NVL_002", Name = "Second material", IsActive = true };
        store.Seed("Materials", material, other);
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        return (new(db, new(db, user, new CurrentUserPermissionService(user)), new()), store, material, other);
    }
}
