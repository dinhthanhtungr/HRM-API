using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal static class PricingSourceColourCodeFilter
{
    // Keep this composable: both callers filter before selecting a winner or paging.
    internal static IQueryable<ExcludedPricingSource> SelectExcludedSources(
        IQueryable<ManufacturingFormulaMaterial> materials,
        IQueryable<Product> targets,
        Guid companyId)
        => from target in targets
           from material in materials
           where target.CompanyId == companyId &&
                 target.ColourCode != null && target.ColourCode.Trim() != "" &&
                 material.IsActive &&
                 material.ManufacturingFormula.CompanyId == companyId &&
                 (material.itemType == ItemType.Product || material.itemType == ItemType.ProductFailure) &&
                 material.ProductId.HasValue && material.Product != null &&
                 material.Product.CompanyId == companyId &&
                 material.Product.ColourCode != null && material.Product.ColourCode.Trim() != "" &&
                 material.Product.ColourCode.Trim().ToUpper() == target.ColourCode.Trim().ToUpper()
           select new ExcludedPricingSource
           {
               ProductId = target.ProductId,
               ManufacturingFormulaId = material.ManufacturingFormulaId
           };
}

internal sealed class ExcludedPricingSource
{
    public Guid ProductId { get; init; }
    public Guid ManufacturingFormulaId { get; init; }
}
