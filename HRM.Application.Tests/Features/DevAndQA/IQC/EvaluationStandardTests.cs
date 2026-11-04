using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using Xunit;
namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class EvaluationStandardTests
{
    [Theory]
    [InlineData("SaleUser")]
    [InlineData("KHOUser")]
    public async Task ManageRequiredBeforeDatabase(string role)
    {
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var service = new EvaluationStandardService(null!, user, new CurrentUserPermissionService(user), new());
        Assert.False((await service.CreateAsync(new() { Code = "MOISTURE", Name = "Độ ẩm" }, default)).Success);
        Assert.False((await service.PatchAsync(new() { Id = Guid.NewGuid() }, default)).Success);
    }

    [Fact]
    public async Task UnauthenticatedReadFailsBeforeDatabase()
    {
        var user = new IqcWorkflowTests.User { IsAuthenticated = false };
        var service = new EvaluationStandardService(null!, user, new CurrentUserPermissionService(user), new());
        Assert.False((await service.ListAsync(new(), default)).Success);
    }

    [Fact]
    public async Task CreateNormalizesAndSetsServerAudit()
    {
        var (service, store, user) = Fixture();
        var result = await service.CreateAsync(new() { Code = " MOISTURE ", Name = " Độ ẩm ", SortOrder = 2 }, default);
        Assert.True(result.Success);
        var entity = Assert.IsType<EvaluationStandard>(Assert.Single(store.Added));
        Assert.Equal("MOISTURE", entity.Code);
        Assert.Equal("Độ ẩm", entity.Name);
        Assert.Equal(user.CompanyId, entity.CompanyId);
        Assert.Equal(user.EmployeeId, entity.CreatedBy);
        Assert.True(store.Transaction.Committed);
    }

    [Fact]
    public async Task MethodMustBeActiveAndBelongToCompany()
    {
        var (service, store, _) = Fixture();
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Code = "DRY", Name = "Sấy" };
        store.Seed("MeasurementMethods", method);
        var result = await service.CreateAsync(new() { Code = "M", Name = "Độ ẩm", MeasurementMethodId = method.Id }, default);
        Assert.False(result.Success);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task CatalogMethodIdIsReturnedAndCanBeExplicitlyCleared()
    {
        var (service, store, user) = Fixture();
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "DRY", Name = "Sấy" };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId.Value, Code = "M", Name = "Độ ẩm" };
        store.Seed("MeasurementMethods", method);
        store.Seed("EvaluationStandards", standard);
        var assigned = await service.PatchAsync(new() { Id = standard.Id, MeasurementMethodId = method.Id }, default);
        Assert.True(assigned.Success);
        Assert.Equal(method.Id, assigned.Data!.MeasurementMethodId);
        Assert.Equal("Sấy", assigned.Data.TestMethod);
        var cleared = await service.PatchAsync(new() { Id = standard.Id, ClearFields = ["measurementMethodId"] }, default);
        Assert.True(cleared.Success);
        Assert.Null(cleared.Data!.MeasurementMethodId);
        Assert.Equal("Sấy", cleared.Data.TestMethod);
    }

    [Fact]
    public async Task DuplicateCodeCaseInsensitiveIncludesInactive()
    {
        var (service, store, user) = Fixture();
        store.Seed("EvaluationStandards", new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "moisture", Name = "Old", IsActive = false });
        Assert.False((await service.CreateAsync(new() { Code = "MOISTURE", Name = "New" }, default)).Success);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task CrossCompanyDetailAndPatchDoNotExposeOrMutate()
    {
        var (service, store, _) = Fixture();
        var entity = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Code = "M", Name = "Private" };
        store.Seed("EvaluationStandards", entity);
        var detail = await service.DetailAsync(new(entity.Id), default);
        Assert.True(detail.Success); Assert.Null(detail.Data);
        Assert.False((await service.PatchAsync(new() { Id = entity.Id, Name = "Changed" }, default)).Success);
        Assert.Equal("Private", entity.Name);
    }

    [Fact]
    public async Task PatchFalseAndZeroApplyWhileOmittedFieldsRemain()
    {
        var (service, store, user) = Fixture();
        var entity = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "M", Name = "Moisture", Description = "Keep", IsActive = true, SortOrder = 4 };
        store.Seed("EvaluationStandards", entity);
        var result = await service.PatchAsync(new() { Id = entity.Id, IsActive = false, SortOrder = 0 }, default);
        Assert.True(result.Success); Assert.False(entity.IsActive); Assert.Equal(4, entity.SortOrder);
        Assert.Equal("Keep", entity.Description); Assert.Equal("Moisture", entity.Name);
        Assert.Equal(user.EmployeeId, entity.UpdatedBy);
    }

    [Fact]
    public async Task ClearConflictAndInvalidCodeFailWithoutWrites()
    {
        var (service, store, _) = Fixture();
        Assert.False((await service.PatchAsync(new() { Id = Guid.NewGuid(), Description = "x", ClearFields = ["description"] }, default)).Success);
        Assert.False((await service.CreateAsync(new() { Code = " ", Name = "Name" }, default)).Success);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task UnchangedPatchDoesNotSaveOrTouchAudit()
    {
        var (service, store, user) = Fixture();
        var date = new DateTime(2026, 10, 1);
        var entity = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "M", Name = "Moisture", Description = "Keep", UpdatedDate = date };
        store.Seed("EvaluationStandards", entity);
        var result = await service.PatchAsync(new() { Id = entity.Id, Code = " M ", Description = " Keep " }, default);
        Assert.True(result.Success);
        Assert.Equal(0, store.Saves);
        Assert.Equal(date, entity.UpdatedDate);
        Assert.Null(entity.UpdatedBy);
    }

    [Fact]
    public async Task ClearDescriptionOnlySavesWhenValueChanges()
    {
        var (service, store, user) = Fixture();
        var entity = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "M", Name = "Moisture", Description = "Clear me" };
        store.Seed("EvaluationStandards", entity);
        Assert.True((await service.PatchAsync(new() { Id = entity.Id, ClearFields = ["description"] }, default)).Success);
        Assert.Null(entity.Description);
        Assert.Equal(1, store.Saves);
        Assert.True((await service.PatchAsync(new() { Id = entity.Id, ClearFields = ["description"] }, default)).Success);
        Assert.Equal(1, store.Saves);
    }

    private static (EvaluationStandardService Service, IqcTestStore Store, IqcWorkflowTests.User User) Fixture()
    {
        var (db, store) = IqcTestStore.CreateStore(); var user = new IqcWorkflowTests.User();
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value });
        return (new(db, user, new CurrentUserPermissionService(user), new KeyedMutationLock<string>()), store, user);
    }
}
