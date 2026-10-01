using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaPricingReviewRulesTests
{
    private static readonly Guid MaterialId = Guid.NewGuid();
    private static readonly Guid OtherMaterialId = Guid.NewGuid();

    [Theory]
    [InlineData(FormulaStatus.Draft)]
    [InlineData(FormulaStatus.SampleSent)]
    [InlineData(FormulaStatus.Completed)]
    [InlineData(FormulaStatus.Cancelled)]
    public async Task NonApprovedFormula_DoesNotQueryPricingOrRequestAReview(FormulaStatus status)
    {
        var service = new FormulaPricingReviewService(null!, null!, null!);
        var formula = new Formula { Status = status.ToString() };

        Assert.False(await service.RequiresMaterialReviewAsync(
            formula, new() { Materials = [Item(MaterialId, 1m)] }, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task MissingMaterialsOrProductReassignment_DoesNotRequestAReview()
    {
        var service = new FormulaPricingReviewService(null!, null!, null!);
        var formula = new Formula { Status = FormulaStatus.Approved.ToString(), ProductId = Guid.NewGuid() };

        Assert.False(await service.RequiresMaterialReviewAsync(formula, null, Guid.NewGuid(), default));
        Assert.False(await service.RequiresMaterialReviewAsync(formula, new(), Guid.NewGuid(), default));
        Assert.False(await service.RequiresMaterialReviewAsync(
            formula, new() { ProductId = Guid.NewGuid(), Materials = [Item(MaterialId, 1m)] }, Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void MaterialChange_RequiresReviewForAdditionRemovalReplacementQuantityOrItemType(int change)
    {
        FormulaMaterialCompositionItem[] current = [new(ItemType.Material, MaterialId, 1m)];
        var requested = new List<UpsertFormulaMaterialRequest> { Item(MaterialId, 1m) };
        switch (change)
        {
            case 0: requested.Add(Item(OtherMaterialId, 0.5m)); break;
            case 1: requested.Clear(); break;
            case 2: requested[0] = Item(OtherMaterialId, 1m); break;
            case 3: requested[0] = Item(MaterialId, 1.1m); break;
            case 4: requested[0] = new() { ItemType = ItemType.Product, ItemId = MaterialId, Quantity = 1m }; break;
        }

        Assert.True(FormulaPricingReviewRules.HasMaterialChanges(current, requested));
    }

    [Fact]
    public void Resubmission_ReorderAndClientSnapshotOrPriceChangesDoNotRequireReview()
    {
        FormulaMaterialCompositionItem[] current =
        [new(ItemType.Material, MaterialId, 1m), new(ItemType.Material, OtherMaterialId, 0.5m)];
        UpsertFormulaMaterialRequest[] requested =
        [Item(OtherMaterialId, 0.5m), new()
        {
            ItemType = ItemType.Material, ItemId = MaterialId, Quantity = 1m,
            UnitPrice = 123m, MaterialNameSnapshot = "Updated display name", LineNo = 99
        }];

        Assert.False(FormulaPricingReviewRules.HasMaterialChanges(current, requested));
    }

    [Fact]
    public void QuantityComparison_UsesTheSamePrecisionAsPersistedMaterials()
    {
        FormulaMaterialCompositionItem[] current = [new(ItemType.Material, MaterialId, 1m)];

        Assert.False(FormulaPricingReviewRules.HasMaterialChanges(current, [Item(MaterialId, 1.00000000001m)]));
        Assert.True(FormulaPricingReviewRules.HasMaterialChanges(current, [Item(MaterialId, 1.00000000005m)]));
    }

    [Fact]
    public void EmptyMaterials_WithoutExistingMaterialsDoNotRequireReview()
        => Assert.False(FormulaPricingReviewRules.HasMaterialChanges([], []));

    [Fact]
    public void MaterialReconfirmation_AppearsAsPendingReviewUntilTheNextPriceApproval()
    {
        var approvedAt = new DateTime(2026, 10, 1, 8, 0, 0);
        var changedAt = approvedAt.AddHours(1);
        var options = new QuotationFeatureOptions { ApprovedPricingReviewAfterDays = 0 };

        var pending = ProductPricingAttentionRules.Resolve(
            approvedAt, [], changedAt, changedAt, options);
        var resolved = ProductPricingAttentionRules.Resolve(
            changedAt.AddHours(1), [], changedAt, changedAt.AddHours(1), options);

        Assert.Contains(ProductPricingAttentionSource.LabFormulaConfirmed, pending);
        Assert.Empty(resolved);
    }

    private static UpsertFormulaMaterialRequest Item(Guid id, decimal quantity)
        => new() { ItemType = ItemType.Material, ItemId = id, Quantity = quantity };
}
