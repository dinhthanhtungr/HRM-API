using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.Materials.Dtos;
using HRM.Domain.Enums.Materials;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using MediatR;

namespace HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;

public sealed class GetPurchasingMaterialsQuery : PaginationQuery, IRequest<PagedResult<PurchasingMaterialSummaryDto>>
{
    public Guid? CategoryId { get; init; }
    public bool IncludeInactive { get; init; }
    public Guid? SupplierId { get; init; }
    [EnumDataType(typeof(MaterialPriceReviewStatus))]
    public MaterialPriceReviewStatus? PriceStatus { get; init; }
    public int StaleAfterDays { get; init; } = 30;
    public int NormalizedStaleAfterDays => Math.Clamp(StaleAfterDays, 1, 365);
    [EnumDataType(typeof(MaterialPurchaseStatus))]
    public MaterialPurchaseStatus? PurchaseStatus { get; init; }
}
