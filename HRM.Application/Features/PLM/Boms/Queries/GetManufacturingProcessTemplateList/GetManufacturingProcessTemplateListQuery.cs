using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateList;

public sealed class GetManufacturingProcessTemplateListQuery
    : PaginationQuery, IRequest<PagedResult<ManufacturingProcessTemplateListItemDto>>
{
    public ManufacturingTemplateStatus? Status { get; init; }
}
