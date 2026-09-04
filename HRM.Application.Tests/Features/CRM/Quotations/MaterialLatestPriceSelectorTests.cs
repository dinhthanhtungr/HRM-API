using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class MaterialLatestPriceSelectorTests
{
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
}
