using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPricePreview;

internal sealed class GetMaterialPricePreviewQueryHandler(ProductPricingReviewReader reader)
    : IRequestHandler<GetMaterialPricePreviewQuery, OperationResult<PricingReviewMaterialPricePreviewDto>>
{
    public Task<OperationResult<PricingReviewMaterialPricePreviewDto>> Handle(
        GetMaterialPricePreviewQuery request,
        CancellationToken cancellationToken)
        => reader.GetMaterialPricePreviewAsync(
            request.ProductId,
            request.SourceType,
            request.SourceId,
            request.Currency,
            request.Limit,
            request.SortBy,
            request.SortDirection,
            cancellationToken);
}
