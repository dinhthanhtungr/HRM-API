using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricing;

/// <summary>
/// Chọn một công thức ưu tiên của sản phẩm và tính giá preview từ giá NVL mới nhất.
/// </summary>
internal sealed class GetQuotationProductPricingQueryHandler
    : IRequestHandler<GetQuotationProductPricingQuery,
        OperationResult<QuotationProductPricingLinePreviewDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly QuotationProductTierPricingResolver _tierPricingResolver;
    private readonly QuotationManualPricingTemplateResolver _manualPricingResolver;

    public GetQuotationProductPricingQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        QuotationProductTierPricingResolver tierPricingResolver,
        QuotationManualPricingTemplateResolver manualPricingResolver)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _tierPricingResolver = tierPricingResolver;
        _manualPricingResolver = manualPricingResolver;
    }

    public async Task<OperationResult<QuotationProductPricingLinePreviewDto>> Handle(
        GetQuotationProductPricingQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                "ProductId is required.");
        }

        var normalizedCurrency = QuotationRules.TrimToNull(request.Currency) ?? "VND";
        var currency = normalizedCurrency.ToUpperInvariant();
        //if (normalizedCurrency is null)
        //{
        //    return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
        //        "Currency is required.");
        //}

        //var currency = normalizedCurrency.ToUpperInvariant();
        if (currency.Length > QuotationRules.MaximumCurrencyLength)
        {
            return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                $"Currency cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                "CustomerId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive)
            .Select(x => new
            {
                ProductCode = x.ColourCode ?? x.Code ?? string.Empty,
                ProductName = x.Name ?? string.Empty,
                Unit = x.Unit ?? string.Empty,
                x.ColourCode,
                x.Code,
                x.Additive,
                x.CategoryId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                "Product was not found in the current company.");
        }

        if (request.CustomerId.HasValue)
        {
            var customerIsVisible = await _visibilityService
                .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == request.CustomerId.Value, cancellationToken);
            if (!customerIsVisible)
            {
                return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                    "Customer was not found or is outside your visibility scope.");
            }
        }

        var visibleQuotations = _visibilityService.ApplyQuotationVisibility(
            _dbContext.Quotations.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);
        var referencesByProduct = await _tierPricingResolver.ResolveAsync(
            [request.ProductId],
            scope.CompanyId,
            currency,
            request.CustomerId,
            null,
            visibleQuotations,
            cancellationToken);
        if (!referencesByProduct.TryGetValue(request.ProductId, out var references))
        {
            return OperationResult<QuotationProductPricingLinePreviewDto>.Fail(
                "Product was not found in the current company.");
        }

        var hasApprovedPricing = references.ApprovedPricing is { PriceTiers.Count: > 0 };
        var hasSystemPricing = references.SystemCalculatedPricing is { PriceTiers.Count: > 0 };
        var manualPricing = hasApprovedPricing || hasSystemPricing
            ? null
            : await _manualPricingResolver.ResolveAsync(
                request.ProductId,
                scope.CompanyId,
                product.CategoryId,
                product.ColourCode,
                product.Code,
                product.Additive,
                currency,
                cancellationToken);
        var availability = hasApprovedPricing
            ? HRM.Domain.Enums.CustomerEnum.QuotationPricingAvailability.ApprovedPricingAvailable
            : hasSystemPricing
                ? HRM.Domain.Enums.CustomerEnum.QuotationPricingAvailability.SystemCalculatedAvailable
                : references.LatestQuotedPricing is { PriceTiers.Count: > 0 }
                    ? HRM.Domain.Enums.CustomerEnum.QuotationPricingAvailability.LatestQuotedPriceOnly
                    : manualPricing!.Availability;
        var warningCode = hasApprovedPricing || hasSystemPricing
            ? null
            : references.LatestQuotedPricing is { PriceTiers.Count: > 0 }
                ? "LATEST_QUOTED_PRICE_ONLY"
                : manualPricing!.WarningCode;
        var warningMessage = hasApprovedPricing || hasSystemPricing
            ? null
            : references.LatestQuotedPricing is { PriceTiers.Count: > 0 }
                ? "Only a previous customer quotation is available. Enter and confirm the price for this quotation."
                : manualPricing!.WarningMessage;

        return OperationResult<QuotationProductPricingLinePreviewDto>.Ok(
            QuotationProductPricingPreviewMapper.Map(
                request.ProductId,
                product.ProductCode,
                product.ProductName,
                product.Unit,
                currency,
                references,
                availability,
                manualPricing?.CanUseManualCustomerPrice ?? true,
                warningCode,
                warningMessage,
                manualPricing?.PriceTiers ?? []));
    }
}
