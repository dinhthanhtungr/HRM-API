using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPricePreview;

public sealed record GetMaterialPricePreviewQuery(
    Guid ProductId,
    PricingReviewSourceType SourceType,
    Guid SourceId,
    string? Currency,
    int Limit = 8,
    string? SortBy = "absoluteDifference",
    string? SortDirection = "desc")
    : IRequest<OperationResult<PricingReviewMaterialPricePreviewDto>>;
