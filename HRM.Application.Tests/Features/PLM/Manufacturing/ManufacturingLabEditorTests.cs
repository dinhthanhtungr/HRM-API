using System.Reflection;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Manufacturings;
using MediatR;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingLabEditorTests
{
    [Theory]
    [InlineData("LabUser", "New", false)]
    [InlineData("LabUser", "FormulaRequested", true)]
    [InlineData("LabUser", "WaitingForStandardSample", true)]
    [InlineData("LabUser", "Finished", true)]
    [InlineData("PLPUUser", "New", true)]
    [InlineData("President", "New", true)]
    [InlineData("Developer", "New", true)]
    public async Task Editor_RoleAndStateMatrix(string role, string status, bool allowed)
    {
        var f = Fixture();
        f.User.Roles = [role];
        f.Order.Status = status;
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var permissions = new CurrentUserPermissionService(f.User);
        var sender = DispatchProxy.Create<ISender, Sender>();
        var handler = new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId,
            new() { Action = "Save", Revision = revision, QcCheck = "QC" }), default);
        Assert.Equal(allowed, result.Success);
    }

    [Fact]
    public async Task LabCannotChangeOrderLabNoteEvenWithOrderManagementClaim()
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Lab.LabUser];
        f.User.HasExplicitPermissionSet = true;
        f.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ManageProductionOrders,
            ApplicationPermissions.PLM.ManageManufacturingFormulas, ApplicationPermissions.PLM.ViewFormulaMaterials];
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var permissions = new CurrentUserPermissionService(f.User);
        var sender = DispatchProxy.Create<ISender, Sender>();
        var result = await new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender).Handle(new(f.Order.MfgProductionOrderId,
                new() { Action = "Save", Revision = revision, LabNote = "forbidden" }), default);
        Assert.False(result.Success);
        Assert.Equal(0, ((Sender)(object)sender).Calls);
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    [InlineData("QCUser", false)]
    [InlineData("anonymous", false)]
    [InlineData("foreign-company", false)]
    [InlineData("no-employee", false)]
    [InlineData("no-link", false)]
    [InlineData("explicit-deny", false)]
    public async Task SaveNotes_UsesCapabilityAndRecordScope(string role, bool allowed)
    {
        var f = Fixture();
        f.User.Roles = [role is "foreign-company" or "no-employee" or "no-link" or "explicit-deny" ? "LabUser" : role];
        if (role == "anonymous") f.User.IsAuthenticated = false;
        if (role == "foreign-company") f.User.CompanyId = Guid.NewGuid();
        if (role == "no-employee") f.User.EmployeeId = null;
        if (role == "no-link") f.Detail.IsActive = false;
        if (role == "explicit-deny") { f.User.HasExplicitPermissionSet = true; f.User.Permissions = []; }
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var sender = DispatchProxy.Create<ISender, Sender>();
        var permissions = new CurrentUserPermissionService(f.User);
        var handler = new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId,
            new() { Action = "Save", Revision = revision, QcCheck = "Lab update" }), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, ((Sender)(object)sender).Calls);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("batches")]
    [InlineData("step")]
    [InlineData("expected")]
    [InlineData("manufacturing")]
    [InlineData("bag")]
    [InlineData("clear")]
    [InlineData("stock")]
    [InlineData("schedule")]
    [InlineData("requirement")]
    [InlineData("clear-requirement")]
    [InlineData("plpuNote")]
    [InlineData("clear-plpuNote")]
    public async Task LabCannotPostPlanOrWorkflowChanges(string field)
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Lab.LabUser];
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var request = new ProductionOrderEditorRequest { Action = "Save", Revision = revision,
            TotalQuantity = field == "quantity" ? 100 : null, NumOfBatches = field == "batches" ? 2 : null,
            StepOfProduct = field == "step" ? StepOfProduct.Forsb : null,
            ExpectedDate = field == "expected" ? DateTime.Today : null,
            ManufacturingDate = field == "manufacturing" ? DateTime.Today : null,
            BagType = field == "bag" ? "PE" : null,
            Requirement = field == "requirement" ? "forbidden" : null,
            PlpuNote = field == "plpuNote" ? "forbidden" : null,
            ClearFields = field == "clear" ? ["expectedDate"] : field == "clear-requirement" ? ["requirement"] : field == "clear-plpuNote" ? ["plpuNote"] : [] };
        if (field is "stock" or "schedule") request = new() { Action = field == "stock" ? "UseStock" : "Schedule", Revision = revision };
        var permissions = new CurrentUserPermissionService(f.User);
        var sender = DispatchProxy.Create<ISender, Sender>();
        var handler = new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, request), default);
        Assert.False(result.Success);
        Assert.Equal(0, ((Sender)(object)sender).Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inform_WhitelistsFieldsByEditorCapability(bool developer)
    {
        var f = Fixture();
        f.User.Roles = [developer ? ApplicationRoles.Developer : ApplicationRoles.Lab.LabUser];
        var sender = DispatchProxy.Create<ISender, Sender>();
        var permissions = new CurrentUserPermissionService(f.User);
        var result = await new GetProductionOrderInformQueryHandler(sender, f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions)).Handle(new(f.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(developer ? ManufacturingEditorPolicy.HeaderFields.Where(x => x != "formulaNote").ToArray() : new[] { "qcCheck" }, result.Data!.EditableFields);
        Assert.True(result.Data.CanEditItems);
        Assert.Equal(new[] { "Save" }, result.Data.AllowedActions);
        Assert.Null(result.Data.ReadOnlyReason);
    }

    [Theory]
    [InlineData(true, true, "PO-123")]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    public async Task Inform_ReturnsPoOnlyFromActiveLinkedOrderInCompany(bool active, bool sameCompany, string? expected)
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Developer];
        f.Sale.PONo = "PO-123";
        f.Sale.IsActive = active;
        if (!sameCompany) f.Sale.CompanyId = Guid.NewGuid();
        var sender = DispatchProxy.Create<ISender, Sender>();
        ((Sender)(object)sender).LinkedOrderId = f.Sale.MerchandiseOrderId;
        var permissions = new CurrentUserPermissionService(f.User);
        var result = await new GetProductionOrderInformQueryHandler(sender, f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions)).Handle(new(f.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, result.Data!.PoNo);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Inform_SampleInfoUsesActiveProductInCompany(bool active, bool sameCompany)
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Lab.LabUser];
        var product = f.Order.Product;
        product.IsActive = active;
        product.UsageRate = 2.5;
        product.ExpiryType = "SixMonths";
        if (!sameCompany) product.CompanyId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        product.SampleRequests.Add(new() { SampleRequestId = sampleId, ExternalId = "TP-VALID",
            CompanyId = f.User.CompanyId!.Value, IsActive = true, CreatedDate = new DateTime(2026, 1, 1) });
        product.SampleRequests.Add(new() { SampleRequestId = Guid.NewGuid(), ExternalId = "TP-FOREIGN",
            CompanyId = Guid.NewGuid(), IsActive = true, CreatedDate = new DateTime(2026, 2, 1) });
        product.SampleRequests.Add(new() { SampleRequestId = Guid.NewGuid(), ExternalId = "TP-INACTIVE",
            CompanyId = f.User.CompanyId.Value, IsActive = false, CreatedDate = new DateTime(2026, 3, 1) });
        var sender = DispatchProxy.Create<ISender, Sender>();
        var permissions = new CurrentUserPermissionService(f.User);
        var result = await new GetProductionOrderInformQueryHandler(sender, f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions)).Handle(new(f.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        if (!active || !sameCompany) { Assert.Null(result.Data!.SampleInfo); return; }
        Assert.Equal(sampleId, result.Data!.SampleInfo!.SampleRequestId);
        Assert.Equal("TP-VALID", result.Data.SampleInfo.ExternalId);
        Assert.Equal(2.5, result.Data.SampleInfo.UsageRate);
        Assert.Equal("SixMonths", result.Data.SampleInfo.ExpiryType);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LabFormulaCreation_RequiresUnchangedPlan(bool changedPlan)
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Lab.LabUser];
        f.Order.Status = "New";
        f.Order.TotalQuantity = changedPlan ? 101 : 100;
        f.Order.NumOfBatches = 2;
        f.Order.StepOfProduct = StepOfProduct.Forsb;
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("Developer", true)]
    [InlineData("LabUser", false)]
    [InlineData("PLPUUser", false)]
    [InlineData("Admin", false)]
    public async Task RequirementEditing_IsSeparateFromOrderAndFormulaManagement(string role, bool allowed)
    {
        var f = Fixture();
        f.User.Roles = [role];
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var permissions = new CurrentUserPermissionService(f.User);
        Assert.Equal(allowed, permissions.HasPermission(ApplicationPermissions.PLM.EditProductionOrderRequirement));
        var sender = DispatchProxy.Create<ISender, Sender>();
        var handler = new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, new()
            { Action = "Save", Revision = revision, Requirement = "Developer note" }), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, ((Sender)(object)sender).Calls);
    }

    [Fact]
    public async Task ExplicitPermissionSet_WithoutRequirementCapabilityCannotEditIt()
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Developer];
        f.User.HasExplicitPermissionSet = true;
        f.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ManageProductionOrders];
        var sender = DispatchProxy.Create<ISender, Sender>();
        var permissions = new CurrentUserPermissionService(f.User);
        var result = await new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender).Handle(new(f.Order.MfgProductionOrderId, new()
            { Action = "Save", Revision = new string('a', 64), ClearFields = ["requirement"] }), default);
        Assert.False(result.Success);
        Assert.Equal(0, ((Sender)(object)sender).Calls);
    }

    [Theory]
    [InlineData("LabUser", false)]
    [InlineData("PLPUUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    public async Task PlpuNote_RequiresOrderManagement(string role, bool allowed)
    {
        var f = Fixture();
        f.User.Roles = [role];
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        var sender = DispatchProxy.Create<ISender, Sender>();
        var permissions = new CurrentUserPermissionService(f.User);
        var result = await new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender).Handle(new(f.Order.MfgProductionOrderId, new()
            { Action = "Save", Revision = revision, PlpuNote = "manual PLPU" }), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, ((Sender)(object)sender).Calls);
    }

    [Fact]
    public async Task LabCannotChangePlpuNoteThroughFormulaCreation()
    {
        var f = Fixture();
        f.User.Roles = [ApplicationRoles.Lab.LabUser];
        f.Order.Status = "New";
        f.Order.TotalQuantity = 100;
        f.Order.NumOfBatches = 2;
        f.Order.StepOfProduct = StepOfProduct.Forsb;
        f.Order.PlpuNote = "stored";
        var original = f.Request();
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, new()
        {
            SourceFormulaId = original.SourceFormulaId, Origin = original.Origin,
            TotalQuantity = original.TotalQuantity, NumOfBatches = original.NumOfBatches,
            StepOfProduct = original.StepOfProduct, ExpectedDate = original.ExpectedDate,
            ManufacturingDate = original.ManufacturingDate, PlpuNote = "forbidden"
        }), default);
        Assert.False(result.Success);
        Assert.Equal("stored", f.Order.PlpuNote);
        Assert.Equal(0, f.Store.Saves);
    }

    private static ManufacturingFormulaCreationTests.Fixture Fixture()
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Order.Status = "FormulaRequested";
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        return f;
    }

    public class Sender : DispatchProxy
    {
        public Guid LinkedOrderId { get; set; }
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            if (args![0] is GetProductionOrderWorkspaceQuery)
                return Task.FromResult(OperationResult<ProductionOrderWorkspaceDto>.Ok(new ProductionOrderWorkspaceDto(new() { MerchandiseOrderId = LinkedOrderId }, [], false, false, 0, 0)));
            return Task.FromResult(OperationResult.Ok());
        }
    }
}
