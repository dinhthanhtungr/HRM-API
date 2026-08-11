using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;

/// <summary>
/// Chọn một công thức ưu tiên của sản phẩm và tính giá preview từ giá NVL mới nhất.
/// </summary>
internal sealed class GetQuotationProductPricingQueryHandler
    : IRequestHandler<GetQuotationProductPricingQuery,
        OperationResult<QuotationResolvedProductPricingDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly QuotationCurrentPricingResolver _pricingResolver;

    public GetQuotationProductPricingQueryHandler(
        ICurrentUser currentUser,
        QuotationCurrentPricingResolver pricingResolver)
    {
        _currentUser = currentUser;
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

        var resolvedByProductId = await _pricingResolver.ResolveAsync(
            [request.ProductId],
            companyId,
            cancellationToken);
        if (!resolvedByProductId.TryGetValue(request.ProductId, out var current))
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                "Product was not found in the current company.");
        }

        if (!current.FormulaId.HasValue ||
            !current.FormulaSelectionSource.HasValue)
        {
            return OperationResult<QuotationResolvedProductPricingDto>.Fail(
                "No selected formula or formula from a sent sample request was found for this product.");
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
                FormulaId = current.FormulaId.Value,
                FormulaExternalId = current.FormulaExternalId ?? string.Empty,
                FormulaName = current.FormulaName ?? string.Empty,
                FormulaSelectionSource = current.FormulaSelectionSource.Value,
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
                    ? current.ManufacturingCost
                    : null,
                StandardSellingPrice = current.StandardSellingPrice,
                ProfitMarginRate = canViewSensitivePricing
                    ? current.Pricing?.ProfitMarginRate
                    : null,
                PricingUpdatedDate = canViewSensitivePricing
                    ? current.PricingUpdatedDate
                    : null,
                Pricing = canViewSensitivePricing ? current.Pricing : null
            });
    }
}
