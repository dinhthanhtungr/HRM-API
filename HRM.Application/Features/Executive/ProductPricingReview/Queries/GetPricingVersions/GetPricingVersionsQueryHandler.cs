using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingVersions;

internal sealed class GetPricingVersionsQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetPricingVersionsQuery, OperationResult<PagedResult<PricingReviewVersionDto>>>
{
    public Task<OperationResult<PagedResult<PricingReviewVersionDto>>> Handle(
        GetPricingVersionsQuery request, CancellationToken cancellationToken)
        => reader.GetVersionsAsync(
            request.ProductId, request.Currency, request.PageNumber, request.PageSize,
            request.SortBy, request.SortDirection, cancellationToken);
}

