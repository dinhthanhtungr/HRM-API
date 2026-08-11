using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Dtos.Lookup;
using HRM.Domain.Enums.Formulas;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;

/// <summary>
/// Tra cứu NVL và Product hợp lệ để thêm vào công thức.
/// </summary>
public sealed class GetFormulaItemLookupQuery
    : PaginationQuery, IRequest<PagedResult<FormulaItemLookupDto>>
{
    public ItemType? ItemType { get; init; }
    public Guid? CategoryId { get; init; }
}
