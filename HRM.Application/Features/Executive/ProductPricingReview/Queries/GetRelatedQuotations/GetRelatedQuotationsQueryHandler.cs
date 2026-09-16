using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetRelatedQuotations;

internal sealed class GetRelatedQuotationsQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetRelatedQuotationsQuery, OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>>
{
    public Task<OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>> Handle(
        GetRelatedQuotationsQuery request, CancellationToken cancellationToken)
        => reader.GetRelatedQuotationsAsync(
            request.ProductId, request.PageNumber, request.PageSize,
            request.SortBy, request.SortDirection, cancellationToken);
}

