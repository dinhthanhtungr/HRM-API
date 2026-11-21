using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;

internal sealed class GetSampleRequestSummarySuggestionsQueryHandler(
    IPLMReadDbContext db, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleRequestSummarySuggestionsQuery, IReadOnlyList<SampleRequestSummarySuggestionDto>>
{
    public async Task<IReadOnlyList<SampleRequestSummarySuggestionDto>> Handle(
        GetSampleRequestSummarySuggestionsQuery request, CancellationToken ct)
    {
        var q = request.Q.Trim();
        var input = SampleRequestSearchInput.Parse(q);
        if (input.Keyword.Length < 2 || q.Length > 100) return [];
        using var timings = SampleWorkDiagnosticTags.Start("summary_suggestions");
        var visible = await SampleRequestSummaryQueryRules.BuildAsync(db, visibility, request, ct);
        timings?.Mark("scope");
        var query = SummarySuggestionQueries.Build(db, visible, input);
        timings?.Mark("build");
        var candidates = await query.ToListAsync(ct);
        timings?.Mark("query");
        var result = SummarySuggestionSelection.Select(candidates).Select(x => new SampleRequestSummarySuggestionDto(
            x.Type, x.Value, x.Label, string.IsNullOrEmpty(x.Code) ? null : x.Code, x.MatchRank)).ToArray();
        timings?.Mark("map");
        return result;
    }
}
