using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingVersionMapper
{
    public static ProductPricingVersionDto ToDto(
        ProductPricingVersion entity,
        bool includeSensitivePricing = true)
        => new()
        {
            ProductPricingVersionId = entity.ProductPricingVersionId,
            ProductId = entity.ProductId,
            FormulaPricingPolicyId = includeSensitivePricing
                ? entity.FormulaPricingPolicyId
                : null,
            FormulaPricingPolicyVersion = includeSensitivePricing
                ? entity.FormulaPricingPolicy?.Version
                : null,
            HasManualTierAdjustment = includeSensitivePricing
                ? entity.HasManualTierAdjustment
                : null,
            ProductCode = entity.Product.ColourCode ?? entity.Product.Code ?? string.Empty,
            ProductName = entity.Product.Name ?? string.Empty,
            SourceType = ResolveSourceType(entity),
            SourceId = entity.SourceManufacturingFormulaId ??
                entity.SourceFormulaId ??
                Guid.Empty,
            SourceExternalId = entity.FormulaExternalIdSnapshot ?? string.Empty,
            SourceName = entity.SourceManufacturingFormula?.Name ??
                entity.SourceFormula?.Name ??
                string.Empty,
            SourceFormulaId = entity.SourceFormulaId,
            SourceManufacturingFormulaId = entity.SourceManufacturingFormulaId,
            SourceSampleTrialId = entity.SourceSampleTrialId,
            SourceManufacturingVUFormulaId = entity.SourceManufacturingVUFormulaId,
            FormulaExternalIdSnapshot = entity.FormulaExternalIdSnapshot,
            BatchNoSnapshot = entity.BatchNoSnapshot,
            Currency = entity.Currency,
            MaterialCostSnapshot = includeSensitivePricing ? entity.MaterialCostSnapshot : null,
            ManufacturingCost = includeSensitivePricing ? entity.ManufacturingCost : null,
            StandardSellingPrice = entity.StandardSellingPrice,
            ProfitMarginRate = includeSensitivePricing ? entity.ProfitMarginRate : null,
            Status = entity.Status,
            Version = entity.Version,
            CalculatedAt = entity.CalculatedAt,
            ApprovedBy = entity.ApprovedBy,
            ApprovedAt = entity.ApprovedAt,
            CreatedDate = entity.CreatedDate,
            UpdatedDate = entity.UpdatedDate,
            PriceTiers = entity.PriceTiers
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductPricingTierDto
                {
                    ProductPricingTierId = x.ProductPricingTierId,
                    QuantityRangeLabel = x.QuantityRangeLabel,
                    MinQuantity = x.MinQuantity,
                    MaxQuantity = x.MaxQuantity,
                    MinInclusive = x.MinInclusive,
                    MaxInclusive = x.MaxInclusive,
                    UnitPrice = x.UnitPrice,
                    SortOrder = x.SortOrder
                })
                .ToArray()
        };

    private static HRM.Domain.Enums.CustomerEnum.ProductPricingSourceType ResolveSourceType(
        ProductPricingVersion entity)
        => entity.SourceManufacturingFormulaId.HasValue
            ? HRM.Domain.Enums.CustomerEnum.ProductPricingSourceType.ManufacturingFormula
            : HRM.Domain.Enums.CustomerEnum.ProductPricingSourceType.Formula;
}
