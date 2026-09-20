using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Tải chi phí NVL realtime của đúng nguồn đã được duyệt và tạo read-model
/// so sánh giá chuẩn dùng chung cho các API báo giá.
/// </summary>
internal sealed class StandardPriceRealtimeComparisonQueryService
{
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;

    public StandardPriceRealtimeComparisonQueryService(
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions)
    {
        _sourceQueryService = sourceQueryService;
        _dateTimeProvider = dateTimeProvider;
        _featureOptions = featureOptions;
    }

    public async Task<IReadOnlyDictionary<Guid, StandardPriceRealtimeComparisonDto>> LoadVisibleAsync(
        IReadOnlyCollection<StandardPriceRealtimeComparisonRequest> requests,
        Guid companyId,
        PricingAccessDecision access,
        CancellationToken cancellationToken)
    {
        if (!access.CanViewApprovedSellingPrice || requests.Count == 0)
        {
            return new Dictionary<Guid, StandardPriceRealtimeComparisonDto>();
        }

        var normalizedRequests = requests
            .Where(x => x.ProductId != Guid.Empty && x.ApprovedStandardPrice is > 0m)
            .GroupBy(x => x.ProductId)
            .Select(x => x.First())
            .ToArray();
        if (normalizedRequests.Length == 0)
        {
            return new Dictionary<Guid, StandardPriceRealtimeComparisonDto>();
        }

        var sources = new Dictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>();
        foreach (var currencyGroup in normalizedRequests.GroupBy(x => x.Currency))
        {
            var selections = currencyGroup
                .Where(x => x.SourceType.HasValue && x.SourceId is { } sourceId && sourceId != Guid.Empty)
                .Select(x => new ProductPricingSourceSelection(
                    x.ProductId,
                    x.SourceType!.Value,
                    x.SourceId!.Value))
                .Distinct()
                .ToArray();
            if (selections.Length == 0)
            {
                continue;
            }

            var loaded = await _sourceQueryService.LoadSelectedAsync(
                selections,
                companyId,
                currencyGroup.Key,
                cancellationToken);
            foreach (var pair in loaded)
            {
                sources[pair.Key] = pair.Value;
            }
        }

        return BuildVisible(normalizedRequests, sources, access);
    }

    public IReadOnlyDictionary<Guid, StandardPriceRealtimeComparisonDto> BuildVisible(
        IReadOnlyCollection<StandardPriceRealtimeComparisonRequest> requests,
        IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto> sources,
        PricingAccessDecision access)
    {
        if (!access.CanViewApprovedSellingPrice || requests.Count == 0)
        {
            return new Dictionary<Guid, StandardPriceRealtimeComparisonDto>();
        }

        var normalizedRequests = requests
            .Where(x => x.ProductId != Guid.Empty && x.ApprovedStandardPrice is > 0m)
            .GroupBy(x => x.ProductId)
            .Select(x => x.First())
            .ToArray();

        var calculatedAt = _dateTimeProvider.Now;
        var result = new Dictionary<Guid, StandardPriceRealtimeComparisonDto>();
        foreach (var request in normalizedRequests)
        {
            ProductPricingSourceOptionDto? source = null;
            if (request.SourceType.HasValue && request.SourceId is { } sourceId && sourceId != Guid.Empty)
            {
                sources.TryGetValue(
                    new ProductPricingSourceSelection(
                        request.ProductId,
                        request.SourceType.Value,
                        sourceId),
                    out source);
            }

            var comparison = StandardPriceRealtimeComparisonCalculator.Calculate(
                request.Currency,
                request.ApprovedStandardPrice,
                request.ApprovedMaterialCostSnapshot,
                source?.CurrentMaterialCost,
                source?.IsCurrentMaterialCostComplete == true,
                _featureOptions.MaterialCostChangeThresholdPercent,
                calculatedAt);
            comparison = StandardPriceRealtimeComparisonVisibility.Apply(comparison, access);
            if (comparison is not null)
            {
                result[request.ProductId] = comparison;
            }
        }

        return result;
    }
}

internal sealed record StandardPriceRealtimeComparisonRequest(
    Guid ProductId,
    string Currency,
    decimal? ApprovedStandardPrice,
    decimal? ApprovedMaterialCostSnapshot,
    ProductPricingSourceType? SourceType,
    Guid? SourceId);
