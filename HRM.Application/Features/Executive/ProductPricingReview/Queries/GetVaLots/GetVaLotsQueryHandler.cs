using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetVaLots;

internal sealed class GetVaLotsQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetVaLotsQuery, OperationResult<PagedResult<PricingReviewVaLotDto>>>
{
    public Task<OperationResult<PagedResult<PricingReviewVaLotDto>>> Handle(
        GetVaLotsQuery request, CancellationToken cancellationToken)
        => reader.GetVaLotsAsync(
            request.ProductId, request.PageNumber, request.PageSize,
            request.SortBy, request.SortDirection, cancellationToken);
}

