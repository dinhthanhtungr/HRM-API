using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingSourceOptions;

public sealed record GetPricingSourceOptionsQuery(
    Guid ProductId,
    PricingReviewSourceType? SourceType,
    string? Keyword,
    string? Currency,
    int PageNumber = 1,
    int PageSize = 5) : IRequest<OperationResult<PagedResult<PricingReviewSourceOptionDto>>>;
