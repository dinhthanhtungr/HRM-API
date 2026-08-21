using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbenchDetail;

internal sealed class GetProductPricingWorkbenchDetailQueryHandler
    : IRequestHandler<
        GetProductPricingWorkbenchDetailQuery,
        OperationResult<ProductPricingWorkbenchDetailDto>>
{
    private const int MaximumPricingHistoryCount = 50;

    private readonly ICRMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;

    public GetProductPricingWorkbenchDetailQueryHandler(
        ICRMReadDbContext dbContext,
        ICurrentUser currentUser,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        ProductPricingRequestQueryService requestQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sourceQueryService = sourceQueryService;
        _requestQueryService = requestQueryService;
    }

    public async Task<OperationResult<ProductPricingWorkbenchDetailDto>> Handle(
        GetProductPricingWorkbenchDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanViewWorkbench(_currentUser))
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                "Only Sale, President or Developer can access the product pricing workbench.");
        }

        var canManagePricing = ProductPricingAccessRules.CanManage(_currentUser);

        if (request.ProductId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                "ProductId and current company context are required.");
        }

        if (request.NormalizedCurrency.Length is 0 or > QuotationRules.MaximumCurrencyLength)
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                $"Currency is required and cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        if (request.SourceType.HasValue != request.SourceId.HasValue ||
            (request.SourceType.HasValue && !Enum.IsDefined(request.SourceType.Value)) ||
            request.SourceId == Guid.Empty)
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                "SourceType and SourceId must be supplied together and must be valid.");
        }

        if (!canManagePricing && request.SourceType.HasValue)
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                "Only President or Developer can preview another pricing source.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new ProductRow
            {
                ProductId = x.ProductId,
                ProductCode = x.ColourCode ?? x.Code ?? string.Empty,
                ProductName = x.Name ?? string.Empty,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                "Product was not found, inactive, or outside the current company.");
        }

        var versionQuery = _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == request.ProductId &&
                x.Currency == request.NormalizedCurrency &&
                x.IsActive);

        var versions = await IncludeVersionDetails(versionQuery)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Take(MaximumPricingHistoryCount)
            .ToListAsync(cancellationToken);

        var draft = Latest(versions, ProductPricingStatus.Draft);
        var approved = Latest(versions, ProductPricingStatus.Approved);

        draft ??= await LoadLatestVersionAsync(
            versionQuery,
            ProductPricingStatus.Draft,
            cancellationToken);

        approved ??= await LoadLatestVersionAsync(
            versionQuery,
            ProductPricingStatus.Approved,
            cancellationToken);
        var storedPricing = draft ?? approved;

        ProductPricingSourceOptionDto? selectedSource;
        if (request.SourceType is { } requestedSourceType && request.SourceId is { } requestedSourceId)
        {
            var eligibleSources = await _sourceQueryService.LoadAsync(
                [request.ProductId],
                companyId,
                request.NormalizedCurrency,
                cancellationToken);

            selectedSource = eligibleSources.GetValueOrDefault(request.ProductId)?
                .FirstOrDefault(x =>
                    x.SourceType == requestedSourceType &&
                    x.SourceId == requestedSourceId);
            if (selectedSource is null)
            {
                return OperationResult<ProductPricingWorkbenchDetailDto>.Fail(
                    "The selected pricing source is not eligible or does not belong to this product/company.");
            }
        }
        else if (storedPricing is not null && TryGetSelection(storedPricing, out var selection))
        {
            var selectedSources = await _sourceQueryService.LoadSelectedAsync(
                [selection],
                companyId,
                request.NormalizedCurrency,
                cancellationToken);
            selectedSource = selectedSources.GetValueOrDefault(selection);
        }
        else
        {
            var eligibleSources = await _sourceQueryService.LoadAsync(
                [request.ProductId],
                companyId,
                request.NormalizedCurrency,
                cancellationToken);
            selectedSource = ProductPricingWorkbenchSourceSelector.ChooseFallback(
                eligibleSources.GetValueOrDefault(request.ProductId) ?? []);
        }

        var requestRows = await _requestQueryService.LoadAsync(
            companyId,
            request.NormalizedCurrency,
            [request.ProductId],
            cancellationToken);
        var draftRow = draft is null ? null : ToRow(draft);
        var approvedRow = approved is null ? null : ToRow(approved);
        var summary = ProductPricingWorkbenchMapper.MapSummary(
            product,
            request.NormalizedCurrency,
            draftRow,
            approvedRow,
            selectedSource,
            requestRows);
        var effectivePricing = ProductPricingWorkbenchMapper.BuildEffectivePricing(
            storedPricing is null ? null : ToRow(storedPricing),
            selectedSource);
        var storedTiers = storedPricing?.PriceTiers
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ProductPricingTierId)
            .ToArray() ?? [];

        var detail = new ProductPricingWorkbenchDetailDto
            {
                Summary = summary,
                ManufacturingCost = summary.ManufacturingCost,
                StandardSellingPrice = summary.StandardSellingPrice,
                ProfitMarginRate = summary.ProfitMarginRate,
                SelectedSource = selectedSource,
                DraftPricing = draft is null ? null : ProductPricingVersionMapper.ToDto(draft),
                ApprovedPricing = approved is null ? null : ProductPricingVersionMapper.ToDto(approved),
                DisplayPriceTiers = storedTiers.Length > 0
                    ? ProductPricingWorkbenchMapper.MapStoredTiers(storedTiers, effectivePricing)
                    : ProductPricingWorkbenchMapper.MapSuggestedTiers(
                        effectivePricing?.SuggestedPriceTiers ??
                        selectedSource?.PriceTierTemplates ??
                        []),
                PricingHistory = versions
                    .Select(x => ProductPricingVersionMapper.ToDto(x))
                    .ToArray(),
                RelatedQuotations = requestRows
                    .GroupBy(x => x.QuotationId)
                    .Select(group => new ProductPricingRelatedQuotationDto
                    {
                        QuotationId = group.Key,
                        QuotationExternalId = group.First().QuotationExternalId,
                        CustomerId = group.First().CustomerId,
                        CustomerExternalId = group.First().CustomerExternalId,
                        CustomerName = group.First().CustomerName,
                        SaleEmployeeId = group.First().SaleEmployeeId,
                        SaleEmployeeName = group.First().SaleEmployeeName,
                        Quantity = group.Sum(x => x.Quantity),
                        Unit = group.First().Unit,
                        RequestedAt = group.Max(x => x.RequestedAt)
                    })
                    .OrderByDescending(x => x.RequestedAt)
                    .ToArray()
            };

        return OperationResult<ProductPricingWorkbenchDetailDto>.Ok(
            ProductPricingWorkbenchVisibility.ApplyToDetail(detail, canManagePricing));
    }

    private static IQueryable<ProductPricingVersion> IncludeVersionDetails(
        IQueryable<ProductPricingVersion> query)
        => query
            .Include(x => x.Product)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Include(x => x.FormulaPricingPolicy)
            .Include(x => x.PriceTiers);

    private static Task<ProductPricingVersion?> LoadLatestVersionAsync(
        IQueryable<ProductPricingVersion> query,
        ProductPricingStatus status,
        CancellationToken cancellationToken)
        => IncludeVersionDetails(query)
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

    private static ProductPricingVersion? Latest(
        IReadOnlyList<ProductPricingVersion> versions,
        ProductPricingStatus status)
        => versions.FirstOrDefault(x => x.Status == status);

    private static bool TryGetSelection(
        ProductPricingVersion version,
        out ProductPricingSourceSelection selection)
    {
        if (version.SourceManufacturingFormulaId is { } manufacturingFormulaId)
        {
            selection = new ProductPricingSourceSelection(
                version.ProductId,
                ProductPricingSourceType.ManufacturingFormula,
                manufacturingFormulaId);
            return true;
        }

        if (version.SourceFormulaId is { } formulaId)
        {
            selection = new ProductPricingSourceSelection(
                version.ProductId,
                ProductPricingSourceType.Formula,
                formulaId);
            return true;
        }

        selection = default;
        return false;
    }

    private static PricingVersionRow ToRow(ProductPricingVersion version)
        => new()
        {
            ProductPricingVersionId = version.ProductPricingVersionId,
            ProductId = version.ProductId,
            SourceType = version.SourceManufacturingFormulaId.HasValue
                ? ProductPricingSourceType.ManufacturingFormula
                : version.SourceFormulaId.HasValue
                    ? ProductPricingSourceType.Formula
                    : null,
            SourceId = version.SourceManufacturingFormulaId ?? version.SourceFormulaId,
            SourceExternalId = version.FormulaExternalIdSnapshot,
            MaterialCostSnapshot = version.MaterialCostSnapshot,
            ManufacturingCost = version.ManufacturingCost,
            StandardSellingPrice = version.StandardSellingPrice,
            ProfitMarginRate = version.ProfitMarginRate,
            Status = version.Status,
            Version = version.Version,
            CreatedDate = version.CreatedDate,
            UpdatedDate = version.UpdatedDate
        };
}
