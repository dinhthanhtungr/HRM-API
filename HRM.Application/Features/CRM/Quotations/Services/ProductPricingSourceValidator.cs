using HRM.Application.Commons.Models;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingSourceValidator
{
    private readonly ProductPricingSourceQueryService _sourceQueryService;

    public ProductPricingSourceValidator(ProductPricingSourceQueryService sourceQueryService)
    {
        _sourceQueryService = sourceQueryService;
    }

    public async Task<OperationResult<ProductPricingSourceSnapshot>> ValidateAsync(
        Guid productId,
        Guid companyId,
        ProductPricingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(sourceType) || sourceId == Guid.Empty)
        {
            return OperationResult<ProductPricingSourceSnapshot>.Fail(
                "A valid pricing source type and source id are required.");
        }

        var sourcesByProduct = await _sourceQueryService.LoadAsync(
            [productId],
            companyId,
            includeSensitivePricing: true,
            cancellationToken);
        var source = sourcesByProduct.GetValueOrDefault(productId)?
            .FirstOrDefault(x => x.SourceType == sourceType && x.SourceId == sourceId);
        if (source is null)
        {
            return OperationResult<ProductPricingSourceSnapshot>.Fail(
                "The pricing source was not found, is outside the current company/product, or has not reached an eligible approval status.");
        }

        return OperationResult<ProductPricingSourceSnapshot>.Ok(
            new ProductPricingSourceSnapshot(
                source.SourceType,
                source.SourceId,
                source.ExternalId,
                source.Name,
                source.MaterialCostSnapshot));
    }
}

internal sealed record ProductPricingSourceSnapshot(
    ProductPricingSourceType SourceType,
    Guid SourceId,
    string ExternalId,
    string Name,
    decimal? MaterialCostSnapshot)
{
    public Guid? FormulaId => SourceType == ProductPricingSourceType.Formula
        ? SourceId
        : null;

    public Guid? ManufacturingFormulaId =>
        SourceType == ProductPricingSourceType.ManufacturingFormula
            ? SourceId
            : null;
}
