using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Materials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviews;

internal sealed class GetMaterialPriceReviewsQueryHandler
    : IRequestHandler<GetMaterialPriceReviewsQuery, PagedResult<MaterialPriceReviewItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IMaterialPriceQueryService _priceQueryService;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPLMFieldVisibilityService _fieldVisibility;

    public GetMaterialPriceReviewsQueryHandler(
        IPLMReadDbContext dbContext,
        IMaterialPriceQueryService priceQueryService,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IPLMFieldVisibilityService fieldVisibility)
    {
        _dbContext = dbContext;
        _priceQueryService = priceQueryService;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _fieldVisibility = fieldVisibility;
    }

    public async Task<PagedResult<MaterialPriceReviewItemDto>> Handle(
        GetMaterialPriceReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return Empty(request);
        }

        var canViewDetails = _fieldVisibility.CanViewMaterialPriceReviewDetails();
        var canViewPrice = canViewDetails || _fieldVisibility.CanViewFormulaPrices();
        var recentCutoff = _dateTimeProvider.Now.AddMonths(-2);
        var recentFormulaUsage = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.itemType == ItemType.Material &&
                x.MaterialId.HasValue &&
                x.Formula.IsActive &&
                x.Formula.CompanyId == companyId)
            .SelectMany(
                material => material.Formula.SampleRequests
                    .Where(sampleRequest =>
                        sampleRequest.IsActive &&
                        sampleRequest.CompanyId == companyId &&
                        sampleRequest.CreatedDate >= recentCutoff),
                (material, sampleRequest) => new
                {
                    MaterialId = material.MaterialId!.Value,
                    UsedAt = sampleRequest.CreatedDate
                })
            .GroupBy(x => x.MaterialId)
            .Select(group => new MaterialUsageRow
            {
                MaterialId = group.Key,
                UsedAt = group.Max(x => x.UsedAt)
            })
            .ToListAsync(cancellationToken);

        var manufacturingFormulaUsage = await _dbContext.ManufacturingFormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.itemType == ItemType.Material &&
                x.MaterialId.HasValue &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.CompanyId == companyId)
            .GroupBy(x => x.MaterialId!.Value)
            .Select(group => new MaterialUsageRow
            {
                MaterialId = group.Key,
                UsedAt = group.Max(x => x.ManufacturingFormula.CreatedDate)
            })
            .ToListAsync(cancellationToken);

        var usages = recentFormulaUsage.ToDictionary(
            x => x.MaterialId,
            x => new MaterialUsage(
                MaterialPriceReviewUsageSource.RecentSampleRequestFormula,
                x.UsedAt));

        foreach (var usage in manufacturingFormulaUsage)
        {
            usages.TryAdd(
                usage.MaterialId,
                new MaterialUsage(
                    MaterialPriceReviewUsageSource.ManufacturingFormula,
                    usage.UsedAt));
        }

        if (usages.Count == 0)
        {
            return Empty(request);
        }

        var materialIds = usages.Keys.ToList();
        var materialsQuery = _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                materialIds.Contains(x.MaterialId) &&
                x.CompanyId == companyId &&
                x.IsActive == true);

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            materialsQuery = materialsQuery.Where(x =>
                EF.Functions.ILike(x.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.CustomCode ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var materialRows = canViewDetails
            ? await materialsQuery.Select(x => new MaterialRow
            {
                MaterialId = x.MaterialId,
                ExternalId = x.ExternalId,
                CustomCode = x.CustomCode,
                Name = x.Name ?? string.Empty,
                Type = x.Category.Types ?? x.Category.Name,
                Unit = x.Unit,
                PurchaseStatus = x.PurchaseAvailability == null
                    ? MaterialPurchaseStatus.Available
                    : x.PurchaseAvailability.Status,
                PurchaseStatusReason = x.PurchaseAvailability == null
                    ? null
                    : x.PurchaseAvailability.Reason,
                PurchaseStatusEffectiveFrom = x.PurchaseAvailability == null
                    ? null
                    : x.PurchaseAvailability.EffectiveFrom,
                ExpectedAvailableDate = x.PurchaseAvailability == null
                    ? null
                    : x.PurchaseAvailability.ExpectedAvailableDate,
                SupplierCount = x.MaterialsSuppliers.Count(materialSupplier =>
                    materialSupplier.IsActive == true &&
                    materialSupplier.Supplier.IsActive == true &&
                    materialSupplier.Supplier.CompanyId == companyId)
            })
            .ToListAsync(cancellationToken)
            : await materialsQuery.Select(x => new MaterialRow
            {
                MaterialId = x.MaterialId,
                ExternalId = x.ExternalId,
                CustomCode = x.CustomCode,
                Name = x.Name ?? string.Empty,
                PurchaseStatus = x.PurchaseAvailability == null
                    ? MaterialPurchaseStatus.Available
                    : x.PurchaseAvailability.Status
            }).ToListAsync(cancellationToken);

        var orderedRows = materialRows
            .OrderBy(x => usages[x.MaterialId].Source)
            .ThenByDescending(x => usages[x.MaterialId].UsedAt)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.MaterialId)
            .ToList();

        var prices = canViewPrice
            ? await _priceQueryService.LoadLatestMaterialPriceInfoDictAsync(
                orderedRows.Select(x => (Guid?)x.MaterialId),
                cancellationToken)
            : new Dictionary<Guid, LatestMaterialPriceDto>();
        var staleCutoff = _dateTimeProvider.Now.AddDays(-request.NormalizedStaleAfterDays);

        var filteredRows = !canViewDetails || request.PriceStatus is not { } requestedStatus
            ? orderedRows
            : orderedRows
                .Where(row => ResolveReviewStatus(
                    prices.GetValueOrDefault(row.MaterialId),
                    staleCutoff) == requestedStatus)
                .ToList();

        var totalCount = filteredRows.Count;
        var pageRows = filteredRows
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToList();

        var items = pageRows.Select(row =>
        {
            var usage = usages[row.MaterialId];
            prices.TryGetValue(row.MaterialId, out var price);

            return new MaterialPriceReviewItemDto
            {
                MaterialId = row.MaterialId,
                ExternalId = row.ExternalId,
                CustomCode = row.CustomCode,
                Name = row.Name,
                Type = row.Type,
                Unit = row.Unit,
                CurrentPrice = canViewPrice ? price?.CurrentPrice : null,
                LastPriceUpdatedAt = canViewDetails ? price?.PriceDate : null,
                PriceSource = canViewDetails ? price?.PriceSource ?? MaterialPriceSource.Unknown : null,
                ReviewStatus = canViewDetails ? ResolveReviewStatus(price, staleCutoff) : null,
                UsageSource = canViewDetails ? usage.Source : null,
                LastUsedAt = canViewDetails ? usage.UsedAt : null,
                SupplierCount = canViewDetails ? row.SupplierCount : null,
                PurchaseStatus = row.PurchaseStatus,
                PurchaseStatusReason = canViewDetails ? row.PurchaseStatusReason : null,
                PurchaseStatusEffectiveFrom = canViewDetails ? row.PurchaseStatusEffectiveFrom : null,
                ExpectedAvailableDate = canViewDetails ? row.ExpectedAvailableDate : null
            };
        }).ToList();

        return new PagedResult<MaterialPriceReviewItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static MaterialPriceReviewStatus ResolveReviewStatus(
        LatestMaterialPriceDto? price,
        DateTime staleCutoff)
    {
        if (price is null || price.PriceSource == MaterialPriceSource.Unknown)
        {
            return MaterialPriceReviewStatus.MissingPrice;
        }

        return !price.PriceDate.HasValue || price.PriceDate.Value < staleCutoff
            ? MaterialPriceReviewStatus.NeedsReview
            : MaterialPriceReviewStatus.UpToDate;
    }

    private static PagedResult<MaterialPriceReviewItemDto> Empty(
        GetMaterialPriceReviewsQuery request)
        => new([], 0, request.NormalizedPageNumber, request.NormalizedPageSize);

    private sealed class MaterialUsageRow
    {
        public Guid MaterialId { get; init; }
        public DateTime UsedAt { get; init; }
    }

    private sealed record MaterialUsage(
        MaterialPriceReviewUsageSource Source,
        DateTime UsedAt);

    private sealed class MaterialRow
    {
        public Guid MaterialId { get; init; }
        public string? ExternalId { get; init; }
        public string? CustomCode { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Type { get; init; }
        public string? Unit { get; init; }
        public int SupplierCount { get; init; }
        public MaterialPurchaseStatus PurchaseStatus { get; init; }
        public string? PurchaseStatusReason { get; init; }
        public DateTime? PurchaseStatusEffectiveFrom { get; init; }
        public DateTime? ExpectedAvailableDate { get; init; }
    }
}
