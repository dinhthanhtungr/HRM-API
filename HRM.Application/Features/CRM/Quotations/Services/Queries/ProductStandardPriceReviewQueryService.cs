using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Products;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services.Queries;

/// <summary>
/// Batched, company-scoped read of the live standard-price review state for
/// quotation screens. It never changes a quotation or its price snapshots.
/// </summary>
internal sealed class ProductStandardPriceReviewQueryService
{
    private const string Currency = "VND";
    private readonly ICRMReadDbContext _dbContext;
    private readonly IDateTimeProvider _clock;
    private readonly QuotationFeatureOptions _options;

    public ProductStandardPriceReviewQueryService(
        ICRMReadDbContext dbContext,
        IDateTimeProvider clock,
        QuotationFeatureOptions options)
    {
        _dbContext = dbContext;
        _clock = clock;
        _options = options;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductStandardPriceStateResult>> LoadAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, ProductStandardPriceStateResult>();
        }

        var approvedRows = await _dbContext.ProductPricingVersions.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == Currency &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice > 0m)
            .ToListAsync(cancellationToken);
        var approvedByProduct = approvedRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.Version)
                    .ThenByDescending(y => y.ApprovedAt ?? y.UpdatedDate ?? y.CreatedDate)
                    .First());

        var confirmations = await _dbContext.Formulas.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                productIds.Contains(x.ProductId) &&
                x.CheckDate.HasValue &&
                x.Status != FormulaStatus.Cancelled.ToString() &&
                x.Status != FormulaStatus.Rejected.ToString())
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, ConfirmedAt = x.Max(y => y.CheckDate) })
            .ToListAsync(cancellationToken);
        var confirmationByProduct = confirmations
            .Where(x => x.ConfirmedAt.HasValue)
            .ToDictionary(x => x.ProductId, x => x.ConfirmedAt!.Value);

        return productIds.Distinct().ToDictionary(
            productId => productId,
            productId => ProductStandardPriceStateResolver.Resolve(
                approvedByProduct.GetValueOrDefault(productId),
                confirmationByProduct.GetValueOrDefault(productId),
                _clock.Now,
                _options));
    }
}
