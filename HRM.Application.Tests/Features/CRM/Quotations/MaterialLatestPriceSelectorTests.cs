using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class MaterialLatestPriceSelectorTests
{
    [Fact]
    public void Select_NewerSupplierPrice_Wins()
    {
        var materialId = Guid.NewGuid();
        var purchaseOrderPrice = CreateCandidate(
            materialId, MaterialPriceSource.PurchaseOrder, 100m, new DateTime(2026, 9, 10));
        var supplierPrice = CreateCandidate(
            materialId, MaterialPriceSource.MaterialSupplier, 90m, new DateTime(2026, 9, 11));

        var result = MaterialLatestPriceSelector.Select(materialId, purchaseOrderPrice, supplierPrice);

        Assert.Equal(90m, result.CurrentPrice);
        Assert.Equal(MaterialPriceSource.MaterialSupplier, result.PriceSource);
    }

    [Fact]
    public void Select_SamePriceDate_PurchaseOrderWins()
    {
        var materialId = Guid.NewGuid();
        var priceDate = new DateTime(2026, 9, 11);
        var purchaseOrderPrice = CreateCandidate(
            materialId, MaterialPriceSource.PurchaseOrder, 90m, priceDate);
        var supplierPrice = CreateCandidate(
            materialId, MaterialPriceSource.MaterialSupplier, 120m, priceDate);

        var result = MaterialLatestPriceSelector.Select(materialId, purchaseOrderPrice, supplierPrice);

        Assert.Equal(90m, result.CurrentPrice);
        Assert.Equal(MaterialPriceSource.PurchaseOrder, result.PriceSource);
    }

    [Fact]
    public void Select_SameSourceAndDate_UsesLargerCandidateIdBeforePrice()
    {
        var materialId = Guid.NewGuid();
        var priceDate = new DateTime(2026, 9, 11);
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var left = CreateCandidate(
            materialId, MaterialPriceSource.PurchaseOrder, 120m, priceDate, lowerId);
        var right = CreateCandidate(
            materialId, MaterialPriceSource.PurchaseOrder, 90m, priceDate, higherId);

        var result = MaterialLatestPriceSelector.Select(materialId, left, right);

        Assert.Equal(90m, result.CurrentPrice);
    }

    [Fact]
    public void Select_IdenticalPriority_UsesLargerPrice()
    {
        var materialId = Guid.NewGuid();
        var candidateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var priceDate = new DateTime(2026, 9, 11);
        var left = CreateCandidate(
            materialId, MaterialPriceSource.MaterialSupplier, 90m, priceDate, candidateId);
        var right = CreateCandidate(
            materialId, MaterialPriceSource.MaterialSupplier, 120m, priceDate, candidateId);

        var result = MaterialLatestPriceSelector.Select(materialId, left, right);

        Assert.Equal(120m, result.CurrentPrice);
    }

    [Fact]
    public void Select_ExplicitZeroSupplierPrice_IsValid()
    {
        var materialId = Guid.NewGuid();
        var supplierPrice = new MaterialPriceCandidate
        {
            MaterialId = materialId,
            CurrentPrice = 0m,
            HasPriceValue = true,
            PriceSource = MaterialPriceSource.MaterialSupplier
        };

        var result = MaterialLatestPriceSelector.Select(materialId, null, supplierPrice);

        Assert.Equal(0m, result.CurrentPrice);
        Assert.Equal(MaterialPriceSource.MaterialSupplier, result.PriceSource);
    }

    [Fact]
    public void Select_MissingPriceValue_RemainsUnknown()
    {
        var materialId = Guid.NewGuid();
        var supplierPrice = new MaterialPriceCandidate
        {
            MaterialId = materialId,
            CurrentPrice = 0m,
            HasPriceValue = false,
            PriceSource = MaterialPriceSource.MaterialSupplier
        };

        var result = MaterialLatestPriceSelector.Select(materialId, null, supplierPrice);

        Assert.Equal(MaterialPriceSource.Unknown, result.PriceSource);
    }

    private static MaterialPriceCandidate CreateCandidate(
        Guid materialId,
        MaterialPriceSource source,
        decimal price,
        DateTime priceDate,
        Guid? candidateId = null)
        => new()
        {
            MaterialId = materialId,
            CandidateId = candidateId ?? Guid.NewGuid(),
            CurrentPrice = price,
            HasPriceValue = true,
            PriceDate = priceDate,
            PriceSource = source
        };
}
