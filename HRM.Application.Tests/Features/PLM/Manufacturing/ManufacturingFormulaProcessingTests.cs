using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using System.Reflection;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Commands.ProcessManufacturingFormula;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Commands.CopyProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateLabManufacturingFormula;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaProcessingTests
{
    [Theory]
    [InlineData("New", false)]
    [InlineData("WaitingForStandardSample", true)]
    public async Task LabStandardToggle_RespectsNewReadonlyAndWaitingState(string status, bool allowed)
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Lab.LabUser];
        f.Base.Order.Status = status;
        var before = f.Formula.Status;
        var result = await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId,
            f.Formula.ManufacturingFormulaId, new() { IsStandard = true, StandardChangeReason = "QC" }), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(status, f.Base.Order.Status);
        Assert.Equal(before, f.Formula.Status);
    }

    [Fact]
    public async Task LabCreate_RejectsExtremeRatiosBeforeSummingOrOpeningTransaction()
    {
        var f = new Fixture();
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        var result = await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA, Items = [
                new() { SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = row.MaterialId!.Value,
                    ItemType = ItemType.Material, Quantity = decimal.MaxValue },
                new() { ItemId = row.MaterialId.Value, ItemType = ItemType.Material, Quantity = decimal.MaxValue }] }), default);
        Assert.False(result.Success);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LabWriters_ResolveCurrentMaterialPriceOnServerAndSnapshotPreviousPrice(bool create)
    {
        var f = new Fixture();
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        var oldPrice = row.UnitPrice;
        f.Prices.Data[new(ItemType.Material, row.MaterialId!.Value)] = new() { CurrentPrice = 37m };
        ManufacturingFormulaInputItem[] items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
            ItemId = row.MaterialId.Value, ItemType = ItemType.Material, Quantity = 1 }];
        var result = create
            ? (await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
                SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA, Items = items }), default)).Success
            : (await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId,
                new() { Items = items }), default)).Success;
        Assert.True(result);
        var formula = create ? Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>()) : f.Formula;
        Assert.Equal(37m, Assert.Single(formula.ManufacturingFormulaMaterials, x => x.IsActive).UnitPrice);
        Assert.Equal(1, f.Prices.Calls);
        if (!create) Assert.Equal(oldPrice, Assert.Single(Assert.Single(formula.ManufacturingFormulaVersions).Items).UnitPrice);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task LabCreate_ValidatesScheduledStandardAndPrefillInactiveProvenance(bool unexpired, bool matchingVu)
    {
        var f = new Fixture();
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        row.IsActive = false;
        f.Base.Store.Seed<ProductionSelectVersion>(nameof(f.Base.Db.ProductionSelectVersions));
        f.Formula.SourceVUFormulaId = matchingVu ? f.Base.Order.FormulaId : Guid.NewGuid();
        f.Base.Store.Seed(nameof(f.Base.Db.ProductStandardFormulas), new ProductStandardFormula {
            ProductStandardFormulaId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId,
            ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Formula.ManufacturingFormulaId,
            ManufacturingFormula = f.Formula, ValidFrom = Fixture.Now.AddDays(-2),
            ValidTo = unexpired ? Fixture.Now.AddDays(1) : Fixture.Now.AddDays(-1) });
        var result = await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA,
            Items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
                ItemId = row.MaterialId!.Value, ItemType = ItemType.Material, Quantity = 1 }] }), default);
        var allowed = unexpired && (matchingVu || !f.Base.Order.FormulaId.HasValue);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed, f.Base.Store.Transaction.Committed);
        if (allowed) Assert.Single(Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>()).ManufacturingFormulaMaterials);
        else Assert.Empty(f.Base.Store.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LabWriters_RetainActiveZeroQuantityRows(bool create)
    {
        var f = new Fixture();
        var original = f.Base.Db.ManufacturingFormulaMaterials.Single();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId, IsActive = true,
            ExternalId = "ZERO", Name = "zero ingredient", CategoryId = f.Base.Material.CategoryId, Unit = "kg" };
        var zero = new ManufacturingFormulaMaterial { ManufacturingFormulaMaterialId = Guid.NewGuid(),
            ManufacturingFormulaId = f.Formula.ManufacturingFormulaId, ManufacturingFormula = f.Formula, IsActive = true,
            MaterialId = material.MaterialId, CategoryId = material.CategoryId, Quantity = 0, UnitPrice = 5, itemType = ItemType.Material };
        f.Base.Store.Seed(nameof(f.Base.Db.Materials), f.Base.Material, material);
        f.Base.Store.Seed(nameof(f.Base.Db.ManufacturingFormulaMaterials), original, zero);
        f.Formula.ManufacturingFormulaMaterials.Add(zero);
        ManufacturingFormulaInputItem[] items = [new() { SourceItemId = original.ManufacturingFormulaMaterialId,
            ItemId = original.MaterialId!.Value, ItemType = ItemType.Material, Quantity = 1 },
            new() { SourceItemId = zero.ManufacturingFormulaMaterialId, ItemId = material.MaterialId, ItemType = ItemType.Material, Quantity = 0 }];
        if (create) Assert.True((await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA, Items = items }), default)).Success);
        else Assert.True((await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId,
            new() { Items = items }), default)).Success);
        var saved = create ? Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>()) : f.Formula;
        Assert.Equal(2, saved.ManufacturingFormulaMaterials.Count(x => x.IsActive));
        Assert.Contains(saved.ManufacturingFormulaMaterials, x => x.IsActive && x.MaterialId == material.MaterialId && x.Quantity == 0);
    }

    [Fact]
    public async Task LabCreate_ClosesCurrentSelectionAndStandardWithoutSchedulingOrReservation()
    {
        var f = new Fixture();
        f.Formula.Name = "F003";
        f.Selection.ValidFrom = Fixture.Now.AddDays(-1);
        var oldStandard = new ProductStandardFormula { ProductStandardFormulaId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId,
            ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Formula.ManufacturingFormulaId, ValidFrom = Fixture.Now.AddDays(-2) };
        f.Base.Store.Seed(nameof(f.Base.Db.ProductStandardFormulas), oldStandard);
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        var result = await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA, IsStandard = true,
            Items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = row.MaterialId!.Value,
                ItemType = ItemType.Material, Quantity = 1 }] }), default);
        Assert.True(result.Success, result.Message);
        var formula = Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>());
        Assert.Equal("F004", formula.Name);
        Assert.Equal("Checking", formula.Status);
        Assert.Equal("FormulaSuccess", f.Base.Order.Status);
        Assert.Equal(Fixture.Now, f.Selection.ValidTo);
        Assert.Equal(Fixture.Now, oldStandard.ValidTo);
        Assert.Equal(f.Base.User.EmployeeId, Assert.Single(f.Base.Store.Added.OfType<ProductionSelectVersion>()).CreatedBy);
        Assert.Single(f.Base.Store.Added.OfType<ProductStandardFormula>());
        Assert.Empty(f.Base.Store.Added.OfType<SchedualMfg>());
        Assert.Empty(f.Base.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>());
        Assert.Equal(Fixture.Now.AddDays(-1), f.Base.Order.CreatedDate);
        Assert.Equal(2, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("source")]
    [InlineData("role")]
    [InlineData("ratio")]
    [InlineData("save")]
    public async Task LabCreate_RejectsInvalidInputOrRollsBackSave(string reason)
    {
        var f = new Fixture();
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        if (reason == "company") f.Base.Order.CompanyId = Guid.NewGuid();
        if (reason == "role") f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        if (reason == "save") f.Base.Store.ThrowOnSave = true;
        var result = await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = reason == "source" ? Guid.NewGuid() : f.Formula.ManufacturingFormulaId,
            SourceType = FormulaSource.FromVA, Items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
                ItemId = row.MaterialId!.Value, ItemType = ItemType.Material, Quantity = reason == "ratio" ? .5m : 1 }] }), default);
        Assert.False(result.Success);
        Assert.False(f.Base.Store.Transaction.Committed);
        if (reason == "save") Assert.True(f.Base.Store.Transaction.RolledBack);
        else Assert.Empty(f.Base.Store.Added);
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("PLPUUser", false)]
    [InlineData("Admin", false)]
    [InlineData("SaleUser", false)]
    public async Task Processing_RequiresLabCapability(string role, bool allowed)
    {
        var f = new Fixture();
        f.Base.User.Roles = [role];
        var result = await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId, new()), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed, f.Base.Store.Transaction.Committed);
        Assert.Equal(allowed ? "FormulaSuccess" : "FormulaRequested", f.Base.Order.Status);
    }

    [Fact]
    public async Task Processing_ActivatesPendingWithoutSchedulingOrReservation_AndOpensStandard()
    {
        var f = new Fixture();
        var previous = new ProductionSelectVersion { CompanyId = f.Base.Order.CompanyId,
            MfgProductionOrderId = f.Base.Order.MfgProductionOrderId, ManufacturingFormulaId = Guid.NewGuid(), ValidFrom = Fixture.Now.AddDays(-1) };
        f.Base.Store.Seed(nameof(f.Base.Db.ProductionSelectVersions), f.Selection, previous);
        var result = await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId,
            new() { IsStandard = true, StandardChangeReason = "Lab decision" }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(Fixture.Now, f.Selection.ValidFrom);
        Assert.Equal(Fixture.Now, previous.ValidTo);
        Assert.Equal(f.Base.User.EmployeeId, previous.ClosedBy);
        Assert.Equal("Checking", f.Formula.Status);
        var standard = Assert.Single(f.Base.Store.Added.OfType<ProductStandardFormula>());
        Assert.Equal(f.Base.Order.ProductId, standard.ProductId);
        Assert.Equal("Lab decision", standard.Note);
        Assert.DoesNotContain(f.Base.Store.Added, x => x is SchedualMfg);
        Assert.DoesNotContain(f.Base.Store.Added, x => x is HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock);
    }

    [Theory]
    [InlineData("unrelated")]
    [InlineData("company")]
    [InlineData("inactive")]
    public async Task Processing_RejectsUnrelatedOrUnavailableFormula(string reason)
    {
        var f = new Fixture();
        if (reason == "unrelated") f.Selection.MfgProductionOrderId = Guid.NewGuid();
        if (reason == "company") f.Formula.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Formula.IsActive = false;
        Assert.False((await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId, new()), default)).Success);
        Assert.Equal("FormulaRequested", f.Base.Order.Status);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.True(f.Base.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task Processing_ItemsSnapshotBeforeEdit_AndClosePriorVersion()
    {
        var f = new Fixture();
        var prior = new ManufacturingFormulaVersion { VersionNo = 3, EffectiveFrom = Fixture.Now.AddDays(-1) };
        f.Formula.ManufacturingFormulaVersions.Add(prior);
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var result = await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId,
            new() { Items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
                ItemId = row.MaterialId!.Value, ItemType = row.itemType, Quantity = 1 }] }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(Fixture.Now, prior.EffectiveTo);
        var snapshot = Assert.Single(f.Formula.ManufacturingFormulaVersions, x => x.VersionNo == 4);
        Assert.Equal("historical name", Assert.Single(snapshot.Items).MaterialNameSnapshot);
        Assert.Equal("current name", row.MaterialNameSnapshot);
        Assert.Equal("F004", f.Formula.Name);
        Assert.Equal("N/A", row.LotNo);
    }

    [Theory]
    [InlineData("FormulaSuccess", "Scheduling", true)]
    [InlineData("FormulaRequested", "FormulaRequested", false)]
    [InlineData("Stocked", "Stocked", false)]
    [InlineData("Finished", "Finished", true)]
    public async Task Inform_HeaderOnlyPreservesLegacyScheduleRulesWithoutReservation(string before, string after, bool schedule)
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.Order.Status = before;
        var created = f.Base.Order.CreatedDate;
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest()), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(after, f.Base.Order.Status);
        if (after == "Scheduling") Assert.Equal(Fixture.Now, f.Selection.ValidFrom);
        else Assert.Null(f.Selection.ValidFrom);
        Assert.Equal(created, f.Base.Order.CreatedDate);
        Assert.Equal(schedule ? 1 : 0, f.Base.Store.Added.OfType<SchedualMfg>().Count());
        Assert.DoesNotContain(f.Base.Store.Added, x => x is HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock);
        Assert.Equal(before == "FormulaSuccess" ? 1 : 0, f.Events.Events.Count);
        Assert.Equal("Approved", f.Base.Sale.Status);
    }

    [Fact]
    public async Task Inform_SelectedFormulaMustBeCurrentSelection()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest(Guid.NewGuid(), [])), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("New")]
    [InlineData("FormulaRequested")]
    [InlineData("Finished")]
    [InlineData("Canceled")]
    [InlineData("Done")]
    [InlineData("Stocked")]
    public async Task EditorSave_HeaderOnlyPreservesIncompletePlanAndCurrentStatus(string status)
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.Order.Status = status;
        f.Base.Order.TotalQuantity = null;
        f.Base.Order.NumOfBatches = null;
        f.Base.Order.StepOfProduct = (StepOfProduct)99;
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        { Action = "Save", LabNote = "Updated note" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var revision = await HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders.ManufacturingEditorRevision.ReadAsync(f.Base.Db, f.Base.Order, default);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch, ExpectedRevision = revision }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(status, f.Base.Order.Status);
        Assert.Null(f.Base.Order.TotalQuantity);
        Assert.Null(f.Base.Order.NumOfBatches);
        Assert.Equal((StepOfProduct)99, f.Base.Order.StepOfProduct);
        Assert.Equal("Updated note", f.Base.Order.LabNote);
    }

    [Fact]
    public async Task Inform_RejectsMaterialMutationWithoutSeparateCapability()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.User.HasExplicitPermissionSet = true;
        f.Base.User.Permissions = [ApplicationPermissions.PLM.ManageProductionOrders, ApplicationPermissions.PLM.ViewProductionOrders];
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest(f.Formula.ManufacturingFormulaId,
            [new() { SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = row.MaterialId!.Value, ItemType = row.itemType, Quantity = 1 }])), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Inform_FullFormulaReservesAndDoesNotCreateSnapshot()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser, ApplicationRoles.Lab.LabUser];
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest(f.Formula.ManufacturingFormulaId,
            [new() { SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = row.MaterialId!.Value, ItemType = row.itemType, Quantity = 1 }])), default);
        Assert.True(result.Success, result.Message);
        Assert.Empty(f.Formula.ManufacturingFormulaVersions);
        Assert.Single(f.Base.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>());
    }

    [Fact]
    public async Task Copy_PreservesExplicitlyConfirmedLegacyDefaultsAndSource()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.Order.TotalQuantityRequest = 300;
        f.Base.Order.UnitPriceAgreed = 500;
        f.Base.Order.CustomerId = Guid.NewGuid();
        f.Base.Order.RequiredDate = Fixture.Now;
        var result = await f.Copy().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest()), default);
        Assert.True(result.Success, result.Message);
        var copy = Assert.Single(f.Base.Store.Added.OfType<MfgProductionOrder>());
        Assert.Equal(0, copy.TotalQuantityRequest);
        Assert.Equal(0, copy.UnitPriceAgreed);
        Assert.Null(copy.CustomerId);
        Assert.Equal("Scheduling", copy.Status);
        Assert.Equal(100, copy.TotalQuantity);
        Assert.Equal(300, f.Base.Order.TotalQuantityRequest);
        Assert.Equal("FormulaRequested", f.Base.Order.Status);
        Assert.Equal("Approved", f.Base.Sale.Status);
        Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>());
        Assert.Single(f.Base.Store.Added.OfType<SchedualMfg>());
        Assert.Single(f.Base.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>());
        Assert.True(f.Base.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("selection")]
    [InlineData("product")]
    [InlineData("role")]
    public async Task Copy_RejectsInvalidSourceBeforeWrites(string reason)
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        if (reason == "company") f.Base.Order.CompanyId = Guid.NewGuid();
        if (reason == "product") f.Base.Detail.ProductId = Guid.NewGuid();
        if (reason == "role") f.Base.User.Roles = [ApplicationRoles.Lab.LabUser];
        var request = f.InformRequest(reason == "selection" ? Guid.NewGuid() : null);
        Assert.False((await f.Copy().Handle(new(f.Base.Order.MfgProductionOrderId, request), default)).Success);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Copy_MaterialDraftRequiresWriteCapabilityBeforeAnyWrites()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.User.HasExplicitPermissionSet = true;
        f.Base.User.Permissions = [ApplicationPermissions.PLM.CreateProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
        var result = await f.Copy().Handle(new(f.Base.Order.MfgProductionOrderId,
            f.InformRequest(f.Formula.ManufacturingFormulaId, [])), default);
        Assert.False(result.Success);
        Assert.Contains("quyền", result.Message);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Store.Added);
    }

    [Fact]
    public async Task Copy_MaterialDraftCreatesNewRowsAndLeavesSourceUntouched()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        var sourceRow = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var oldQuantity = sourceRow.Quantity;
        var oldFormula = f.Formula.ManufacturingFormulaId;
        var result = await f.Copy().Handle(new(f.Base.Order.MfgProductionOrderId,
            f.InformRequest(oldFormula, [new() { SourceItemId = sourceRow.ManufacturingFormulaMaterialId,
                ItemId = sourceRow.MaterialId!.Value, ItemType = sourceRow.itemType, Quantity = 1m, LotNo = sourceRow.LotNo }])), default);
        Assert.True(result.Success, result.Message);
        var created = Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>());
        Assert.NotEqual(oldFormula, created.ManufacturingFormulaId);
        Assert.Equal(1m, Assert.Single(created.ManufacturingFormulaMaterials).Quantity);
        Assert.Equal(oldQuantity, sourceRow.Quantity);
        Assert.True(f.Base.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("FormulaRequested")]
    [InlineData("FormulaSuccess")]
    [InlineData("Scheduled")]
    public async Task LabSave_NotesPreservePlanAndCompleteRequestedFormula(string status)
    {
        var f = new Fixture();
        f.Base.Order.Status = status;
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        { Action = "Save", QcCheck = "2" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(status == "FormulaRequested" ? "FormulaSuccess" : status, f.Base.Order.Status);
        Assert.Null(f.Base.Order.TotalQuantity);
        Assert.Null(f.Base.Order.NumOfBatches);
        Assert.Equal("2", f.Base.Order.QcCheck);
        Assert.Empty(f.Base.Store.Added.OfType<SchedualMfg>());
    }

    [Fact]
    public async Task LabSave_CanReplaceMaterialsAndStdWithPlanUnchanged()
    {
        var f = new Fixture();
        f.Base.Order.TotalQuantity = 100;
        f.Base.Order.NumOfBatches = 2;
        f.Base.Order.StepOfProduct = StepOfProduct.Forsb;
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var added = new Material { MaterialId = Guid.NewGuid(), ExternalId = "NEW-MATERIAL", Name = "New material",
            CompanyId = f.Base.Order.CompanyId, IsActive = true, CategoryId = row.CategoryId, Unit = "kg" };
        f.Base.Store.Seed(nameof(f.Base.Db.Materials), f.Base.Material, added);
        f.Prices.Data[new HRM.Application.Commons.Pricing.Models.PriceItemKey(ItemType.Material, added.MaterialId)] = new() { CurrentPrice = 10, PriceDate = Fixture.Now };
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId,
            f.InformRequest(f.Formula.ManufacturingFormulaId,
                [new() { SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = row.MaterialId!.Value, ItemType = row.itemType, Quantity = .4m },
                 new() { ItemId = added.MaterialId, ItemType = ItemType.Material, Quantity = .6m }])), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("FormulaRequested", f.Base.Order.Status);
        Assert.Contains("[[AUTO_PLPU_NOTE]]", f.Base.Order.PlpuNote);
        Assert.Contains(f.Formula.ManufacturingFormulaMaterials, x => x.MaterialId == added.MaterialId && x.Quantity == .6m);
        Assert.Contains(f.Formula.ManufacturingFormulaMaterials, x => x.MaterialId == row.MaterialId && x.Quantity == .4m);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LabSave_FormulaNoteTargetsSelectedVaAndPreservesOrderNotes(bool clear, bool withItems)
    {
        var f = new Fixture();
        f.Base.User.Roles = ["LabUser"];
        f.Base.Order.Status = "FormulaRequested";
        f.Formula.Note = "Old VA note";
        f.Base.Order.LabNote = "Order QA";
        f.Base.Order.PlpuNote = "Order PLPU";
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        {
            Action = "Save", SelectedFormulaId = f.Formula.ManufacturingFormulaId,
            FormulaNote = clear ? null : "VA note\nSecond line",
            ClearFields = clear ? ["formulaNote"] : [],
            Items = withItems ? [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
                ItemId = row.MaterialId!.Value, ItemType = row.itemType, Quantity = 1m, LotNo = row.LotNo }] : null
        };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(clear ? null : "VA note\nSecond line", f.Formula.Note);
        Assert.Equal("Order QA", f.Base.Order.LabNote);
        if (!withItems) Assert.Equal("Order PLPU", f.Base.Order.PlpuNote);
        Assert.Equal("FormulaSuccess", f.Base.Order.Status);
        Assert.Equal("Checking", f.Formula.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LabSave_CompletesOnceWithoutSchedulingThenPlpuSchedules(bool alsoManager)
    {
        var f = new Fixture();
        if (alsoManager) f.Base.User.Roles = ["LabUser", "PLPUUser"];
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        row.ManufacturingFormulaId = f.Formula.ManufacturingFormulaId;
        row.ManufacturingFormula = f.Formula;
        row.Material = f.Base.Material;
        row.MaterialExternalIdSnapshot = f.Base.Material.ExternalId;
        f.Base.Order.TotalQuantity = 100;
        f.Base.Order.NumOfBatches = 2;
        f.Base.Order.StepOfProduct = StepOfProduct.Forsb;
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest { Action = "Save" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var command = new UpdateProductionOrderInformCommand(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch };
        var result = await f.Inform().Handle(command, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("FormulaSuccess", f.Base.Order.Status);
        Assert.Equal("Checking", f.Formula.Status);
        Assert.Single(f.Events.Events);
        Assert.Equal(Fixture.Now, f.Selection.ValidFrom);
        Assert.Empty(f.Base.Store.Added.OfType<SchedualMfg>());
        Assert.True(f.Base.Store.Transaction.Committed);

        f.Base.User.Roles = ["LabUser"];
        Assert.True((await f.Inform().Handle(command, default)).Success);
        Assert.Equal("FormulaSuccess", f.Base.Order.Status);
        Assert.Single(f.Events.Events);
        Assert.Empty(f.Base.Store.Added.OfType<SchedualMfg>());

        f.Base.User.Roles = ["PLPUUser"];
        result = await f.Inform().Handle(command, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("Scheduling", f.Base.Order.Status);
        Assert.Single(f.Base.Store.Added.OfType<SchedualMfg>());
        Assert.Equal(2, f.Events.Events.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("company")]
    [InlineData("inactive")]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("negative")]
    public async Task LabSave_RejectsCompletionWithoutValidSelectedFormula(string reason)
    {
        var f = new Fixture();
        if (reason == "missing") f.Selection.ValidTo = Fixture.Now;
        if (reason == "company") f.Formula.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Formula.IsActive = false;
        var row = Assert.Single(f.Formula.ManufacturingFormulaMaterials);
        if (reason == "empty") row.IsActive = false;
        if (reason == "zero") row.Quantity = 0;
        if (reason == "negative") row.Quantity = -1;
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest { Action = "Save" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.False(result.Success);
        Assert.Equal("FormulaRequested", f.Base.Order.Status);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Events.Events);
    }

    [Theory]
    [InlineData("material")]
    [InlineData("formula")]
    [InlineData("revision")]
    public async Task LabSave_DoesNotCompleteWithoutCapabilitiesOrCurrentRevision(string missing)
    {
        var f = new Fixture();
        f.Base.User.HasExplicitPermissionSet = true;
        f.Base.User.Permissions = new[]
        {
            ApplicationPermissions.PLM.ViewProductionOrders,
            ApplicationPermissions.PLM.ManageProductionOrders,
            ApplicationPermissions.PLM.ManageManufacturingFormulas,
            ApplicationPermissions.PLM.ViewFormulaMaterials
        }.Where(x => x != (missing == "material" ? ApplicationPermissions.PLM.ViewFormulaMaterials
            : missing == "formula" ? ApplicationPermissions.PLM.ManageManufacturingFormulas : string.Empty)).ToArray();
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest { Action = "Save" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch, ExpectedRevision = missing == "revision" ? new string('0', 64) : null }, default);
        if (missing == "revision") Assert.False(result.Success);
        Assert.Equal("FormulaRequested", f.Base.Order.Status);
        Assert.Equal("New", f.Formula.Status);
        Assert.Empty(f.Events.Events);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("selection")]
    [InlineData("length")]
    public async Task LabSave_FormulaNoteRejectsInvalidScopeOrCapability(string reason)
    {
        var f = new Fixture();
        f.Base.User.Roles = ["LabUser"];
        f.Formula.Note = "Original";
        if (reason == "permission") { f.Base.User.HasExplicitPermissionSet = true;
            f.Base.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ManageProductionOrders]; }
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        { Action = "Save", SelectedFormulaId = reason == "selection" ? Guid.NewGuid() : f.Formula.ManufacturingFormulaId,
          FormulaNote = reason == "length" ? new string('x', 20001) : "Changed" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.False(result.Success);
        Assert.Equal("Original", f.Formula.Note);
    }

    [Fact]
    public async Task LabSave_DirectWriterRejectsChangedPlan()
    {
        var f = new Fixture();
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest()), default);
        Assert.False(result.Success);
        Assert.Contains("quyền", result.Message);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task LabSave_DirectWriterRejectsRequirementChange()
    {
        var f = new Fixture();
        f.Base.Order.Requirement = "Customer baseline";
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        { Action = "Save", Requirement = "Not allowed" };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.False(result.Success);
        Assert.Equal("Customer baseline", f.Base.Order.Requirement);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LabSave_PreservesPlpuNoteAndRejectsDirectChanges(bool change)
    {
        var f = new Fixture();
        f.Base.Order.PlpuNote = "stored PLPU";
        var patch = new HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest
        { Action = "Save", QcCheck = "QA", PlpuNote = change ? "forbidden" : null };
        var request = HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorMapping.Merge(f.Base.Order, patch);
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, request)
        { IsEditorSave = true, EditorPatch = patch }, default);
        Assert.Equal(!change, result.Success);
        Assert.Equal("stored PLPU", f.Base.Order.PlpuNote);
        Assert.Equal(change ? 0 : 1, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("ambiguous")]
    public async Task InformScheduling_CannotCommitInvalidSelection(string reason)
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        f.Base.Order.Status = "FormulaSuccess";
        if (reason == "foreign") f.Formula.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Formula.IsActive = false;
        if (reason == "ambiguous") f.Base.Store.Seed(nameof(f.Base.Db.ProductionSelectVersions), f.Selection,
            new ProductionSelectVersion { CompanyId = f.Base.Order.CompanyId, MfgProductionOrderId = f.Base.Order.MfgProductionOrderId,
                ManufacturingFormulaId = f.Formula.ManufacturingFormulaId, ManufacturingFormula = f.Formula });
        var result = await f.Inform().Handle(new(f.Base.Order.MfgProductionOrderId, f.InformRequest()), default);
        Assert.False(result.Success);
        Assert.False(f.Base.Store.Transaction.Committed);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Null(f.Selection.ValidFrom);
    }

    [Theory]
    [InlineData(null, false, "Source instruction")]
    [InlineData("", false, "Source instruction")]
    [InlineData("Updated instruction", false, "Updated instruction")]
    [InlineData(null, true, null)]
    public async Task QaCreateVa_KeepsSourceInstructionUnlessExplicitlyChangedOrCleared(string? note, bool clear, string? expected)
    {
        var f = new Fixture();
        f.Formula.Note = "Source instruction";
        f.Base.Order.LabNote = "Plan QA request";
        f.Base.Order.QcCheck = "QC request";
        var row = f.Base.Db.ManufacturingFormulaMaterials.Single();
        var result = await f.LabCreate().Handle(new(f.Base.Order.MfgProductionOrderId, new() {
            SourceFormulaId = f.Formula.ManufacturingFormulaId, SourceType = FormulaSource.FromVA,
            Note = note, ClearNote = clear, Items = [new() { SourceItemId = row.ManufacturingFormulaMaterialId,
                ItemId = row.MaterialId!.Value, ItemType = ItemType.Material, Quantity = 1 }]
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormula>()).Note);
        Assert.Equal("Plan QA request", f.Base.Order.LabNote);
        Assert.Equal("QC request", f.Base.Order.QcCheck);
        Assert.Equal("Source instruction", f.Formula.Note);
    }

    [Theory]
    [InlineData(false, "Source instruction")]
    [InlineData(true, null)]
    public async Task QaProcessVa_EmptyNotePreservesUnlessClearFlag(bool clear, string? expected)
    {
        var f = new Fixture();
        f.Formula.Note = "Source instruction";
        var result = await f.Process().Handle(new(f.Base.Order.MfgProductionOrderId, f.Formula.ManufacturingFormulaId,
            new() { Note = "", ClearNote = clear }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, f.Formula.Note);
    }
    private sealed class Fixture
    {
        public static readonly DateTime Now = ManufacturingFormulaCreationTests.Fixture.Now;
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.Production);
        public ManufacturingFormula Formula { get; }
        public ProductionSelectVersion Selection { get; }
        public EventsWriter Events { get; } = new();
        public ManufacturingFormulaReadTests.PriceProvider Prices { get; }
        public Fixture()
        {
            Prices = (ManufacturingFormulaReadTests.PriceProvider)(object)DispatchProxy.Create<IMaterialPriceQueryService, ManufacturingFormulaReadTests.PriceProvider>();
            Base.User.Roles = [ApplicationRoles.Lab.LabUser];
            Base.Order.Status = "FormulaRequested";
            Formula = Base.Db.ManufacturingFormulas.Single();
            Formula.Status = "New";
            var row = Base.Db.ManufacturingFormulaMaterials.Single();
            row.MaterialNameSnapshot = "historical name";
            Formula.ManufacturingFormulaMaterials.Add(row);
            Base.Store.Seed(nameof(Base.Db.MfgProductionOrders), Base.Order);
            Selection = new() { CompanyId = Base.Order.CompanyId, MfgProductionOrderId = Base.Order.MfgProductionOrderId,
                ManufacturingFormulaId = Formula.ManufacturingFormulaId, ManufacturingFormula = Formula, MfgProductionOrder = Base.Order };
            Base.Store.Seed(nameof(Base.Db.ProductionSelectVersions), Selection);
        }
        private ManufacturingFormulaInputResolver Resolver() => new(Base.Db, (IMaterialPriceQueryService)(object)Prices, new Clock(), new CurrentUserPermissionService(Base.User));
        private ManufacturingWorkflowNotifier Notifier() => new((INotificationService)Base.Notifications);
        public ProcessManufacturingFormulaCommandHandler Process() => new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), new Clock(), Events, Resolver(), Notifier());
        public CreateLabManufacturingFormulaCommandHandler LabCreate() => new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), new Clock(), new Codes(), Events, Resolver(), Notifier());
        public UpdateProductionOrderInformCommandHandler Inform() => new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), new Clock(), Events,
            Resolver(), new ProductionOrderReservationService(Base.Db), Notifier(), PromisedDeliveryTestFactory.Create(Base.Db, Base.User).Notifier);
        public CopyProductionOrderCommandHandler Copy() => new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), new Codes(), new Clock(),
            Events, Resolver(), new ProductionOrderReservationService(Base.Db));
        public ProductionOrderInformRequest InformRequest(Guid? selected = null, IReadOnlyList<ManufacturingFormulaInputItem>? items = null)
            => new() { TotalQuantity = 100, NumOfBatches = 2, StepOfProduct = Enum.GetValues<StepOfProduct>()[0],
                ExpectedDate = null, ManufacturingDate = null, SelectedFormulaId = selected, Items = items };
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => Fixture.Now; }
    private sealed class Codes : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => Task.FromResult(prefix + "COPY");
    }
    private sealed class EventsWriter : IEventLogWriter
    {
        public List<EventLogCreateRequest> Events { get; } = [];
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) { Events.Add(request); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) { Events.AddRange(requests); return Task.CompletedTask; }
    }
}
