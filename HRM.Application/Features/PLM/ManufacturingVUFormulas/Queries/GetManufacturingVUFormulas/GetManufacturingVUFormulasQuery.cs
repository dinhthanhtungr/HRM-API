using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using HRM.Domain.Enums.Manufacturings;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulas;

public sealed class GetManufacturingVUFormulasQuery
    : PaginationQuery,
      IRequest<OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>>
{
    public Guid? ProductId { get; set; }
    public ManufacturingProductOrder? Status { get; set; }
}
