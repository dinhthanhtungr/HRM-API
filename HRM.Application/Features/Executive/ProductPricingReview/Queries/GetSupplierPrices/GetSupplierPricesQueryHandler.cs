using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetSupplierPrices;

internal sealed class GetSupplierPricesQueryHandler(ProductPricingReviewMaterialReader reader)
    : IRequestHandler<GetSupplierPricesQuery, OperationResult<PricingReviewSupplierPricesDto>>
{
    public Task<OperationResult<PricingReviewSupplierPricesDto>> Handle(
        GetSupplierPricesQuery request, CancellationToken cancellationToken)
        => reader.GetSupplierPricesAsync(
            request.ProductId, request.MaterialId, request.Currency, cancellationToken);
}

