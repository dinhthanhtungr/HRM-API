using HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using System.Reflection;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingEditorContractTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("company")]
    [InlineData("permission")]
    [InlineData("materials")]
    [InlineData("link")]
    [InlineData("stale")]
    public async Task Action_RejectsInvalidAccessAndStaleDraftBeforeDispatch(string reason)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        if (reason == "anonymous") f.User.IsAuthenticated = false;
        if (reason == "company") f.User.CompanyId = Guid.NewGuid();
        if (reason == "permission") { f.User.HasExplicitPermissionSet = true; f.User.Permissions = []; }
        if (reason == "materials")
        {
            f.User.HasExplicitPermissionSet = true;
            f.User.Permissions = [ApplicationPermissions.PLM.ManageProductionOrders, ApplicationPermissions.PLM.ViewProductionOrders];
        }
        if (reason == "link") f.Detail.IsActive = false;
        if (reason == "stale") f.Order.LabNote = "concurrent edit";
        var sender = DispatchProxy.Create<ISender, SenderStub>();
        var permissions = new CurrentUserPermissionService(f.User);
        var handler = new ExecuteProductionOrderEditorActionCommandHandler(f.Db, f.User, permissions,
            new PLMFieldVisibilityService(permissions), sender);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, new()
        {
            Action = "Save", Revision = revision,
            Items = reason == "materials" ? [new() { ItemId = Guid.NewGuid(), Quantity = 1 }] : null
        }), default);
        Assert.False(result.Success);
        Assert.Equal(0, ((SenderStub)(object)sender).Calls);
        Assert.Equal(0, f.Store.Saves);
    }

    public class SenderStub : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            return Task.FromResult(OperationResult.Ok());
        }
    }

    [Fact]
    public void Steps_PreserveLegacyCodesAndValues()
    {
        Assert.Equal(Enumerable.Range(0, 12), ManufacturingEditorPolicy.Steps.Select(x => x.Value));
        Assert.Equal(new[] { "MDTS_GH", "BTS_GH", "CT_MD_HT_GH", "MD_HT_GH", "CT_HT_GH", "CT_MDTS_GH" },
            ManufacturingEditorPolicy.Steps.Skip(6).Select(x => x.Code));
    }

    [Fact]
    public void EveryKnownStatus_CanSaveWithoutReopeningWorkflowActions()
    {
        foreach (var status in Enum.GetValues<ManufacturingProductOrder>().Where(x => x is not (ManufacturingProductOrder.Unknown or ManufacturingProductOrder.WaitingForStandardSample)))
        {
            var actions = ManufacturingEditorPolicy.Actions(status.ToString(), true, true, true);
            Assert.Contains("Save", actions);
            Assert.Equal(status == ManufacturingProductOrder.New, actions.Contains("UseStock"));
            Assert.Equal(status == ManufacturingProductOrder.FormulaSuccess, actions.Contains("Schedule"));
            Assert.Empty(ManufacturingEditorPolicy.Actions(status.ToString(), false, true, true));
        }
        Assert.Empty(ManufacturingEditorPolicy.Actions("999", true, true, true));
        Assert.Empty(ManufacturingEditorPolicy.Actions("Unknown", true, true, true));
        Assert.Empty(ManufacturingEditorPolicy.Actions(nameof(ManufacturingProductOrder.WaitingForStandardSample), true, true, true));
    }

    [Fact]
    public void SparseSave_PreservesLegacyUnknownStepAndNullableDates()
    {
        var order = new MfgProductionOrder { StepOfProduct = (StepOfProduct)99, BagType = "PE", LabNote = "old" };
        var patch = new ProductionOrderEditorRequest { Action = "Save", LabNote = "new" };
        Assert.Null(ProductionOrderEditorMapping.Validate(patch));
        var merged = ProductionOrderEditorMapping.Merge(order, patch);
        Assert.Equal((StepOfProduct)99, merged.StepOfProduct);
        Assert.Equal("PE", merged.BagType);
        Assert.Null(merged.ExpectedDate);
        Assert.Equal("new", merged.LabNote);
    }

    [Fact]
    public void ClearFields_RejectsUnknownConflictsAndNonSaveActions()
    {
        Assert.NotNull(ProductionOrderEditorMapping.Validate(new() { Action = "Save", ClearFields = ["status"] }));
        Assert.NotNull(ProductionOrderEditorMapping.Validate(new() { Action = "Save", LabNote = "x", ClearFields = ["labNote"] }));
        Assert.NotNull(ProductionOrderEditorMapping.Validate(new() { Action = "UseStock", ClearFields = ["labNote"] }));
        var request = new ProductionOrderEditorRequest { Action = "Save", ClearFields = ["stepOfProduct", "bagType"] };
        Assert.Null(ProductionOrderEditorMapping.Validate(request));
        var merged = ProductionOrderEditorMapping.Merge(new() { StepOfProduct = StepOfProduct.Forsb, BagType = "PE" }, request);
        Assert.Null(merged.StepOfProduct);
        Assert.Equal("", merged.BagType);
    }
}
