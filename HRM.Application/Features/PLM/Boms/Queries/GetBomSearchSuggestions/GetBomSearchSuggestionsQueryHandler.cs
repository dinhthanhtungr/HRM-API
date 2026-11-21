using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetBoms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomSearchSuggestions;

internal sealed class GetBomSearchSuggestionsQueryHandler(IPLMReadDbContext db, ICurrentUser user)
    : IRequestHandler<GetBomSearchSuggestionsQuery, IReadOnlyList<SmartSearchSuggestion>>
{
    public async Task<IReadOnlyList<SmartSearchSuggestion>> Handle(GetBomSearchSuggestionsQuery request, CancellationToken ct)
    {
        if (user.CompanyId is not { } companyId || companyId == Guid.Empty) return [];
        var rows = BomSearchQueryRules.Build(db, companyId, request.ProductId, request.BomType, request.Keyword, request, out var schema);
        return (await schema.SuggestionsAsync<BomListItemDto>(rows, request, ct))?.SearchSuggestions ?? [];
    }
}
