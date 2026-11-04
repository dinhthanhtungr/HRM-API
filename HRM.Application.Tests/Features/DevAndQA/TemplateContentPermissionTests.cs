using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class TemplateContentPermissionTests
{
    private static (EvaluationServicesFixture Service, IqcTestStore Store, EvaluationTemplate Template) Fixture(
        string role, EvaluationTemplateType type, EvaluationTemplateStatus status, string[] permissions, bool foreign = false)
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role], HasExplicitPermissionSet = true, Permissions = permissions };
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = foreign ? Guid.NewGuid() : user.CompanyId!.Value,
            Code = "ORIGINAL", Name = "Original", Version = 7, Type = type, Status = status };
        store.Seed("EvaluationTemplates", template);
        return (new(db, user, new CurrentUserPermissionService(user), new()), store, template);
    }

    [Theory]
    [InlineData(EvaluationTemplateType.Iqc)]
    [InlineData(EvaluationTemplateType.Coa)]
    [InlineData(EvaluationTemplateType.ProductEvaluation)]
    public async Task QcWithCorrectCapabilityEditsContentAtEveryStatus(EvaluationTemplateType type)
    {
        var permission = type == EvaluationTemplateType.Coa ? ApplicationPermissions.DevAndQA.ManageCoa : ApplicationPermissions.DevAndQA.ManageIqc;
        foreach (var status in Enum.GetValues<EvaluationTemplateStatus>())
        {
            var (service, store, template) = Fixture("QCUser", type, status, [permission]);
            var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = template.CompanyId, Code = "M", Name = "Moisture" };
            var other = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = template.CompanyId, Code = "D", Name = "Density" };
            store.Seed("EvaluationStandards", standard, other);
            var kept = EvaluationSnapshotFactory.CreateTemplateItem(standard,
                new EvaluationTemplate { Id = template.Id, CompanyId = template.CompanyId });
            template.Items.Add(kept);
            var snapshotTemplate = new EvaluationTemplate { Id = template.Id, CompanyId = template.CompanyId, Type = EvaluationTemplateType.Iqc, Status = EvaluationTemplateStatus.Published, Items = [kept] };
            var iqcSnapshot = EvaluationSnapshotFactory.CreateIqc(snapshotTemplate, Guid.NewGuid(), template.CompanyId);
            snapshotTemplate.Type = EvaluationTemplateType.Coa;
            var coaSnapshot = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = template.CompanyId };
            EvaluationSnapshotFactory.InitializeCoa(coaSnapshot, snapshotTemplate);
            var result = await service.Templates.PatchTemplateAsync(new(template.Id, Name: "New", Items: [
                new() { EvaluationStandardId = other.Id },
                new() { EvaluationStandardId = standard.Id, Configuration = new() { Unit = "%", IsRequired = true, ClearFields = ["testMethod"] } }
            ]), default);
            Assert.Equal(kept.Id, result.Items[1].Id);
            Assert.Equal(new[] { 1, 2 }, result.Items.Select(x => x.Configuration.SortOrder));
            Assert.Equal("%", result.Items[1].Configuration.Unit);
            Assert.Null(result.Items[1].Configuration.TestMethod);
            Assert.True(result.Items[1].Configuration.IsRequired);
            Assert.Equal(type, result.Type);
            Assert.Equal(7, result.Version);
            Assert.Equal(status, result.Status);
            Assert.Equal("ORIGINAL", result.Code);
            Assert.Single(iqcSnapshot.Results);
            Assert.Single(coaSnapshot.Results);
            Assert.Null(iqcSnapshot.Results.Single().Unit);
            Assert.Null(coaSnapshot.Results.Single().Unit);
            await service.Templates.PatchTemplateAsync(new(template.Id, Name: "Again"), default);
            await service.Templates.PatchTemplateAsync(new(template.Id, Items: null), default);
            Assert.Equal(2, template.Items.Count);
            await service.Templates.PatchTemplateAsync(new(template.Id, Items: []), default);
            Assert.Empty(template.Items);
            Assert.Equal(status, template.Status);
        }
    }

    [Theory]
    [InlineData("QCUser")]
    [InlineData("Admin")]
    [InlineData("President")]
    [InlineData("LabUser")]
    public async Task NonDeveloperCannotCreateOrChangeCode(string role)
    {
        var (service, store, template) = Fixture(role, EvaluationTemplateType.Iqc, EvaluationTemplateStatus.Draft,
            [ApplicationPermissions.DevAndQA.ManageIqc]);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.CreateTemplateAsync(
            new("NEW", "New", EvaluationTemplateType.Iqc, 1, []), default))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(
            new(template.Id, Code: "CHANGED"), default))).StatusCode);
        Assert.Equal("ORIGINAL", template.Code);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData(false, false, false, 403)]
    [InlineData(true, false, false, 403)]
    [InlineData(true, true, false, 403)]
    [InlineData(false, true, true, 404)]
    public async Task QcRequiresCorrectCapabilityAndCompany(bool wrongType, bool granted, bool foreign, int expected)
    {
        var type = wrongType ? EvaluationTemplateType.Coa : EvaluationTemplateType.Iqc;
        var (service, store, template) = Fixture("QCUser", type, EvaluationTemplateStatus.Published,
            granted || wrongType ? [ApplicationPermissions.DevAndQA.ManageIqc] : [], foreign);
        Assert.Equal(expected, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(
            new(template.Id, Name: "Denied"), default))).StatusCode);
        Assert.Equal("Original", template.Name);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task DeveloperStillCreatesAndRenamesWithCorrectCapability()
    {
        var (service, store, template) = Fixture("Developer", EvaluationTemplateType.Iqc, EvaluationTemplateStatus.Published,
            [ApplicationPermissions.DevAndQA.ManageIqc]);
        var created = await service.Templates.CreateTemplateAsync(new("NEW", "New", EvaluationTemplateType.Iqc, 1, []), default);
        Assert.Equal(EvaluationTemplateStatus.Draft, created.Status);
        Assert.Equal("CHANGED", (await service.Templates.PatchTemplateAsync(new(template.Id, Code: "CHANGED"), default)).Code);
    }
}
