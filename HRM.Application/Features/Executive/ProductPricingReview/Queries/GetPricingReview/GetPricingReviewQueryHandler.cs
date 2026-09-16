using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingReview;

internal sealed class GetPricingReviewQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetPricingReviewQuery, OperationResult<ProductPricingReviewDto>>
{
    public Task<OperationResult<ProductPricingReviewDto>> Handle(
        GetPricingReviewQuery request, CancellationToken cancellationToken)
        => reader.GetReviewAsync(
            request.ProductId, request.Currency, request.QuotationId,
            request.SourceType, request.SourceId, cancellationToken);
}

