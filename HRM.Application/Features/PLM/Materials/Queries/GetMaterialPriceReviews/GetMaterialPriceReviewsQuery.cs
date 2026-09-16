using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviews;

/// <summary>
/// Liệt kê NVL cần rà giá theo mức độ sử dụng gần đây trong Formula và ManufacturingFormula.
/// </summary>
public sealed class GetMaterialPriceReviewsQuery
    : PaginationQuery, IRequest<PagedResult<MaterialPriceReviewItemDto>>
{
    public MaterialPriceReviewStatus? PriceStatus { get; init; }
    public int StaleAfterDays { get; init; } = 30;

    public int NormalizedStaleAfterDays => Math.Clamp(StaleAfterDays, 1, 365);
}
