using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PublishEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;
using HRM.Application.Features.DevAndQA.IQC.Commands.InitializeIqcEvaluation;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqcEvaluationResult;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Commands.PatchMeasurementMethod;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class EvaluationApiTests
{
    [Theory]
    [InlineData("Developer")]
    [InlineData("QCUser")]
    [InlineData("LabUser")]
    public async Task CatalogSyncReplacesOnlySelectedRowsIncludingNullAndKeepsIdentityAndOrder(string role)
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = [role], HasExplicitPermissionSet = true, Permissions = [] });
        var company = user.CompanyId!.Value;
        var a = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "A", Name = "New A", NameEn = "English A", IsActive = true, TestMethod = "Catalog method" };
        var b = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "B", Name = "New B", IsActive = true };
        store.Seed("EvaluationStandards", a, b);
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = company, Code = "IQC", Name = "IQC", Type = EvaluationTemplateType.Iqc, Status = EvaluationTemplateStatus.Published };

        var first = new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationStandardId = a.Id, CriterionCode = "OLD_A", CriterionName = "Old A", Unit = "%", IsRequired = true, SortOrder = 4 };
        var second = new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationStandardId = b.Id, CriterionCode = "B", CriterionName = "Old B", Unit = "kg", SortOrder = 8 };
        template.Items = [first, second];
        store.Seed("EvaluationTemplates", template);
        var preview = await service.Templates.PreviewCatalogSyncAsync(new(template.Id, ["A"]), default);
        Assert.Equal("Old A", Assert.Single(preview.Changes).Before.CriterionName);
        Assert.Equal("New A", Assert.Single(preview.Changes).After.CriterionName);
        Assert.Equal("Old A", first.CriterionName);
        Assert.Equal(0, store.Saves);
        await service.Templates.PatchTemplateAsync(new(template.Id, SyncStandardCodes: [" a "]), default);
        Assert.Equal("New A", first.CriterionName);
        Assert.Equal("English A", first.CriterionNameEn);
        Assert.Equal("A", first.CriterionCode);
        Assert.Null(first.Unit);
        Assert.False(first.IsRequired);
        Assert.Equal("Catalog method", first.TestMethod);
        Assert.Equal(4, first.SortOrder);
        Assert.Same(first, template.Items.First());
        Assert.Equal("Old B", second.CriterionName);
        Assert.Equal("kg", second.Unit);
        await service.Templates.PatchTemplateAsync(new(template.Id, SyncStandardCodes: ["A", "B"]), default);
        Assert.Equal("New B", second.CriterionName);
        Assert.Null(second.Unit);
        Assert.Equal(8, second.SortOrder);
        var denied = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(new(template.Id, Name: "Other change"), default));
        Assert.Equal(403, denied.StatusCode);
        template.CompanyId = Guid.NewGuid();
        var foreign = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PreviewCatalogSyncAsync(new(template.Id, ["A"]), default));
        Assert.Equal(404, foreign.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SaleUser")]
    public async Task CatalogSyncRejectsOtherRolesEvenWithManageClaims(string role)
    {
        var user = new IqcWorkflowTests.User { Roles = [role], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.DevAndQA.ManageIqc, ApplicationPermissions.DevAndQA.ManageCoa] };
        var service = new EvaluationServicesFixture(null!, user, new CurrentUserPermissionService(user), new());
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.Templates.PreviewCatalogSyncAsync(new(Guid.NewGuid(), ["A"]), default))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.Templates.PatchTemplateAsync(new(Guid.NewGuid(), SyncStandardCodes: ["A"]), default))).StatusCode);
    }

    [Fact]
    public async Task OrdinaryTemplateSaveKeepsOldSnapshotAndNullOverridesWithoutActiveCatalog()
    {
        var (service, store, user) = Fixture();
        var id = Guid.NewGuid();
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "IQC", Name = "IQC", Type = EvaluationTemplateType.Iqc };
        var item = new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationStandardId = id, CriterionCode = "A", CriterionName = "Old", CriterionNameEn = "Old English", Unit = null, IsRequired = true, SortOrder = 9 };
        template.Items = [item];
        store.Seed("EvaluationTemplates", template);
        await service.Templates.PatchTemplateAsync(new(template.Id, Items: [new() { EvaluationStandardId = id, Configuration = new() { IsRequired = false } }]), default);
        Assert.Equal("Old", item.CriterionName);
        Assert.Equal("Old English", item.CriterionNameEn);
        Assert.Null(item.Unit);
        Assert.False(item.IsRequired);
        Assert.Equal(1, item.SortOrder);
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("inactive", 409)]
    [InlineData("not-in-template", 409)]
    [InlineData("duplicate", 400)]
    public async Task CatalogSyncRejectsInvalidSourcesBeforeSaving(string scenario, int status)
    {
        var (service, store, user) = Fixture();
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "A", Name = "A", IsActive = scenario != "inactive" };
        store.Seed("EvaluationStandards", standard);
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId.Value, Code = "IQC", Name = "IQC", Type = EvaluationTemplateType.Iqc };
        if (scenario != "not-in-template") template.Items.Add(new() { EvaluationStandardId = standard.Id, CriterionCode = "A", CriterionName = "Old" });
        store.Seed("EvaluationTemplates", template);
        string[] codes = scenario == "duplicate" ? ["A", "a"] : [scenario == "missing" ? "B" : "A"];
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(new(template.Id, SyncStandardCodes: codes), default));
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(0, store.Saves);
    }
    [Fact]
    public async Task LabUserCannotRenameStandardCode()
    {
        var (db, store) = IqcTestStore.CreateContext<IIqcDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["LabUser"] };
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var entity = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "OLD", Name = "Old" };
        store.Seed("EvaluationStandards", entity);
        var service = new HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards.EvaluationStandardService(
            db, user, new CurrentUserPermissionService(user), new());
        var result = await service.PatchAsync(new() { Id = entity.Id, Code = "NEW", Name = "Changed" }, default);
        Assert.False(result.Success);
        Assert.Equal("OLD", entity.Code);
        Assert.Equal("Old", entity.Name);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void ExplicitLabManageClaimIsRequiredAndDoesNotGrantCoaManage()
    {
        var user = new IqcWorkflowTests.User { Roles = ["LabUser"], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.DevAndQA.ViewIqc, ApplicationPermissions.DevAndQA.ManageIqc] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc));
        Assert.False(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageCoa));
        Assert.Contains(ApplicationPermissions.DevAndQA.ManageIqc,
            ApplicationPermissionCatalog.ResolveRole("LabUser", false, []));
        Assert.DoesNotContain(ApplicationPermissions.DevAndQA.ManageIqc,
            ApplicationPermissionCatalog.ResolveRole("LabUser", true, [ApplicationPermissions.DevAndQA.ViewIqc]));
    }
    [Theory]
    [InlineData(EvaluationTemplateStatus.Draft, false, true)]
    [InlineData(EvaluationTemplateStatus.Draft, true, false)]
    [InlineData(EvaluationTemplateStatus.Published, false, true)]
    [InlineData(EvaluationTemplateStatus.Retired, false, true)]
    public async Task LabUserEditsContentLikeQcUser(EvaluationTemplateStatus status, bool changeCode, bool allowed)
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = ["LabUser"] });
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Type = EvaluationTemplateType.Iqc, Status = status, Code = "IQC", Name = "Old", Version = 1 };
        store.Seed("EvaluationTemplates", template);
        var request = new PatchEvaluationTemplateCommand(template.Id, Code: changeCode ? "NEW" : null, Name: "New name");
        if (allowed)
        {
            var result = await service.Templates.PatchTemplateAsync(request, default);
            Assert.Equal("New name", result.Name);
            Assert.Equal("IQC", template.Code);
        }
        else
        {
            await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(request, default));
            Assert.Equal(0, store.Saves);
            Assert.Equal("Old", template.Name);
        }
    }

    [Fact]
    public async Task LabUserCannotCreateTemplateButCanManageCoaType()
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = ["LabUser"] });
        var create = new HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate.CreateEvaluationTemplateCommand(
            "IQC", "Name", EvaluationTemplateType.Iqc, 1, []);
        var denied = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.CreateTemplateAsync(create, default));
        Assert.Equal(403, denied.StatusCode);
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Type = EvaluationTemplateType.Coa, Status = EvaluationTemplateStatus.Draft, Code = "COA", Name = "Old" };
        store.Seed("EvaluationTemplates", template);
        var updated = await service.Templates.PatchTemplateAsync(new(template.Id, Name: "New"), default);
        Assert.Equal("New", updated.Name);
    }
    private static (EvaluationServicesFixture Service, IqcTestStore Store, IqcWorkflowTests.User User) Fixture(IqcWorkflowTests.User? current = null)
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = current ?? new IqcWorkflowTests.User();
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        return (new(db, user, new CurrentUserPermissionService(user), new()), store, user);
    }

    [Theory]
    [InlineData("Developer", EvaluationTemplateStatus.Published)]
    [InlineData("Developer", EvaluationTemplateStatus.Retired)]
    [InlineData("QCUser", EvaluationTemplateStatus.Published)]
    [InlineData("QCUser", EvaluationTemplateStatus.Retired)]
    public async Task AllowedEditorsReplaceReleasedItemsKeepingExistingIds(string role, EvaluationTemplateStatus status)
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = [role] });
        var company = user.CompanyId!.Value;
        var kept = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "A", Name = "A", IsActive = true };
        var added = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "B", Name = "B", IsActive = true };
        store.Seed("EvaluationStandards", kept, added);
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = company,
            Code = "IQC", Name = "IQC", Type = EvaluationTemplateType.Iqc, Status = status };
        var old = new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationTemplateId = template.Id,
            EvaluationStandardId = kept.Id, CriterionCode = "A", CriterionName = "Old" };
        template.Items.Add(old);
        template.Items.Add(new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationTemplateId = template.Id, EvaluationStandardId = Guid.NewGuid() });
        var snapshot = new IqcInspectionResult { SourceTemplateItemId = old.Id, CriterionName = "Saved", HasConfigurationSnapshot = true };
        store.Seed("EvaluationTemplates", template);
        var response = await service.Templates.PatchTemplateAsync(new(template.Id, Items: [
            new() { EvaluationStandardId = kept.Id, Configuration = new() { Unit = "%", IsRequired = true } }, new() { EvaluationStandardId = added.Id }]), default);
        Assert.Equal(old.Id, response.Items[0].Id);
        Assert.Equal("%", response.Items[0].Configuration.Unit);
        Assert.True(response.Items[0].Configuration.IsRequired);
        Assert.NotEqual(Guid.Empty, response.Items[1].Id);
        var inserted = Assert.IsType<EvaluationTemplateItem>(Assert.Single(store.Added));
        Assert.Equal(added.Id, inserted.EvaluationStandardId);
        Assert.Equal(response.Items[1].Id, inserted.Id);
        Assert.DoesNotContain(old, store.Added);
        Assert.Equal(new[] { 1, 2 }, response.Items.Select(x => x.Configuration.SortOrder));
        Assert.Equal(status, template.Status);
        Assert.Equal("Saved", snapshot.CriterionName);
        await service.Templates.PatchTemplateAsync(new(template.Id, Items: [
            new() { EvaluationStandardId = kept.Id }, new() { EvaluationStandardId = added.Id }]), default);
        Assert.Single(store.Added); // Lưu lại dòng đã có không đánh dấu INSERT lần nữa.
        await service.Templates.PatchTemplateAsync(new(template.Id, Items: []), default);
        Assert.Empty(template.Items);
    }

    [Fact]
    public async Task MethodOrderIsAssignedByServerAndCannotBePatched()
    {
        var (service, store, user) = Fixture();
        store.Seed("MeasurementMethods", new MeasurementMethod { Id = Guid.NewGuid(),
            CompanyId = user.CompanyId!.Value, Code = "OLD", Name = "Old", SortOrder = 7 });
        var created = await service.Methods.CreateMethodAsync(new("NEW", "New", SortOrder: 999), default);
        Assert.Equal(8, created.SortOrder);
        var method = Assert.IsType<MeasurementMethod>(Assert.Single(store.Added));
        store.Seed("MeasurementMethods", method);
        var patched = await service.Methods.PatchMethodAsync(new(method.Id, SortOrder: 999), default);
        Assert.Equal(8, patched.SortOrder);
    }

    [Fact]
    public void EnglishCriterionNameIsCopiedToIqcAndCoaSnapshots()
    {
        var company = Guid.NewGuid();
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company,
            Code = "DENSITY", Name = "Tỷ trọng", NameEn = "Density", IsActive = true };
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = company };
        var item = EvaluationSnapshotFactory.CreateTemplateItem(standard, template);
        template.Items.Add(item);
        template.Type = EvaluationTemplateType.Iqc;
        template.Status = EvaluationTemplateStatus.Published;
        var iqc = EvaluationSnapshotFactory.CreateIqc(template, Guid.NewGuid(), company);
        template.Type = EvaluationTemplateType.Coa;
        var coa = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = company };
        EvaluationSnapshotFactory.InitializeCoa(coa, template);

        standard.NameEn = "Changed catalog";
        item.CriterionNameEn = "Changed template";

        Assert.Equal("Density", Assert.Single(iqc.Results).CriterionNameEn);
        Assert.Equal("Density", Assert.Single(coa.Results).CriterionNameEn);
    }

    [Fact]
    public async Task EnglishMethodNameSupportsPatchPreserveClearAndConflict()
    {
        var (service, store, user) = Fixture();
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Code = "DRY", Name = "Sấy", NameEn = "Drying" };
        store.Seed("MeasurementMethods", method);
        Assert.Equal("Drying", (await service.Methods.PatchMethodAsync(new(method.Id), default)).NameEn);
        Assert.Equal("New name", (await service.Methods.PatchMethodAsync(new(method.Id, NameEn: " New name "), default)).NameEn);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Methods.PatchMethodAsync(
            new(method.Id, NameEn: "Conflict", ClearFields: ["nameEn"]), default));
        Assert.Equal("New name", method.NameEn);
        Assert.Null((await service.Methods.PatchMethodAsync(new(method.Id, ClearFields: ["nameEn"]), default)).NameEn);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("KHOUser")]
    [InlineData("ACCUser")]
    public async Task MutationRequiresCapabilityBeforeReadingDatabase(string role)
    {
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var service = new EvaluationServicesFixture(null!, user, new CurrentUserPermissionService(user), new());
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Methods.PatchMethodAsync(new(Guid.NewGuid()), default));
        Assert.Equal(403, error.StatusCode);
    }

    [Theory]
    [InlineData("QCUser", true, true)]
    [InlineData("President", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("Developer", true, true)]
    [InlineData("ACCUser", true, false)]
    [InlineData("LabUser", true, true)]
    [InlineData("KHOUser", false, false)]
    [InlineData("SaleUser", false, false)]
    public void CoaPermissionsUseSeparateRoleMatrix(string role, bool view, bool manage)
    {
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(view, permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewCoa));
        Assert.Equal(manage, permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageCoa));
        Assert.Contains(ApplicationPermissions.DevAndQA.ViewCoa, ApplicationPermissionCatalog.Codes);
        Assert.Contains(ApplicationPermissions.DevAndQA.ManageCoa, ApplicationPermissionCatalog.Codes);
    }

    [Fact]
    public void LabUserCanManageIqcAndCoa()
    {
        var user = new IqcWorkflowTests.User { Roles = ["LabUser"] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc));
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewCoa));
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc));
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageCoa));
        var explicitUser = new IqcWorkflowTests.User { Roles = ["LabUser"], HasExplicitPermissionSet = true, Permissions = [] };
        Assert.False(new CurrentUserPermissionService(explicitUser).HasPermission(ApplicationPermissions.DevAndQA.ViewIqc));
    }

    [Fact]
    public async Task UnauthenticatedOrUnknownPermissionFailsClosed()
    {
        var user = new IqcWorkflowTests.User { IsAuthenticated = false };
        var service = new EvaluationServicesFixture(null!, user, new CurrentUserPermissionService(user), new());
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.GetCoaAsync(Guid.NewGuid(), default))).StatusCode);
        var explicitUser = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = ["unknown.permission"] };
        service = new(null!, explicitUser, new CurrentUserPermissionService(explicitUser), new());
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.GetTemplateAsync(Guid.NewGuid(), default))).StatusCode);
    }

    [Fact]
    public async Task IqcCapabilityDoesNotGrantCoaAccess()
    {
        var user = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.DevAndQA.ViewIqc, ApplicationPermissions.DevAndQA.ManageIqc] };
        var service = new EvaluationServicesFixture(null!, user, new CurrentUserPermissionService(user), new());
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.GetCoaAsync(Guid.NewGuid(), default))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.IssueCoaAsync(Guid.NewGuid(), default))).StatusCode);
    }

    [Fact]
    public async Task MethodAndCoaFromOtherCompanyReturn404()
    {
        var (service, store, _) = Fixture();
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Code = "PRIVATE", Name = "Private" };
        var coa = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        store.Seed("MeasurementMethods", method);
        store.Seed("CoaCertificates", coa);
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Methods.GetMethodAsync(method.Id, default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.GetCoaAsync(coa.Id, default))).StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task PatchMethodNoOpDoesNotUpdateAudit()
    {
        var (service, store, user) = Fixture();
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Code = "DRY", Name = "Sấy" };
        store.Seed("MeasurementMethods", method);
        await service.Methods.PatchMethodAsync(new(method.Id, Code: "DRY", Name: "Sấy"), default);
        Assert.Equal(0, store.Saves);
        Assert.Null(method.UpdatedDate);
    }

    [Theory]
    [InlineData("Admin", EvaluationTemplateStatus.Published)]
    public async Task ReleasedTemplateCannotBeEdited(string role, EvaluationTemplateStatus status)
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = [role] });
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Type = EvaluationTemplateType.Iqc, Status = status, Name = "Old" };
        store.Seed("EvaluationTemplates", template);
        Assert.Equal(409, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Templates.PatchTemplateAsync(new(template.Id, Name: "New"), default))).StatusCode);
        Assert.Equal("Old", template.Name);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData(EvaluationTemplateStatus.Draft)]
    [InlineData(EvaluationTemplateStatus.Published)]
    [InlineData(EvaluationTemplateStatus.Retired)]
    public async Task DeveloperCanEditDraftTemplateWithoutChangingStatus(EvaluationTemplateStatus status)
    {
        var (service, store, user) = Fixture(new IqcWorkflowTests.User { Roles = ["Developer"] });
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Type = EvaluationTemplateType.Iqc, Status = status, Code = "IQC", Name = "Old" };
        store.Seed("EvaluationTemplates", template);

        await service.Templates.PatchTemplateAsync(new(template.Id, Name: "New"), default);

        Assert.Equal("New", template.Name);
        Assert.Equal(status, template.Status);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task DeveloperCannotPatchTemplateOutsideCompany()
    {
        var (service, store, _) = Fixture(new IqcWorkflowTests.User { Roles = ["Developer"] });
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            Type = EvaluationTemplateType.Iqc, Status = EvaluationTemplateStatus.Published, Code = "IQC", Name = "Old" };
        store.Seed("EvaluationTemplates", template);

        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.Templates.PatchTemplateAsync(new(template.Id, Name: "New"), default));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Old", template.Name);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task InitializeIqcCopiesDefaultsAndIsIdempotent()
    {
        var (service, store, user) = Fixture();
        var company = user.CompanyId!.Value;
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 7,
            WarehouseVoucherDetail = new WarehouseVoucherDetail { Voucher = new WarehouseVoucher { CompanyId = company } } };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "M", Name = "Độ ẩm" };
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), CompanyId = company, Code = "DEFAULT", Name = "IQC", Type = EvaluationTemplateType.Iqc,
            Status = EvaluationTemplateStatus.Published, IsDefault = true, Version = 1 };
        template.Items.Add(new EvaluationTemplateItem { Id = Guid.NewGuid(), EvaluationTemplateId = template.Id, EvaluationStandardId = standard.Id,
            CriterionCode = "M", CriterionName = "Độ ẩm", DataType = EvaluationDataType.Decimal, Unit = "%", AcceptanceRuleJson = "{\"operator\":\"LessThanOrEqual\",\"value\":0.2}" });
        store.Seed("QCInputByQCs", qc); store.Seed("EvaluationStandards", standard); store.Seed("EvaluationTemplates", template);
        var result = await service.Iqc.InitializeIqcAsync(new(7), default);
        var saved = Assert.IsType<IqcInspectionEvaluation>(Assert.Single(store.Added));
        Assert.Equal(template.Id, result.EvaluationTemplateId);
        Assert.True(Assert.Single(result.Results).HasConfigurationSnapshot);
        template.Items.Single().Unit = "Changed";
        Assert.Equal("%", saved.Results.Single().Unit);
        store.Seed("IqcInspectionEvaluations", saved);
        await service.Iqc.InitializeIqcAsync(new(7), default);
        Assert.Equal(1, store.Saves);
        Assert.Single(store.Added);
    }

    [Fact]
    public async Task ResultIdMustBelongToRequestedIqc()
    {
        var (service, store, user) = Fixture();
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 7,
            WarehouseVoucherDetail = new WarehouseVoucherDetail { Voucher = new WarehouseVoucher { CompanyId = user.CompanyId!.Value } } };
        store.Seed("QCInputByQCs", qc);
        store.Seed("IqcInspectionEvaluations", new IqcInspectionEvaluation { Id = Guid.NewGuid(), QCInputByQCId = qc.QCInputByQCId });
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Iqc.PatchIqcResultAsync(new(7, Guid.NewGuid(), "0.1"), default))).StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task IssuedCoaCannotChangeHeaderOrResults()
    {
        var (service, store, user) = Fixture();
        var coa = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, Status = CoaCertificateStatus.Issued };
        store.Seed("CoaCertificates", coa);
        Assert.Equal(409, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.PatchCoaAsync(new(coa.Id, BatchNo: "New"), default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.PatchCoaResultAsync(new(coa.Id, Guid.NewGuid(), "New"), default))).StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task CoaIssueRequiresAllRequiredValues()
    {
        var (service, store, user) = Fixture();
        var coa = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value };
        coa.Results.Add(new CoaCertificateResult { Id = Guid.NewGuid(), CriterionCode = "M", CriterionName = "Độ ẩm", DataType = EvaluationDataType.Decimal,
            IsRequired = true, HasConfigurationSnapshot = true });
        store.Seed("CoaCertificates", coa);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => service.Coa.IssueCoaAsync(coa.Id, default));
        Assert.Equal(CoaCertificateStatus.Draft, coa.Status);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void ConfigPatchRejectsConflictingClearAndKeepsFalseAndZero()
    {
        var config = new EvaluationConfigurationDto(EvaluationDataType.Decimal, "%", null, null, null, null, null, null, true, 3);
        Assert.Throws<EvaluationRequestException>(() => EvaluationConfiguration.Merge(config, new() { Unit = "%", ClearFields = ["unit"] }, null));
        var result = EvaluationConfiguration.Merge(config, new() { IsRequired = false, SortOrder = 0 }, null);
        Assert.False(result.IsRequired);
        Assert.Equal(config.SortOrder, result.SortOrder);
        Assert.Equal("%", result.Unit);
    }

    [Theory]
    [InlineData(EvaluationDataType.Decimal, "0,2")]
    [InlineData(EvaluationDataType.Integer, "0.2")]
    [InlineData(EvaluationDataType.Boolean, "1")]
    [InlineData(EvaluationDataType.Date, "07/10/2026")]
    public void InvalidMeasuredValuesAreRejected(EvaluationDataType type, string value)
    {
        var config = new EvaluationConfigurationDto(type, null, null, null, null, null, null, null, false, 0);
        Assert.Throws<EvaluationRequestException>(() => EvaluationValueRules.Normalize(value, config));
    }

    [Fact]
    public void InputLimitsAreIndependentOfAcceptanceThreshold()
    {
        var config = new EvaluationConfigurationDto(EvaluationDataType.Decimal, "%", null, null, null, "{\"minValue\":0,\"maxValue\":100,\"decimalPlaces\":2}",
            "{\"operator\":\"LessThanOrEqual\",\"value\":0.2}", null, true, 0);
        Assert.Equal("0.5", EvaluationValueRules.Normalize("0.50", config));
        Assert.Throws<EvaluationRequestException>(() => EvaluationValueRules.Normalize("101", config));
        Assert.Throws<EvaluationRequestException>(() => EvaluationValueRules.Normalize("0.123", config));
    }
}
