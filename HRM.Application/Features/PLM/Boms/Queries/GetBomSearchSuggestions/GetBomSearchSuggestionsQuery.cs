using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomSearchSuggestions;

public sealed class GetBomSearchSuggestionsQuery : PaginationQuery, IRequest<IReadOnlyList<SmartSearchSuggestion>>
{
    public Guid? ProductId { get; init; }
    // Controller luôn đặt theo route; không tin giá trị BomType từ client.
    public BomType BomType { get; set; }
}
