using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetPricingVersions;

public sealed record GetPricingVersionsQuery(
    Guid ProductId,
    string? Currency,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null)
    : IRequest<OperationResult<PagedResult<PricingReviewVersionDto>>>;

