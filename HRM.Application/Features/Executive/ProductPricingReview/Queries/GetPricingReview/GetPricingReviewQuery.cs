using HRM.Application.Commons.Models;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingReview;

public sealed record GetPricingReviewQuery(
    Guid ProductId,
    string? Currency,
    Guid? QuotationId,
    PricingReviewSourceType? SourceType,
    Guid? SourceId) : IRequest<OperationResult<ProductPricingReviewDto>>;

