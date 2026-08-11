using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Formulas.Dtos.Lookup;
using HRM.Domain.Enums.Formulas;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup;

/// <summary>
/// Lookup công thức nhẹ cho FE chọn, có thể lấy từ Formula VU hoặc ManufacturingFormula VA.
/// </summary>
public sealed class GetFormulaLookupQuery
    : PaginationQuery, IRequest<PagedResult<FormulaLookupDto>>
{
    public Guid? ProductId { get; init; }
    public Guid? SampleRequestId { get; init; }
    public string? Status { get; init; }
    public FormulaSource? SourceType { get; init; }
}
