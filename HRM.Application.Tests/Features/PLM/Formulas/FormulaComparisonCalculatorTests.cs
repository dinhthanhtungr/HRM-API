using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Comparison;
using HRM.Application.Features.PLM.Formulas.Queries.CompareFormulas;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaComparisonCalculatorTests
{
    [Fact]
    public void Compare_ReturnsAddedAndQuantityChangedItemsUsingCurrentPrices()
    {
        var resinId = Guid.NewGuid();
        var additiveId = Guid.NewGuid();
        var baseFormula = CreateFormula(
            "BASE",
            CreateMaterial(resinId, 1m, "Kg", "RESIN"));
        var comparedFormula = CreateFormula(
            "COMPARED",
            CreateMaterial(resinId, 2m, "Kg", "RESIN"),
            CreateMaterial(additiveId, 1m, "Kg", "ADDITIVE"));
        var prices = new Dictionary<PriceItemKey, LatestItemPriceDto>
        {
            [new PriceItemKey(ItemType.Material, resinId)] = CreatePrice(resinId, 100m),
            [new PriceItemKey(ItemType.Material, additiveId)] = CreatePrice(additiveId, 50m)
        };

        var result = FormulaComparisonCalculator.Compare(
            baseFormula,
            comparedFormula,
            "VND",
            prices,
            new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc));

        Assert.True(result.Summary.CanCompare);
        Assert.Equal(100m, result.Summary.BaseFormulaMaterialCost);
        Assert.Equal(250m, result.Summary.ComparedFormulaMaterialCost);
        Assert.Equal(150m, result.Summary.DifferenceAmount);
        Assert.Equal(FormulaComparisonStatus.Increased, result.Summary.ComparisonStatus);
        Assert.Equal(1, result.Summary.QuantityChangedCount);
        Assert.Equal(1, result.Summary.AddedToComparedCount);
        Assert.Contains(result.Items, x =>
            x.MaterialId == resinId &&
            x.Status == FormulaComparisonItemStatus.QuantityChanged &&
            x.QuantityDifference == 1m);
        Assert.Contains(result.Items, x =>
            x.MaterialId == additiveId &&
            x.Status == FormulaComparisonItemStatus.AddedToComparedFormula);
    }

    [Fact]
    public void Compare_MissingCurrentPriceKeepsAmountsUnavailable()
    {
        var materialId = Guid.NewGuid();
        var result = FormulaComparisonCalculator.Compare(
            CreateFormula("BASE", CreateMaterial(materialId, 1m, "Kg", "MAT")),
            CreateFormula("COMPARED", CreateMaterial(materialId, 2m, "Kg", "MAT")),
            "VND",
            new Dictionary<PriceItemKey, LatestItemPriceDto>(),
            DateTime.UtcNow);

        Assert.False(result.Summary.CanCompare);
        Assert.Null(result.Summary.BaseFormulaMaterialCost);
        Assert.Null(result.Summary.ComparedFormulaMaterialCost);
        Assert.Equal(
            FormulaComparisonUnavailableReason.MissingCurrentPrice,
            result.Summary.UnavailableReason);
        Assert.Equal(
            FormulaComparisonItemStatus.MissingCurrentPrice,
            Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Compare_AggregatesDuplicateRowsAndDetectsUnitMismatch()
    {
        var materialId = Guid.NewGuid();
        var baseFormula = CreateFormula(
            "BASE",
            CreateMaterial(materialId, 1m, "Kg", "MAT"),
            CreateMaterial(materialId, 2m, "KG", "MAT"));
        var comparedFormula = CreateFormula(
            "COMPARED",
            CreateMaterial(materialId, 3m, "Gram", "MAT"));
        var prices = new Dictionary<PriceItemKey, LatestItemPriceDto>
        {
            [new PriceItemKey(ItemType.Material, materialId)] = CreatePrice(materialId, 10m)
        };

        var result = FormulaComparisonCalculator.Compare(
            baseFormula,
            comparedFormula,
            "VND",
            prices,
            DateTime.UtcNow);

        var item = Assert.Single(result.Items);
        Assert.Equal(3m, item.BaseFormula.Quantity);
        Assert.Equal(2, item.BaseFormula.FormulaMaterialIds.Count);
        Assert.Equal(FormulaComparisonItemStatus.UnitMismatch, item.Status);
        Assert.False(result.Summary.CanCompare);
        Assert.Equal(1, result.Summary.UnitMismatchCount);
    }

    private static FormulaComparisonSource CreateFormula(
        string externalId,
        params FormulaComparisonMaterial[] materials)
    {
        var formulaId = Guid.NewGuid();
        foreach (var material in materials)
        {
            material.FormulaId = formulaId;
        }

        return new FormulaComparisonSource
        {
            FormulaId = formulaId,
            ProductId = Guid.NewGuid(),
            ExternalId = externalId,
            Name = externalId,
            Status = "Draft",
            Items = materials
        };
    }

    private static FormulaComparisonMaterial CreateMaterial(
        Guid materialId,
        decimal quantity,
        string unit,
        string code)
        => new()
        {
            FormulaMaterialId = Guid.NewGuid(),
            ItemId = materialId,
            ItemType = ItemType.Material,
            Quantity = quantity,
            Unit = unit,
            ItemCode = code,
            ItemName = code
        };

    private static LatestItemPriceDto CreatePrice(Guid materialId, decimal price)
        => new()
        {
            ItemType = ItemType.Material,
            ItemId = materialId,
            CurrentPrice = price,
            PriceSource = LatestPriceSourceType.PurchaseOrder
        };
}
