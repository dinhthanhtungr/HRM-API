using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingWorkbenchSourceSelector
{
    public static ProductPricingSourceOptionDto? ChooseFallback(
        IReadOnlyList<ProductPricingSourceOptionDto> sources)
        => sources
            .OrderByDescending(x =>
                x.SourceType == ProductPricingSourceType.Formula &&
                x.Status == FormulaStatus.Approved.ToString())
            .ThenByDescending(x => x.IsCustomerSelected)
            .ThenByDescending(x => x.UpdatedDate)
            .ThenBy(x => x.ExternalId)
            .FirstOrDefault();
}
