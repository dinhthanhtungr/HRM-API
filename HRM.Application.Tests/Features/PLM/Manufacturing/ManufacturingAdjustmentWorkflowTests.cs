using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed partial class ManufacturingAdjustmentWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedPdf_UsesCurrentMaterialLabelsOnlyWithinCompany(bool foreign)
    {
        var f = new Fixture();
        var row = new ManufacturingFormulaAdjustmentItem { MaterialId = f.Base.Material.MaterialId,
            itemType = ItemType.Material, MaterialExternalIdSnapshot = "OLD", MaterialNameSnapshot = "Old name",
            AdjustedQuantity = 100, DeltaQuantity = 100, Unit = "g" };
        var entity = f.Existing(new ManufacturingFormulaAdjustmentBatch { BatchNo = 1, TrialNo = 1, BatchQuantity = 50000,
            AdjustmentType = AdjustmentType.AddQuantity, Items = [row] });
        f.Base.Material.ExternalId = "CURRENT";
        f.Base.Material.Name = "Current name";
        if (foreign) f.Base.Material.CompanyId = Guid.NewGuid();
        var renderer = new PreviewRenderer();
        var service = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, renderer);
        var result = await service.PrintAsync(entity.ManufacturingFormulaAdjustmentId, null, default);
        Assert.True(result.Success, result.Message);
        var printed = Assert.Single(Assert.Single(renderer.Document!.Batches).Items);
        Assert.Equal(foreign ? "OLD" : "CURRENT", printed.Code);
        Assert.Equal(foreign ? "Old name" : "Current name", printed.Name);
        Assert.Equal("OLD", row.MaterialExternalIdSnapshot);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task SavedPdf_UsesCurrentProductColourCodeButKeepsSavedName()
    {
        var f = new Fixture();
        var product = f.Base.Db.Products.First();
        product.ColourCode = "NEW-COLOUR";
        var row = new ManufacturingFormulaAdjustmentItem { ProductId = product.ProductId,
            itemType = ItemType.Product, ProductExternalIdSnapshot = "OLD-COLOUR", ProductNameSnapshot = "Saved product name",
            AdjustedQuantity = 100, DeltaQuantity = 100, Unit = "g" };
        var entity = f.Existing(new ManufacturingFormulaAdjustmentBatch { BatchNo = 1, TrialNo = 1, BatchQuantity = 50000,
            AdjustmentType = AdjustmentType.AddQuantity, Items = [row] });
        var renderer = new PreviewRenderer();
        var service = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, renderer);
        var result = await service.PrintAsync(entity.ManufacturingFormulaAdjustmentId, null, default);
        Assert.True(result.Success, result.Message);
        var printed = Assert.Single(Assert.Single(renderer.Document!.Batches).Items);
        Assert.Equal("NEW-COLOUR", printed.Code);
        Assert.Equal("Saved product name", printed.Name);
        Assert.Equal("OLD-COLOUR", row.ProductExternalIdSnapshot);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("foreign-order")]
    [InlineData("unrelated-va")]
    [InlineData("empty-pdf")]
    public async Task Preview_ComputesServerTotalsWithoutSavingOrPublishing(string reason)
    {
        var f = new Fixture();
        var renderer = new PreviewRenderer { Empty = reason == "empty-pdf" };
        var document = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, renderer);
        if (reason == "foreign-order") f.Base.Order.CompanyId = Guid.NewGuid();
        var result = await document.PreviewAsync(f.Base.Order.MfgProductionOrderId, new() {
            ManufacturingFormulaId = reason == "unrelated-va" ? Guid.NewGuid() : null,
            Ranges = [f.Range(1, AdjustmentType.AddQuantity, 200)] }, default);
        Assert.Equal(reason == "valid", result.Success);
        if (reason == "valid") Assert.Equal(50200m, Assert.Single(renderer.Document!.Batches).FinalQuantityGram);
        Assert.Empty(f.Base.Store.Added);
        Assert.Empty(f.Base.Store.Removed);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Notifications.Requests);
        Assert.False(f.Base.Store.Transaction.Committed);
    }

    private sealed class PreviewRenderer : HRM.Application.Abstractions.Documents.IManufacturingAdjustmentPdfRenderer
    {
        public bool Empty;
        public HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments.PdfMfgFormulaAdjustment? Document;
        public byte[] Render(HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments.PdfMfgFormulaAdjustment document)
        { Document = document; return Empty ? [] : [1]; }
    }

    [Fact]
    public async Task Create_DerivesOriginalBaseAndGramAmountsFromFormulaAndPlan()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(1, AdjustmentType.AddQuantity, 250)] }, default);
        Assert.True(result.Success, result.Message);
        var entity = Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormulaAdjustment>());
        var batch = Assert.Single(entity.Batches);
        var row = Assert.Single(batch.Items);
        Assert.Equal(50000m, row.BaseQuantity);
        Assert.Equal(50250m, row.AdjustedQuantity);
        Assert.Equal(250m, row.DeltaQuantity);
        Assert.Equal("g", row.Unit);
        Assert.Equal(50250m, batch.FinalQuantityGram);
        Assert.Equal("current name", row.MaterialNameSnapshot);
        Assert.True(f.Base.Store.Transaction.Committed);
    }

    [Fact]
    public async Task AdditionalBatch_SetUsesZeroBaseAndDoesNotSubtractTakenQuantity()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(3, AdjustmentType.SetQuantity, 10000)] }, default);
        Assert.True(result.Success, result.Message);
        var batch = Assert.Single(Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormulaAdjustment>()).Batches);
        Assert.True(batch.IsAdditionalBatch);
        Assert.Null(batch.TakenQuantityGram);
        Assert.Equal(0m, Assert.Single(batch.Items).BaseQuantity);
        Assert.Equal(10000m, batch.BatchQuantity);
        Assert.Equal(10000m, batch.FinalQuantityGram);
    }

    [Fact]
    public async Task SeveralWeighingsAccumulate_ThenColourDeltaAddsToAllWeighings()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(3, AdjustmentType.SetQuantity, 10000), f.Range(3, AdjustmentType.SetQuantity, 5000),
                f.Range(3, AdjustmentType.AddQuantity, -100)] }, default);
        Assert.True(result.Success, result.Message);
        var batches = Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormulaAdjustment>()).Batches.OrderBy(x => x.TrialNo).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, batches.Select(x => x.TrialNo));
        Assert.Equal(15000m, batches[1].FinalQuantityGram);
        Assert.Equal(15000m, Assert.Single(batches[2].Items).BaseQuantity);
        Assert.Equal(14900m, batches[2].FinalQuantityGram);
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("missing-first")]
    [InlineData("negative-set")]
    [InlineData("nonpositive-final")]
    [InlineData("foreign-material")]
    [InlineData("foreign-order")]
    [InlineData("unrelated-formula")]
    public async Task InvalidInputOrScope_DoesNotPersist(string reason)
    {
        var f = new Fixture();
        var range = f.Range(1, AdjustmentType.AddQuantity, 100);
        Guid? formula = null;
        if (reason == "mixed") range = f.Range(2, AdjustmentType.AddQuantity, 100, to: 3);
        if (reason == "missing-first") range = f.Range(3, AdjustmentType.AddQuantity, 100);
        if (reason == "negative-set") range = f.Range(1, AdjustmentType.SetQuantity, -1);
        if (reason == "nonpositive-final") range = f.Range(1, AdjustmentType.AddQuantity, -50000);
        if (reason == "foreign-material") f.Base.Material.CompanyId = Guid.NewGuid();
        if (reason == "foreign-order") f.Base.Order.CompanyId = Guid.NewGuid();
        if (reason == "unrelated-formula") formula = Guid.NewGuid();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new() { ManufacturingFormulaId = formula, Ranges = [range] }, default);
        Assert.False(result.Success);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.True(f.Base.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task MissingCapabilityOrMaterialVisibility_RejectsBeforeMutation()
    {
        var f = new Fixture();
        f.Base.User.HasExplicitPermissionSet = true;
        f.Base.User.Permissions = [ApplicationPermissions.PLM.ManageManufacturingAdjustments];
        Assert.False((await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new(), default)).Success);
        f.Base.User.Permissions = [ApplicationPermissions.PLM.ViewFormulaMaterials];
        Assert.False((await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new(), default)).Success);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task DeleteFirstAdditionalWeighing_IsRejectedWhenAnotherTrialRemains()
    {
        var f = new Fixture();
        var first = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 3, TrialNo = 1,
            AdjustmentType = AdjustmentType.SetQuantity, IsAdditionalBatch = true };
        var later = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 3, TrialNo = 2,
            AdjustmentType = AdjustmentType.AddQuantity, IsAdditionalBatch = true };
        var entity = f.Existing(first, later);
        var result = await f.Service.DeleteAsync(entity.ManufacturingFormulaAdjustmentId, first.ManufacturingFormulaAdjustmentBatchId, default);
        Assert.False(result.Success);
        Assert.True(first.IsActive);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Delete_RequiresBatchOwnershipAndUsesSoftDelete()
    {
        var f = new Fixture();
        var entity = f.Existing();
        Assert.True((await f.Service.DeleteAsync(entity.ManufacturingFormulaAdjustmentId, null, default)).Success);
        Assert.False(entity.IsActive);
        Assert.Equal(f.Base.User.EmployeeId, entity.UpdatedBy);
    }

    [Fact]
    public async Task SaveFailure_RequestsRollback()
    {
        var f = new Fixture();
        f.Base.Store.ThrowOnSave = true;
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(1, AdjustmentType.SetQuantity, 1000)] }, default);
        Assert.False(result.Success);
        Assert.True(f.Base.Store.Transaction.RolledBack);
        Assert.False(f.Base.Store.Transaction.Committed);
    }

    [Fact]
    public async Task RangePatch_PreservesRetainedBatchIdAndHistoricalBase_AfterPlanChanges()
    {
        var f = new Fixture();
        var row = new ManufacturingFormulaAdjustmentItem { ManufacturingFormulaAdjustmentItemId = Guid.NewGuid(), MaterialId = f.Base.Material.MaterialId,
            itemType = ItemType.Material, BaseQuantity = 50000, AdjustedQuantity = 50100, DeltaQuantity = 100, Unit = "g" };
        var old = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 1, TrialNo = 1,
            AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000, TakenQuantityGram = 500, Items = [row] };
        var entity = f.Existing(old);
        f.Base.Order.TotalQuantity = 200;
        var result = await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId, old.ManufacturingFormulaAdjustmentBatchId,
            f.Range(1, AdjustmentType.AddQuantity, 200), false, default);
        Assert.True(result.Success, result.Message);
        var patched = Assert.Single(entity.Batches);
        Assert.Same(old, patched);
        Assert.Equal(50000m, Assert.Single(patched.Items).BaseQuantity);
        Assert.Equal(50200m, patched.FinalQuantityGram);
        Assert.Single(f.Base.Store.Removed);
        Assert.True(f.Base.Store.Transaction.Committed);
    }

    [Fact]
    public async Task RangePatch_ShrinksOnlyContiguousMatchingGroup()
    {
        var f = new Fixture();
        var first = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 1, TrialNo = 1,
            AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000, TakenQuantityGram = 500 };
        var second = new ManufacturingFormulaAdjustmentBatch { ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 2, TrialNo = 1,
            AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000, TakenQuantityGram = 500 };
        var entity = f.Existing(first, second);
        var result = await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId, first.ManufacturingFormulaAdjustmentBatchId,
            f.Range(1, AdjustmentType.AddQuantity, 100), false, default);
        Assert.True(result.Success, result.Message);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
        Assert.Equal(2, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Notifications_ResolveBothLegacyGroups_AndExcludeForeignOrInactiveEmployees()
    {
        var f = new Fixture();
        var own = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId, IsActive = true };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var inactive = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId, IsActive = false };
        f.Base.Store.Seed(nameof(f.Base.Db.MemberInGroups),
            new MemberInGroup { GroupId = ManufacturingAdjustmentRecipientGroups.All[0], Profile = own.EmployeeId, ProfileNavigation = own },
            new MemberInGroup { GroupId = ManufacturingAdjustmentRecipientGroups.All[1], Profile = own.EmployeeId, ProfileNavigation = own },
            new MemberInGroup { GroupId = ManufacturingAdjustmentRecipientGroups.All[0], Profile = foreign.EmployeeId, ProfileNavigation = foreign },
            new MemberInGroup { GroupId = ManufacturingAdjustmentRecipientGroups.All[0], Profile = inactive.EmployeeId, ProfileNavigation = inactive });
        Assert.True((await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new(), default)).Success);
        var notification = Assert.Single(f.Base.Notifications.Requests);
        Assert.Equal(own.EmployeeId, Assert.Single(notification.TargetUserIds!));
        using var payload = System.Text.Json.JsonDocument.Parse(notification.PayloadJson!);
        Assert.Equal(f.Base.Order.MfgProductionOrderId, payload.RootElement.GetProperty("mfgProductionOrderId").GetGuid());
        Assert.Equal(0, payload.RootElement.GetProperty("batchCount").GetInt32());
        Assert.False(payload.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Notifications_StillPublishWithEmptyGroups_ForCommonRecipientPolicy()
    {
        var f = new Fixture();
        Assert.True((await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new(), default)).Success);
        var notification = Assert.Single(f.Base.Notifications.Requests);
        Assert.Empty(notification.TargetUserIds!);
        Assert.Equal(HRM.Domain.Enums.Notifications.TopicNotifications.ManufacturingFormulaAdjustmentCreated, notification.Topic);
    }

    [Fact]
    public async Task AddQuantity_WithoutTakenQuantity_IsRejectedBeforeSaving()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(1, AdjustmentType.AddQuantity, 250, taken: false)] }, default);
        Assert.False(result.Success);
        Assert.Contains("lấy ra", result.Message);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task WeighingRange_CannotCrossPlannedBatchBoundary()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(2, AdjustmentType.SetQuantity, 10000, to: 3)] }, default);
        Assert.False(result.Success);
        Assert.Contains("tách", result.Message);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("")]
    public async Task DefectiveMaterialWithoutWarehouseLot_PreservesQuality(string lot)
    {
        var f = new Fixture();
        var range = new ManufacturingAdjustmentRangeInput
        {
            BatchFrom = 1, BatchTo = 1, AdjustmentType = AdjustmentType.AddQuantity,
            TakenQuantityGram = 500,
            Items = [new() { ItemId = f.Base.Material.MaterialId,
                ItemType = ItemType.MaterialFailure, QuantityGram = 100, LotNo = lot }]
        };
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [range] }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(ItemType.MaterialFailure, Assert.Single(Assert.Single(result.Data!.Batches).Items).itemType);
    }

    [Fact]
    public async Task SameMaterialWithNaLot_KeepsGoodAndDefectiveRowsSeparate()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, new()
        {
            Ranges = [new() { BatchFrom = 1, BatchTo = 1, TakenQuantityGram = 500,
                Items = [new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.Material, QuantityGram = 10, LotNo = "N/A" },
                    new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.MaterialFailure, QuantityGram = 20, LotNo = "N/A" }] }]
        }, default);
        Assert.True(result.Success, result.Message);
        var rows = Assert.Single(result.Data!.Batches).Items;
        Assert.Equal(new[] { ItemType.Material, ItemType.MaterialFailure }, rows.Select(x => x.itemType));
    }

    [Fact]
    public async Task Preview_UsesRequestedDatesPerRange_WithoutPersistingThem()
    {
        var f = new Fixture();
        var renderer = new PreviewRenderer();
        var document = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, renderer);
        var headerDate = new DateTime(2026, 9, 1);
        var batchDate = headerDate.AddDays(1);
        var result = await document.PreviewAsync(f.Base.Order.MfgProductionOrderId, new()
        {
            PreviewCreatedDate = headerDate,
            Ranges = [new()
            {
                BatchFrom = 1, BatchTo = 2, PreviewCreatedDate = batchDate,
                AdjustmentType = AdjustmentType.SetQuantity,
                Items = [new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.Material, QuantityGram = 1000 }]
            }, f.Range(1, AdjustmentType.SetQuantity, 2000)]
        }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(headerDate, renderer.Document!.CreatedDate);
        Assert.Equal(Guid.Empty, renderer.Document.ManufacturingFormulaAdjustmentId);
        Assert.All(renderer.Document.Batches, batch => Assert.Equal(Guid.Empty, batch.ManufacturingFormulaAdjustmentBatchId));
        Assert.Equal(batchDate, renderer.Document.Batches.Single(x => x.BatchNo == 1 && x.TrialNo == 1).CreatedDate);
        Assert.Equal(headerDate, renderer.Document.Batches.Single(x => x.BatchNo == 1 && x.TrialNo == 2).CreatedDate);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Notifications.Requests);
    }

    [Fact]
    public async Task List_FiltersAcrossOrders_AndKeepsCompanyScope()
    {
        var f = new Fixture();
        var entity = f.Existing(new ManufacturingFormulaAdjustmentBatch { BatchNo = 3, TrialNo = 2, IsAdditionalBatch = true, Status = "Draft" });
        var other = new ManufacturingFormulaAdjustment
        {
            ManufacturingFormulaAdjustmentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            MfgProductionOrderId = f.Base.Order.MfgProductionOrderId, MfgProductionOrder = f.Base.Order,
            Batches = [new() { BatchNo = 3, TrialNo = 2, IsAdditionalBatch = true, Status = "Draft" }]
        };
        f.Base.Store.Seed(nameof(f.Base.Db.ManufacturingFormulaAdjustments), entity, other);
        var result = await f.Service.ListAsync(new() { BatchNo = 3, TrialNo = 2, BatchStatus = " draft ", IsAdditionalBatch = true }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(entity.ManufacturingFormulaAdjustmentId, Assert.Single(result.Data!).ManufacturingFormulaAdjustmentId);
        Assert.Empty((await f.Service.ListAsync(new() { TrialNo = 99 }, default)).Data!);
    }

    [Fact]
    public async Task ListInactive_RequiresManageCapability_AndExplicitOptIn()
    {
        var f = new Fixture();
        var entity = f.Existing();
        entity.IsActive = false;
        f.Base.User.HasExplicitPermissionSet = true;
        f.Base.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
        Assert.False((await f.Service.ListAsync(new() { IncludeInactive = true }, default)).Success);
        f.Base.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials,
            ApplicationPermissions.PLM.ManageManufacturingAdjustments];
        Assert.Empty((await f.Service.ListAsync(new(), default)).Data!);
        Assert.Single((await f.Service.ListAsync(new() { IncludeInactive = true }, default)).Data!);
    }

    [Fact]
    public async Task RangePatch_RejectsForeignBatchAndKeepsExistingBatches()
    {
        var f = new Fixture();
        var entity = f.Existing();
        Assert.False((await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId, Guid.NewGuid(), f.Range(1, AdjustmentType.AddQuantity, 1), false, default)).Success);
        Assert.Empty(entity.Batches);
        Assert.Empty(f.Base.Store.Removed);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Plpu_CannotReadWriteInitializeCalculateOrPrintAdjustments()
    {
        var f = new Fixture();
        f.Base.User.Roles = [ApplicationRoles.Production.PLPUUser];
        var entity = f.Existing();
        Assert.False((await f.Service.GetAsync(entity.ManufacturingFormulaAdjustmentId, default)).Success);
        Assert.False((await f.Service.ListAsync(new(), default)).Success);
        Assert.False((await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, null, default)).Success);
        var input = new HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments.ManufacturingAdjustmentInput
        { Ranges = [f.Range(1, AdjustmentType.AddQuantity, 1)] };
        Assert.False((await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, input, default)).Success);
        Assert.False((await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, input, default)).Success);
        var document = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, new PreviewRenderer());
        Assert.False((await document.PrintAsync(entity.ManufacturingFormulaAdjustmentId, null, default)).Success);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    private sealed class Fixture
    {
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.Production);
        public ManufacturingAdjustmentService Service { get; }
        public Fixture()
        {
            Base.User.Roles = [ApplicationRoles.Lab.LabUser];
            Base.Order.TotalQuantity = 100;
            Base.Order.NumOfBatches = 2;
            var formula = Base.Db.ManufacturingFormulas.Single();
            formula.ManufacturingFormulaMaterials.Add(Base.Db.ManufacturingFormulaMaterials.Single());
            Base.Store.Seed(nameof(Base.Db.MfgProductionOrders), Base.Order);
            Base.Store.Seed(nameof(Base.Db.ProductionSelectVersions), new ProductionSelectVersion
            { CompanyId = Base.Order.CompanyId, MfgProductionOrderId = Base.Order.MfgProductionOrderId, ManufacturingFormulaId = formula.ManufacturingFormulaId,
                ManufacturingFormula = formula, MfgProductionOrder = Base.Order, ValidFrom = ManufacturingFormulaCreationTests.Fixture.Now });
            var permissions = new CurrentUserPermissionService(Base.User);
            Service = new(Base.Db, Base.User, permissions, new PLMFieldVisibilityService(permissions), new Clock(),
                (INotificationService)Base.Notifications, new ManufacturingAdjustmentInputResolver(Base.Db));
        }
        public ManufacturingAdjustmentRangeInput Range(int batch, AdjustmentType mode, decimal quantity, int? to = null, bool taken = true)
            => new() { BatchFrom = batch, BatchTo = to ?? batch, AdjustmentType = mode, TakenQuantityGram = taken ? 500 : null,
                Items = [new() { ItemId = Base.Material.MaterialId, ItemType = ItemType.Material, QuantityGram = quantity }] };
        public ManufacturingFormulaAdjustment Existing(params ManufacturingFormulaAdjustmentBatch[] batches)
        {
            var entity = new ManufacturingFormulaAdjustment { ManufacturingFormulaAdjustmentId = Guid.NewGuid(), MfgProductionOrderId = Base.Order.MfgProductionOrderId,
                MfgProductionOrder = Base.Order, CompanyId = Base.Order.CompanyId, ManufacturingFormulaId = Base.SourceId, Batches = batches.ToList() };
            Base.Store.Seed(nameof(Base.Db.ManufacturingFormulaAdjustments), entity);
            return entity;
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => ManufacturingFormulaCreationTests.Fixture.Now; }
}
