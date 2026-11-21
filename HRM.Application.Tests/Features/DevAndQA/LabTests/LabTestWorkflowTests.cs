using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA.LabTests;

public sealed class LabTestWorkflowTests
{
    [Fact]
    public async Task CreateCopiesSnapshotAndSeparatesSourceNameFromAudit()
    {
        var (service, store, user) = Fixture();
        var standard = Standard(user);
        store.Seed("EvaluationStandards", standard);
        var result = await service.CreateAsync(Input() with { PreparedByName = "Original tester", Results = [new(standard.Id, "0.90")] }, default);
        Assert.Equal("NVL_TEST_00000001", result.Code);
        Assert.Equal(user.EmployeeId, result.CreatedBy);
        Assert.Equal("Original tester", result.PreparedByName);
        Assert.Equal("0.9", Assert.Single(result.Results).Value);
        standard.Name = "Changed later";
        Assert.Equal("Density", Assert.Single(result.Results).CriterionName);
        Assert.True(store.Transaction.Committed);
    }

    [Fact]
    public async Task PatchKeepsOmittedFieldsAndClearsOnlyExplicitNullableFields()
    {
        var (service, store, user) = Fixture();
        var standard = Standard(user);
        var entity = new LabTest { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "NVL_TEST_00000001",
            SampleName = "Sample", TestDate = new(2026, 1, 2), Note = "Keep", BatchNumber = "Batch",
            Results = [new() { Id = Guid.NewGuid(), EvaluationStandardId = standard.Id, CriterionCode = standard.Code,
                CriterionName = standard.Name, DataType = EvaluationDataType.Decimal, Value = "0.9" }] };
        store.Seed("LabTests", entity);
        var changed = await service.PatchAsync(entity.Id, new() { ClearFields = ["batchNumber"], Results = [new(standard.Id, ClearValue: true)] }, default);
        Assert.Null(changed.BatchNumber);
        Assert.Equal("Keep", changed.Note);
        Assert.Null(Assert.Single(changed.Results).Value);

        Assert.Equal(user.EmployeeId, changed.UpdatedBy);
    }

    [Fact]
    public async Task ImportIsAtomicAndRetryDoesNotResurrectDeletedRow()
    {
        var (service, store, user) = Fixture();
        var batch = Guid.NewGuid();
        var old = new LabTest { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "NVL_TEST_00000001", ImportBatchId = batch, ImportRowNumber = 5, IsDeleted = true };
        store.Seed("LabTests", old);
        var imported = await service.ImportAsync(batch, [new(5, Input()), new(6, Input())], default);
        Assert.Equal(1, imported.Skipped);
        Assert.Equal(1, imported.Created);
        Assert.True(old.IsDeleted);
        Assert.Equal("NVL_TEST_00000002", imported.Items.Single(x => !x.AlreadyImported).Code);
        Assert.Equal(1, store.Saves);

        var (invalidService, invalidStore, _) = Fixture();
        await Assert.ThrowsAsync<EvaluationRequestException>(() => invalidService.ImportAsync(Guid.NewGuid(),
            [new(5, Input()), new(6, Input() with { SampleName = "" })], default));
        Assert.Empty(invalidStore.Added);
        Assert.Equal(0, invalidStore.Saves);
        Assert.True(invalidStore.Transaction.RolledBack);
    }

    [Fact]
    public async Task DeleteIsScopedAndIdempotent()
    {
        var (service, store, user) = Fixture();
        var entity = new LabTest { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value };
        store.Seed("LabTests", entity);
        await service.DeleteAsync(entity.Id, default);
        var audit = entity.UpdatedDate;
        await service.DeleteAsync(entity.Id, default);
        Assert.True(entity.IsDeleted);
        Assert.Equal(audit, entity.UpdatedDate);
        Assert.Equal(1, store.Saves);
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.DetailAsync(entity.Id, default))).StatusCode);
        entity.CompanyId = Guid.NewGuid();
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.DeleteAsync(entity.Id, default))).StatusCode);
    }

    [Fact]
    public async Task ForeignStandardCannotBeAdded()
    {
        var (service, store, user) = Fixture();
        var foreign = Standard(user); foreign.CompanyId = Guid.NewGuid();
        store.Seed("EvaluationStandards", foreign);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.CreateAsync(Input() with { Results = [new(foreign.Id, "1")] }, default));
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("KHOUser", true)]
    [InlineData("LabUser", false)]
    public async Task UnauthorizedMutationsFailBeforeWrite(string role, bool authenticated)
    {
        var (service, store, _) = Fixture(new() { Roles = [role], IsAuthenticated = authenticated });
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.CreateAsync(Input(), default))).StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ViewOnlyCanReadButCannotMutate()
    {
        var user = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.DevAndQA.ViewIqc] };
        var (service, store, _) = Fixture(user);
        var entity = new LabTest { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value };
        store.Seed("LabTests", entity);
        Assert.Equal(entity.Id, (await service.DetailAsync(entity.Id, default)).Id);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.DeleteAsync(entity.Id, default))).StatusCode);
    }

    private static LabTestWriteRequest Input() => new() { SampleName = "PP sample", TestDate = new(2026, 1, 2), ItemCode = "Excel code" };
    private static EvaluationStandard Standard(IqcWorkflowTests.User user) => new()
    { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "DENSITY", Name = "Density", DataType = EvaluationDataType.Decimal };
    private static (LabTestService Service, IqcTestStore Store, IqcWorkflowTests.User User) Fixture(IqcWorkflowTests.User? current = null)
    {
        var user = current ?? new() { Roles = ["LabUser"] };
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var access = new EvaluationAccessService(db, user, new CurrentUserPermissionService(user));
        return (new(db, access, new KeyedMutationLock<string>()), store, user);
    }
}
