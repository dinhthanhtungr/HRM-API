using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductPricingSourceQueryService
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProductPricingRealtimeSourceQueryService _realtimeSourceQueryService;

    public ProductPricingSourceQueryService(
        IPLMReadDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ProductPricingRealtimeSourceQueryService realtimeSourceQueryService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _realtimeSourceQueryService = realtimeSourceQueryService;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        bool includeSensitivePricing,
        CancellationToken cancellationToken)
    {
        if (includeSensitivePricing)
        {
            return await _realtimeSourceQueryService.LoadAsync(
                productIds,
                companyId,
                cancellationToken);
        }

        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>();
        }

        var formulaSources = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                ProductPricingSourceRules.EligibleFormulaStatuses.Contains(x.Status))
            .Select(x => new ProductPricingSourceRow
            {
                ProductId = x.ProductId,
                SourceType = ProductPricingSourceType.Formula,
                SourceId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                IsEligible = true,
                IsCustomerSelected = x.IsSelect,
                MaterialCostSnapshot = includeSensitivePricing ? x.TotalPrice : null,
                UpdatedDate = x.UpdatedDate ?? x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        var manufacturingSources = await _dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.ValidFrom <= now &&
                (!x.ValidTo.HasValue || x.ValidTo >= now) &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.CompanyId == companyId &&
                (ProductPricingSourceRules.EligibleManufacturingFormulaStatuses.Contains(
                     x.ManufacturingFormula.Status) ||
                 x.ManufacturingFormula.ManufacturingFormulaVersions.Any(version =>
                     version.Status == ProductPricingSourceRules.ReleasedManufacturingVersionStatus)))
            .Select(x => new ProductPricingSourceRow
            {
                ProductId = x.ProductId,
                SourceType = ProductPricingSourceType.ManufacturingFormula,
                SourceId = x.ManufacturingFormulaId!.Value,
                ExternalId = x.ManufacturingFormula!.ExternalId,
                Name = x.ManufacturingFormula.Name,
                Status = x.ManufacturingFormula.Status,
                IsEligible = true,
                IsCustomerSelected = true,
                MaterialCostSnapshot = includeSensitivePricing
                    ? x.ManufacturingFormula.TotalPrice
                    : null,
                UpdatedDate = x.ManufacturingFormula.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return formulaSources
            .Concat(manufacturingSources)
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProductPricingSourceOptionDto>)group
                    .GroupBy(x => new { x.SourceType, x.SourceId })
                    .Select(x => x.First())
                    .OrderBy(x => x.SourceType)
                    .ThenByDescending(x => x.UpdatedDate)
                    .ThenBy(x => x.ExternalId)
                    .Select(x => new ProductPricingSourceOptionDto
                    {
                        SourceType = x.SourceType,
                        SourceId = x.SourceId,
                        ExternalId = x.ExternalId,
                        Name = x.Name,
                        Status = x.Status,
                        IsEligible = x.IsEligible,
                        IsCustomerSelected = x.IsCustomerSelected,
                        MaterialCostSnapshot = x.MaterialCostSnapshot,
                        UpdatedDate = x.UpdatedDate
                    })
                    .ToArray());
    }

    private sealed class ProductPricingSourceRow
    {
        public Guid ProductId { get; init; }
        public ProductPricingSourceType SourceType { get; init; }
        public Guid SourceId { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool IsEligible { get; init; }
        public bool IsCustomerSelected { get; init; }
        public decimal? MaterialCostSnapshot { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }
}
