using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentOptions;

public sealed record GetManufacturingEquipmentOptionsQuery(
    string? Keyword,
    string? GroupType,
    string? AreaExternalId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ManufacturingEquipmentOptionDto>>
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}
