using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class EvaluationStandardDefaultsTests
{
    [Theory]
    [InlineData(EvaluationDataType.Decimal, "{\"minValue\":\"0\",\"maxValue\":\"100\",\"decimalPlaces\":3}", "{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}")]
    [InlineData(EvaluationDataType.Boolean, null, "{\"operator\":\"Equal\",\"value\":true}")]
    [InlineData(EvaluationDataType.Date, "{\"minDate\":\"2026-01-01\"}", "{\"operator\":\"Between\",\"min\":\"2026-01-01\",\"max\":\"2026-12-31\"}")]
    public void ValidDefaults(EvaluationDataType type, string? input, string? rule) =>
        Assert.Null(EvaluationStandardDefaultsRules.Validate(type, "%", input, rule));

    [Theory]
    [InlineData(EvaluationDataType.Decimal, "{\"minValue\":10,\"maxValue\":1}", null)]
    [InlineData(EvaluationDataType.Integer, "{\"minValue\":1.2}", null)]
    [InlineData(EvaluationDataType.Text, "[]", null)]
    [InlineData(EvaluationDataType.SingleSelect, null, null)]
    [InlineData(EvaluationDataType.Boolean, null, "{\"operator\":\"LessThan\",\"value\":true}")]
    [InlineData(EvaluationDataType.Decimal, null, "{\"operator\":\"Equal\",\"value\":\"0,2\"}")]
    [InlineData((EvaluationDataType)99, null, null)]
    public void InvalidDefaults(EvaluationDataType type, string? input, string? rule) =>
        Assert.NotNull(EvaluationStandardDefaultsRules.Validate(type, null, input, rule));

    [Fact]
    public void ChoicesRequireUniqueCodesAndRuleValuesMustExist()
    {
        const string options = "{\"options\":[{\"code\":\"PASS\",\"label\":\"Đạt\"},{\"code\":\"FAIL\",\"label\":\"Không đạt\"}]}";
        Assert.Null(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.SingleSelect, null, options,
            "{\"operator\":\"In\",\"values\":[\"PASS\"]}"));
        Assert.NotNull(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.SingleSelect, null, options,
            "{\"operator\":\"Equal\",\"value\":\"UNKNOWN\"}"));
    }

    [Fact]
    public void SnapshotDoesNotFollowLaterCatalogChanges()
    {
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), Code = "MOISTURE", Name = "Độ ẩm",
            DataType = EvaluationDataType.Decimal, Unit = "%", AcceptanceRuleJson = "{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}" };
        var iqc = EvaluationStandardSnapshotFactory.CreateIqcItem(standard, Guid.NewGuid());
        var coa = EvaluationStandardSnapshotFactory.CreateCoaItem(standard, Guid.NewGuid());
        standard.Unit = "ppm"; standard.Name = "Changed"; standard.AcceptanceRuleJson = null;
        Assert.Equal("%", iqc.Unit); Assert.Equal("Độ ẩm", coa.CriterionName);
        Assert.NotNull(iqc.AcceptanceRuleJson); Assert.NotNull(coa.AcceptanceRuleJson);
    }

    [Fact]
    public async Task CreateAndPatchPersistDefaultsAndValidateMergedType()
    {
        var (db, store) = IqcTestStore.CreateStore();
        var user = new IqcWorkflowTests.User();
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value });
        var service = new EvaluationStandardService(db, user, new CurrentUserPermissionService(user), new KeyedMutationLock<string>());
        var created = await service.CreateAsync(new CreateEvaluationStandardCommand {
            Code = "MOISTURE", Name = "Độ ẩm", DataType = EvaluationDataType.Decimal, Unit = "%",
            AcceptanceRuleJson = "{\"operator\":\"LessThanOrEqual\",\"value\":\"0.2\"}", IsRequired = true }, default);
        Assert.True(created.Success);
        var entity = Assert.IsType<EvaluationStandard>(Assert.Single(store.Added));
        Assert.Equal(EvaluationDataType.Decimal, created.Data!.DataType);
        store.Seed(nameof(db.EvaluationStandards), entity);
        Assert.False((await service.PatchAsync(new() { Id = entity.Id, DataType = EvaluationDataType.Boolean }, default)).Success);
        Assert.Equal(EvaluationDataType.Decimal, entity.DataType);
        var patched = await service.PatchAsync(new() { Id = entity.Id, DataType = EvaluationDataType.Boolean,
            ClearFields = ["acceptanceRuleJson", "unit"], IsRequired = false }, default);
        Assert.True(patched.Success); Assert.Null(entity.Unit); Assert.Null(entity.AcceptanceRuleJson); Assert.False(entity.IsRequired);
    }

    [Fact]
    public void NewClearFieldsRejectConflicts()
    {
        Assert.NotNull(EvaluationStandardDefaultsRules.ValidateClearFields(new PatchEvaluationStandardCommand {
            Unit = "%", ClearFields = ["unit"] }));
        Assert.NotNull(EvaluationStandardDefaultsRules.ValidateClearFields(new PatchEvaluationStandardCommand {
            ClearFields = ["dataType"] }));
    }
}
