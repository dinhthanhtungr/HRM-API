using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public sealed class GetMaterialDemandsQuery : PaginationQuery, IRequest<PagedResult<MaterialDemandDto>?>
{
    public string? ProcessingType { get; init; }
    public bool OnlyAvailableLeZero { get; init; }
    public decimal? AvailableMax { get; init; }
}
