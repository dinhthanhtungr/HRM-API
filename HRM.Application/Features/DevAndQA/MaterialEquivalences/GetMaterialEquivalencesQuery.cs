using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.WareHouses;
using MediatR;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public sealed class GetMaterialEquivalencesQuery : PaginationQuery, IRequest<PagedResult<MaterialEquivalenceDto>?>
{
    public StockType[] StockTypes { get; init; } = [];
    public bool OnlyAvailableLeZero { get; init; }
    public decimal? AvailableMax { get; init; }
    public string? ProcessingType { get; init; }
    public decimal? ProductOnHandMin { get; init; }
    public decimal? ProductOnHandMax { get; init; }
}
