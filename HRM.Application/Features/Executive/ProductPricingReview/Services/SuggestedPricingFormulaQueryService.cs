using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Products;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

/// <summary>
/// Chọn công thức nên dùng theo cùng một luật cho drawer Pricing Review và các màn overview.
/// Mỗi loại candidate cung cấp mốc nghiệp vụ riêng; tất cả candidate được xếp chung theo mốc đó.
/// </summary>
internal sealed class SuggestedPricingFormulaQueryService
{
    private readonly IPLMReadDbContext _dbContext;

    public SuggestedPricingFormulaQueryService(IPLMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, SuggestedPricingFormulaCandidate>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var normalizedProductIds = productIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (normalizedProductIds.Length == 0)
        {
            return new Dictionary<Guid, SuggestedPricingFormulaCandidate>();
        }

        // These queries share the scoped EF context, so execute sequentially.
        var vuCandidates = await _dbContext.Formulas.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                normalizedProductIds.Contains(x.ProductId) &&
                x.IsActive &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive &&
                x.SentDate.HasValue &&
                x.Status != FormulaStatus.Cancelled.ToString() &&
                x.Status != FormulaStatus.Rejected.ToString())
            .Select(x => new SuggestedPricingFormulaCandidate
            {
                ProductId = x.ProductId,
                SourceType = PricingReviewSourceType.VU,
                SourceId = x.FormulaId,
                SourceCode = x.ExternalId,
                SourceName = x.Name,
                SourceNote = x.Note,
                Status = x.Status,
                IsEligible = ProductPricingReviewRules.EligibleVuFormulaStatuses.Contains(x.Status),
                CreatedAt = x.CreatedDate,
                PriorityAt = x.SentDate!.Value,
                PriorityDateSource = SuggestedFormulaPriorityDateSource.SentDate
            })
            .ToListAsync(cancellationToken);

        var producedVaCandidates = await _dbContext.ProductionSelectVersions.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.CompanyId == companyId &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.Status == ManufacturingProductOrderFormula.Checking.ToString() &&
                x.MfgProductionOrder.CompanyId == companyId &&
                normalizedProductIds.Contains(x.MfgProductionOrder.ProductId) &&
                x.MfgProductionOrder.IsActive &&
                x.MfgProductionOrder.Product.IsActive)
            .Select(x => new SuggestedPricingFormulaCandidate
            {
                ProductId = x.MfgProductionOrder.ProductId,
                SourceType = PricingReviewSourceType.VA,
                SourceId = x.ManufacturingFormulaId!.Value,
                SourceCode = x.ManufacturingFormula!.ExternalId,
                SourceName = x.ManufacturingFormula.Name,
                SourceNote = x.ManufacturingFormula.Note,
                Status = x.ManufacturingFormula.Status,
                IsEligible = true,
                CreatedAt = x.ManufacturingFormula.CreatedDate,
                PriorityAt = x.MfgProductionOrder.CreatedDate,
                PriorityDateSource = SuggestedFormulaPriorityDateSource.ProductionOrderCreatedDate
            })
            .ToListAsync(cancellationToken);

        return vuCandidates
            .Concat(producedVaCandidates)
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(candidate => candidate.PriorityAt)
                    .ThenByDescending(candidate => candidate.SourceId)
                    .First());
    }
}

internal sealed class SuggestedPricingFormulaCandidate
{
    public Guid ProductId { get; init; }
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string? SourceNote { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsEligible { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime PriorityAt { get; init; }
    public SuggestedFormulaPriorityDateSource PriorityDateSource { get; init; }
}

internal enum SuggestedFormulaPriorityDateSource
{
    SentDate = 0,
    ProductionOrderCreatedDate = 10
}
