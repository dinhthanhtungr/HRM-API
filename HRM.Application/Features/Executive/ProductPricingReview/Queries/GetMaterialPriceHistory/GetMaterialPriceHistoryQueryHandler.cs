using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPriceHistory;

internal sealed class GetMaterialPriceHistoryQueryHandler(ProductPricingReviewMaterialReader reader)
    : IRequestHandler<GetMaterialPriceHistoryQuery, OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>>
{
    public Task<OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>> Handle(
        GetMaterialPriceHistoryQuery request, CancellationToken cancellationToken)
        => reader.GetPriceHistoryAsync(
            request.ProductId, request.MaterialId, request.Currency,
            request.PageNumber, request.PageSize, request.SortBy, request.SortDirection, cancellationToken);
}
