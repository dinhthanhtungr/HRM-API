using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingAttentionRulesTests
{
    [Fact]
    public void ForNeedsPricingQueue_ExcludesFormulaUpdateReasonOnly()
    {
        var result = ProductPricingAttentionRules.ForNeedsPricingQueue(
        [
            ProductPricingAttentionSource.SaleQuotationRequested,
            ProductPricingAttentionSource.LabFormulaConfirmed,
            ProductPricingAttentionSource.ReviewExpired,
            ProductPricingAttentionSource.MaterialCostIncreased
        ]);

        Assert.Equal(
        [
            ProductPricingAttentionSource.SaleQuotationRequested,
            ProductPricingAttentionSource.ReviewExpired,
            ProductPricingAttentionSource.MaterialCostIncreased
        ], result);
    }
}
