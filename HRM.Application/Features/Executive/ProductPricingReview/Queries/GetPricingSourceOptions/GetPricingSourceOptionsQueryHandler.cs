using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingSourceOptions;

internal sealed class GetPricingSourceOptionsQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetPricingSourceOptionsQuery, OperationResult<PagedResult<PricingReviewSourceOptionDto>>>
{
    public Task<OperationResult<PagedResult<PricingReviewSourceOptionDto>>> Handle(
        GetPricingSourceOptionsQuery request, CancellationToken cancellationToken)
        => reader.GetSourceOptionsAsync(
            request.ProductId, request.SourceType, request.Keyword,
            request.Currency, request.PageNumber, request.PageSize, cancellationToken);
}
