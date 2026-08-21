using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingWorkspace;

internal sealed class GetQuotationPricingWorkspaceQueryHandler
    : IRequestHandler<GetQuotationPricingWorkspaceQuery, OperationResult<QuotationPricingWorkspaceDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingSourceQueryService _sourceQueryService;

    public GetQuotationPricingWorkspaceQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser,
        ProductPricingSourceQueryService sourceQueryService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
        _sourceQueryService = sourceQueryService;
    }

    public async Task<OperationResult<QuotationPricingWorkspaceDto>> Handle(
        GetQuotationPricingWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
        {
            return OperationResult<QuotationPricingWorkspaceDto>.Fail(
                "Only President or Developer can access the quotation pricing workspace.");
        }

        if (request.QuotationId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<QuotationPricingWorkspaceDto>.Fail(
                "QuotationId and current company context are required.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Include(x => x.Customer)
            .Include(x => x.SaleEmployee)
            .Include(x => x.Lines)
                .ThenInclude(x => x.PriceTiers)
            .FirstOrDefaultAsync(x =>
                x.QuotationId == request.QuotationId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (quotation is null)
        {
            return OperationResult<QuotationPricingWorkspaceDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        var productIds = quotation.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();
        var pricingVersions = await LoadPricingVersionsAsync(
            productIds,
            companyId,
            quotation.Currency,
            cancellationToken);
        var sourcesByProduct = await _sourceQueryService.LoadAsync(
            productIds,
            companyId,
            quotation.Currency,
            includeSensitivePricing: true,
            cancellationToken: cancellationToken);

        var draftByProduct = LatestByProduct(
            pricingVersions,
            ProductPricingStatus.Draft);
        var approvedByProduct = LatestByProduct(
            pricingVersions,
            ProductPricingStatus.Approved);

        var lines = quotation.Lines
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.QuotationLineId)
            .Select(line => MapLine(
                line,
                draftByProduct.GetValueOrDefault(line.ProductId),
                approvedByProduct.GetValueOrDefault(line.ProductId),
                sourcesByProduct.GetValueOrDefault(line.ProductId) ?? []))
            .ToArray();

        return OperationResult<QuotationPricingWorkspaceDto>.Ok(
            new QuotationPricingWorkspaceDto
            {
                QuotationId = quotation.QuotationId,
                QuotationExternalId = quotation.ExternalId,
                CustomerId = quotation.CustomerId,
                CustomerName = quotation.Customer.CustomerName,
                SaleEmployeeId = quotation.SaleEmployeeId,
                SaleEmployeeName = quotation.SaleEmployee.FullName,
                Currency = quotation.Currency,
                QuotationDate = quotation.QuotationDate,
                Status = quotation.Status,
                Lines = lines
            });
    }

    private async Task<IReadOnlyList<ProductPricingVersion>> LoadPricingVersionsAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.FormulaPricingPolicy)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == currency &&
                x.IsActive &&
                (x.Status == ProductPricingStatus.Draft ||
                 x.Status == ProductPricingStatus.Approved))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<Guid, ProductPricingVersion> LatestByProduct(
        IReadOnlyList<ProductPricingVersion> versions,
        ProductPricingStatus status)
        => versions
            .Where(x => x.Status == status)
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.Version)
                    .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                    .First());

    private static QuotationPricingWorkspaceLineDto MapLine(
        QuotationLine line,
        ProductPricingVersion? draft,
        ProductPricingVersion? approved,
        IReadOnlyList<ProductPricingSourceOptionDto> sources)
    {
        var hasAppliedPricing = line.ProductPricingVersionId.HasValue &&
            line.PriceTiers.Count > 0;
        var storedPricing = draft ?? approved;
        var selectedSource = storedPricing is null
            ? sources.FirstOrDefault()
            : FindSelectedSource(storedPricing, sources);
        var effectivePricing = BuildEffectivePricing(storedPricing, selectedSource);
        var storedTiers = storedPricing?.PriceTiers
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ProductPricingTierId)
            .ToArray() ?? [];
        var state = QuotationPricingWorkspaceRules.ResolveState(
            line.ProductPricingVersionId,
            line.PriceTiers.Count,
            approved is not null,
            draft is not null,
            sources.Count > 0);

        return new QuotationPricingWorkspaceLineDto
        {
            QuotationLineId = line.QuotationLineId,
            ProductId = line.ProductId,
            ProductCode = line.ProductExternalIdSnapshot,
            ProductName = line.ProductNameSnapshot,
            Quantity = line.Quantity,
            Unit = line.Unit,
            SortOrder = line.SortOrder,
            PricingState = state,
            AppliedProductPricingVersionId = line.ProductPricingVersionId,
            AppliedUnitPrice = line.UnitPrice,
            HasNewerApprovedPricing = hasAppliedPricing &&
                approved is not null &&
                approved.ProductPricingVersionId != line.ProductPricingVersionId,
            AppliedPriceTiers = line.PriceTiers
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.QuotationLinePriceTierId)
                .Select(x => new QuotationLinePriceTierDto
                {
                    QuotationLinePriceTierId = x.QuotationLinePriceTierId,
                    QuantityRangeLabel = x.QuantityRangeLabel,
                    MinQuantity = x.MinQuantity,
                    MaxQuantity = x.MaxQuantity,
                    MinInclusive = x.MinInclusive,
                    MaxInclusive = x.MaxInclusive,
                    UnitPrice = x.UnitPrice,
                    SortOrder = x.SortOrder
                })
                .ToArray(),
            DraftPricing = draft is null
                ? null
                : ProductPricingVersionMapper.ToDto(draft),
            ApprovedPricing = approved is null
                ? null
                : ProductPricingVersionMapper.ToDto(approved),
            SelectedPricingSource = selectedSource,
            CurrentMaterialCost = selectedSource?.CurrentMaterialCost,
            IsCurrentMaterialCostComplete =
                selectedSource?.IsCurrentMaterialCostComplete == true,
            MissingMaterialPriceCount =
                selectedSource?.MissingMaterialPriceCount ?? 0,
            StoredMaterialCostSnapshot = storedPricing?.MaterialCostSnapshot,
            StoredPricingUpdatedDate = storedPricing?.UpdatedDate ?? storedPricing?.CreatedDate,
            EffectivePricing = effectivePricing,
            PriceTiersAreStored = storedTiers.Length > 0,
            DisplayPriceTiers = storedTiers.Length > 0
                ? MapStoredTiers(storedTiers, effectivePricing)
                : MapSuggestedTiers(effectivePricing?.SuggestedPriceTiers ?? []),
            PricingSources = sources
        };
    }

    private static ProductPricingSourceOptionDto? FindSelectedSource(
        ProductPricingVersion? pricingVersion,
        IReadOnlyList<ProductPricingSourceOptionDto> sources)
    {
        if (pricingVersion?.SourceManufacturingFormulaId is { } manufacturingFormulaId)
        {
            return sources.FirstOrDefault(x =>
                x.SourceType == ProductPricingSourceType.ManufacturingFormula &&
                x.SourceId == manufacturingFormulaId);
        }

        if (pricingVersion?.SourceFormulaId is { } formulaId)
        {
            return sources.FirstOrDefault(x =>
                x.SourceType == ProductPricingSourceType.Formula &&
                x.SourceId == formulaId);
        }

        return null;
    }

    private static FormulaPriceCalculationDto? BuildEffectivePricing(
        ProductPricingVersion? storedPricing,
        ProductPricingSourceOptionDto? source)
    {
        // The source resolver owns the canonical calculation. Stored versions are
        // snapshots and must not trigger a second calculation in a read handler.
        return source?.Pricing;
    }

    private static IReadOnlyList<QuotationPricingWorkspaceTierDto> MapStoredTiers(
        IReadOnlyList<ProductPricingTier> tiers,
        FormulaPriceCalculationDto? pricing)
        => tiers
            .Select(x => new QuotationPricingWorkspaceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                MarginVsMaterialPercent = CalculateMargin(x.UnitPrice, pricing?.MaterialCost),
                MarginVsCostPercent = CalculateMargin(x.UnitPrice, pricing?.CostBase),
                RequiresManualPrice = false,
                IsStored = true,
                SortOrder = x.SortOrder
            })
            .ToArray();

    private static IReadOnlyList<QuotationPricingWorkspaceTierDto> MapSuggestedTiers(
        IReadOnlyList<FormulaSuggestedPriceTierDto> tiers)
        => tiers
            .Select(x => new QuotationPricingWorkspaceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                MarginVsMaterialPercent = x.MarginVsMaterialPercent,
                MarginVsCostPercent = x.MarginVsCostPercent,
                RequiresManualPrice = x.RequiresManualPrice,
                IsStored = false,
                SortOrder = x.SortOrder
            })
            .ToArray();

    private static decimal? CalculateMargin(decimal price, decimal? comparisonBase)
    {
        if (!comparisonBase.HasValue || comparisonBase <= 0m || price <= 0m)
        {
            return null;
        }

        return decimal.Round(
            (price - comparisonBase.Value) / comparisonBase.Value * 100m,
            4,
            MidpointRounding.AwayFromZero);
    }
}
