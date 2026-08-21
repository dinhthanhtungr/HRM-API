using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingSourceQueryService
{
    private readonly ProductPricingRealtimeSourceQueryService _realtimeSourceQueryService;

    public ProductPricingSourceQueryService(
        ProductPricingRealtimeSourceQueryService realtimeSourceQueryService)
    {
        _realtimeSourceQueryService = realtimeSourceQueryService;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        bool includeSensitivePricing,
        CancellationToken cancellationToken)
    {
        var resolved = await _realtimeSourceQueryService.LoadAsync(
            productIds,
            companyId,
            currency,
            cancellationToken);
        if (includeSensitivePricing)
        {
            return resolved;
        }

        return resolved.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ProductPricingSourceOptionDto>)pair.Value
                .Select(ToSaleSource)
                .ToArray());
    }

    private static ProductPricingSourceOptionDto ToSaleSource(
        ProductPricingSourceOptionDto source)
        => new()
        {
            PricingStatus = source.PricingStatus,
            FormulaPricingPolicyId = source.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = source.FormulaPricingPolicyVersion,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            Status = source.Status,
            IsEligible = source.IsEligible,
            IsCustomerSelected = source.IsCustomerSelected,
            StandardSellingPrice = source.StandardSellingPrice,
            PricingProfile = source.PricingProfile,
            PriceTierTemplates = source.PriceTierTemplates,
            UpdatedDate = source.UpdatedDate
        };
}
