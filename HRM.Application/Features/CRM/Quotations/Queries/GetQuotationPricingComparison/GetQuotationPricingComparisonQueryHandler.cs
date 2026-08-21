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
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetQuotationPricingComparisonQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
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
                Currency = x.Currency,
                Lines = x.Lines
                    .OrderBy(line => line.SortOrder)
                    .ThenBy(line => line.QuotationLineId)
                    .Select(line => new PricingComparisonLine
                    {
                        QuotationLineId = line.QuotationLineId,
                        ProductId = line.ProductId,
                        ProductPricingVersionId = line.ProductPricingVersionId,
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

        var productIds = quotation.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var approvedRows = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.SourceFormula)
            .Include(x => x.PriceTiers)
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == scope.CompanyId &&
                x.Currency == quotation.Currency &&
                x.Status == ProductPricingStatus.Approved &&
                x.IsActive)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        var currentPricingByProductId = approvedRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());
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
        HRM.Domain.Entities.CustomerSchema.ProductPricingVersion? current)
    {
        var currentTiers = current?.PriceTiers
            .OrderBy(x => x.SortOrder)
            .Select(x => new QuotationCurrentPriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                RequiresManualPrice = false,
                SortOrder = x.SortOrder
            })
            .ToList() ?? [];
        var status = current is null
            ? QuotationCurrentPricingStatus.ApprovedPricingNotFound
            : currentTiers.Count == 0
                ? QuotationCurrentPricingStatus.ManualTierPriceRequired
                : QuotationCurrentPricingStatus.Available;
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
            CurrentProductPricingVersionId = current?.ProductPricingVersionId,
            CurrentProductPricingVersion = current?.Version,
            IsUsingLatestApprovedPricing = current is not null &&
                line.ProductPricingVersionId == current.ProductPricingVersionId,
            FormulaId = current?.SourceFormulaId,
            FormulaExternalId = current?.FormulaExternalIdSnapshot,
            FormulaName = current?.SourceFormula?.Name,
            FormulaSelectionSource = null,
            CurrentPricingStatus = status,
            IsCurrentPricingComplete = isComplete,
            MissingMaterialPriceCount = 0,
            CurrentPriceTiers = currentTiers,
            HasDifference = isComplete
                ? HasTierDifference(line.PriceTiers, currentTiers)
                : null
        };
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
        public string Currency { get; init; } = string.Empty;
        public IReadOnlyList<PricingComparisonLine> Lines { get; init; } = [];
    }

    private sealed class PricingComparisonLine
    {
        public Guid QuotationLineId { get; init; }
        public Guid ProductId { get; init; }
        public Guid? ProductPricingVersionId { get; init; }
        public string ProductExternalId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public QuotationLinePriceMode PriceMode { get; init; }
        public decimal UnitPrice { get; init; }
        public IReadOnlyList<QuotationLinePriceTierDto> PriceTiers { get; init; } = [];
    }
}
