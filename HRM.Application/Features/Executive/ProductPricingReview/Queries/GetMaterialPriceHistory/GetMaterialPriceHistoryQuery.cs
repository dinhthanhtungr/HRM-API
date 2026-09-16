using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.ProductPricingReview.Queries.GetMaterialPriceHistory;

public sealed record GetMaterialPriceHistoryQuery(
    Guid ProductId,
    Guid MaterialId,
    string? Currency,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null)
    : IRequest<OperationResult<PagedResult<PricingReviewMaterialPriceHistoryDto>>>;
