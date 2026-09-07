using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingWorkbenchSourceSelector
{
    public static ProductPricingSourceOptionDto? ChooseFallback(
        IReadOnlyList<ProductPricingSourceOptionDto> sources)
        => sources
            // All items here have already passed the eligible-source rule.  For a
            // fallback calculation, use the most recently changed eligible source;
            // Approved must not outrank a newer SampleSent or Completed formula.
            .OrderByDescending(x => x.UpdatedDate)
            .ThenByDescending(x => x.IsCustomerSelected)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.ExternalId)
            .FirstOrDefault();
}
