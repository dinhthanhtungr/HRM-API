using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;

/// <summary>
/// Chọn một công thức ưu tiên của sản phẩm và tính giá preview từ giá NVL mới nhất.
/// </summary>
internal sealed class GetQuotationProductPricingQueryHandler
    : IRequestHandler<GetQuotationProductPricingQuery,
        OperationResult<QuotationResolvedProductPricingDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ICRMReadDbContext _dbContext;
    private readonly QuotationCurrentPricingResolver _pricingResolver;

    public GetQuotationProductPricingQueryHandler(
        ICurrentUser currentUser,
        ICRMReadDbContext dbContext,
        QuotationCurrentPricingResolver pricingResolver)
    {
        _currentUser = currentUser;
        _dbContext = dbContext;
        _pricingResolver = pricingResolver;
    }

    public async Task<OperationResult<QuotationResolvedProductPricingDto>> Handle(
        GetQuotationProductPricingQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                "ProductId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                "Current user does not have a company context.");
        }

        var currency = QuotationRules.TrimToNull(request.Currency)?.ToUpperInvariant() ?? "VND";
        if (currency.Length > QuotationRules.MaximumCurrencyLength)
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                $"Currency cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        var approved = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Include(x => x.PriceTiers)
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == request.ProductId &&
                x.Currency == currency &&
                x.Status == ProductPricingStatus.Approved &&
                x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var resolvedByProductId = await _pricingResolver.ResolveAsync(
            [request.ProductId],
            companyId,
            cancellationToken);
        if (!resolvedByProductId.TryGetValue(request.ProductId, out var current))
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                "Product was not found in the current company.");
        }

        var canViewSensitivePricing =
            _currentUser.IsInRole(ApplicationRoles.President) ||
            _currentUser.IsInRole(ApplicationRoles.Developer);

        return OperationResult<QuotationResolvedProductPricingDto>.Ok(
            new QuotationResolvedProductPricingDto
            {
                ProductId = current.ProductId,
                ProductCode = current.ProductCode,
                ProductName = current.ProductName,
                ProductPricingVersionId = approved?.ProductPricingVersionId,
                ProductPricingVersion = approved?.Version,
                Currency = currency,
                ProductPricingStatus = approved?.Status,
                CanApplyToQuotation = approved is not null && approved.PriceTiers.Count > 0,
                ApprovedPriceTiers = approved?.PriceTiers
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new ProductPricingTierDto
                    {
                        ProductPricingTierId = x.ProductPricingTierId,
                        QuantityRangeLabel = x.QuantityRangeLabel,
                        MinQuantity = x.MinQuantity,
                        MaxQuantity = x.MaxQuantity,
                        MinInclusive = x.MinInclusive,
                        MaxInclusive = x.MaxInclusive,
                        UnitPrice = x.UnitPrice,
                        SortOrder = x.SortOrder
                    }).ToArray() ?? [],
                PricingSourceType = approved is null
                    ? null
                    : approved.SourceManufacturingFormulaId.HasValue
                        ? ProductPricingSourceType.ManufacturingFormula
                        : ProductPricingSourceType.Formula,
                PricingSourceId = approved?.SourceManufacturingFormulaId ??
                    approved?.SourceFormulaId,
                FormulaId = approved?.SourceFormulaId ?? current.FormulaId,
                FormulaExternalId = approved?.FormulaExternalIdSnapshot ?? current.FormulaExternalId ?? string.Empty,
                FormulaName = approved?.SourceManufacturingFormula?.Name ??
                    approved?.SourceFormula?.Name ??
                    current.FormulaName ??
                    string.Empty,
                FormulaSelectionSource = current.FormulaSelectionSource,
                RealtimeMaterialCost = canViewSensitivePricing
                    ? current.RealtimeMaterialCost.MaterialCost
                    : null,
                IsRealtimeMaterialCostComplete = canViewSensitivePricing
                    ? current.RealtimeMaterialCost.IsComplete
                    : null,
                MissingMaterialPriceCount = canViewSensitivePricing
                    ? current.RealtimeMaterialCost.MissingPriceCount
                    : null,
                ManufacturingCost = canViewSensitivePricing
                    ? approved?.ManufacturingCost ?? current.ManufacturingCost
                    : null,
                StandardSellingPrice = approved?.StandardSellingPrice,
                ProfitMarginRate = canViewSensitivePricing
                    ? approved?.ProfitMarginRate
                    : null,
                PricingUpdatedDate = canViewSensitivePricing
                    ? approved?.UpdatedDate ?? approved?.CreatedDate ?? current.PricingUpdatedDate
                    : null,
                Pricing = canViewSensitivePricing ? current.Pricing : null
            });
    }
}
