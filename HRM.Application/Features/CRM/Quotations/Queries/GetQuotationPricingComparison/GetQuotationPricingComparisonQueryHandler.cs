using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingComparison;

internal sealed class GetQuotationPricingComparisonQueryHandler
    : IRequestHandler<GetQuotationPricingComparisonQuery,
        OperationResult<QuotationPricingComparisonDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly QuotationCurrentPricingResolver _pricingResolver;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetQuotationPricingComparisonQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        QuotationCurrentPricingResolver pricingResolver,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _pricingResolver = pricingResolver;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<QuotationPricingComparisonDto>> Handle(
        GetQuotationPricingComparisonQuery request,
        CancellationToken cancellationToken)
    {
        if (request.QuotationId == Guid.Empty)
        {
            return OperationResult<QuotationPricingComparisonDto>.Fail(
                "QuotationId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.QuotationId == request.QuotationId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive)
            .Select(x => new PricingComparisonQuotation
            {
                QuotationId = x.QuotationId,
                Lines = x.Lines
                    .OrderBy(line => line.SortOrder)
                    .ThenBy(line => line.QuotationLineId)
                    .Select(line => new PricingComparisonLine
                    {
                        QuotationLineId = line.QuotationLineId,
                        ProductId = line.ProductId,
                        ProductExternalId = line.ProductExternalIdSnapshot,
                        ProductName = line.ProductNameSnapshot,
                        PriceMode = line.PriceMode,
                        UnitPrice = line.UnitPrice,
                        PriceTiers = line.PriceTiers
                            .OrderBy(tier => tier.SortOrder)
                            .ThenBy(tier => tier.QuotationLinePriceTierId)
                            .Select(tier => new QuotationLinePriceTierDto
                            {
                                QuotationLinePriceTierId = tier.QuotationLinePriceTierId,
                                QuantityRangeLabel = tier.QuantityRangeLabel,
                                MinQuantity = tier.MinQuantity,
                                MaxQuantity = tier.MaxQuantity,
                                MinInclusive = tier.MinInclusive,
                                MaxInclusive = tier.MaxInclusive,
                                UnitPrice = tier.UnitPrice,
                                SortOrder = tier.SortOrder
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (quotation is null)
        {
            return OperationResult<QuotationPricingComparisonDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        var currentPricingByProductId = await _pricingResolver.ResolveAsync(
            quotation.Lines.Select(x => x.ProductId),
            scope.CompanyId,
            cancellationToken);
        var lines = quotation.Lines
            .Select(line => BuildLineComparison(
                line,
                currentPricingByProductId.GetValueOrDefault(line.ProductId)))
            .ToList();

        return OperationResult<QuotationPricingComparisonDto>.Ok(
            new QuotationPricingComparisonDto
            {
                QuotationId = quotation.QuotationId,
                CalculatedAt = _dateTimeProvider.Now,
                Lines = lines
            });
    }

    private static QuotationLinePricingComparisonDto BuildLineComparison(
        PricingComparisonLine line,
        QuotationCurrentProductPricing? current)
    {
        var currentTiers = current?.Pricing?.SuggestedPriceTiers
            .Select(x => new QuotationCurrentPriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                RequiresManualPrice = x.RequiresManualPrice,
                SortOrder = x.SortOrder
            })
            .ToList() ?? [];
        var status = ResolveStatus(current, currentTiers);
        var isComplete = status == QuotationCurrentPricingStatus.Available;

        return new QuotationLinePricingComparisonDto
        {
            QuotationLineId = line.QuotationLineId,
            ProductId = line.ProductId,
            ProductExternalId = line.ProductExternalId,
            ProductName = line.ProductName,
            SavedPriceMode = line.PriceMode,
            SavedUnitPrice = line.UnitPrice,
            SavedPriceTiers = line.PriceTiers,
            FormulaId = current?.FormulaId,
            FormulaExternalId = current?.FormulaExternalId,
            FormulaName = current?.FormulaName,
            FormulaSelectionSource = current?.FormulaSelectionSource,
            CurrentPricingStatus = status,
            IsCurrentPricingComplete = isComplete,
            MissingMaterialPriceCount = current?.RealtimeMaterialCost.MissingPriceCount ?? 0,
            CurrentPriceTiers = currentTiers,
            HasDifference = isComplete
                ? HasTierDifference(line.PriceTiers, currentTiers)
                : null
        };
    }

    private static QuotationCurrentPricingStatus ResolveStatus(
        QuotationCurrentProductPricing? current,
        IReadOnlyCollection<QuotationCurrentPriceTierDto> currentTiers)
    {
        if (current is null)
        {
            return QuotationCurrentPricingStatus.ProductNotFound;
        }

        if (!current.FormulaId.HasValue)
        {
            return QuotationCurrentPricingStatus.FormulaNotFound;
        }

        if (!current.HasFormulaMaterials)
        {
            return QuotationCurrentPricingStatus.FormulaMaterialsMissing;
        }

        if (!current.RealtimeMaterialCost.IsComplete)
        {
            return QuotationCurrentPricingStatus.MaterialPriceMissing;
        }

        return currentTiers.Any(x => x.RequiresManualPrice || !x.UnitPrice.HasValue)
            ? QuotationCurrentPricingStatus.ManualTierPriceRequired
            : QuotationCurrentPricingStatus.Available;
    }

    private static bool HasTierDifference(
        IReadOnlyCollection<QuotationLinePriceTierDto> savedTiers,
        IReadOnlyCollection<QuotationCurrentPriceTierDto> currentTiers)
    {
        if (savedTiers.Count != currentTiers.Count)
        {
            return true;
        }

        var orderedSaved = savedTiers.OrderBy(x => x.SortOrder).ToArray();
        var orderedCurrent = currentTiers.OrderBy(x => x.SortOrder).ToArray();
        for (var index = 0; index < orderedSaved.Length; index++)
        {
            var saved = orderedSaved[index];
            var current = orderedCurrent[index];
            if (saved.QuantityRangeLabel != current.QuantityRangeLabel ||
                saved.MinQuantity != current.MinQuantity ||
                saved.MaxQuantity != current.MaxQuantity ||
                saved.MinInclusive != current.MinInclusive ||
                saved.MaxInclusive != current.MaxInclusive ||
                saved.UnitPrice != current.UnitPrice ||
                saved.SortOrder != current.SortOrder)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class PricingComparisonQuotation
    {
        public Guid QuotationId { get; init; }
        public IReadOnlyList<PricingComparisonLine> Lines { get; init; } = [];
    }

    private sealed class PricingComparisonLine
    {
        public Guid QuotationLineId { get; init; }
        public Guid ProductId { get; init; }
        public string ProductExternalId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public QuotationLinePriceMode PriceMode { get; init; }
        public decimal UnitPrice { get; init; }
        public IReadOnlyList<QuotationLinePriceTierDto> PriceTiers { get; init; } = [];
    }
}
