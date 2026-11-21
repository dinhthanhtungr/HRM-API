using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;

public sealed class GetSampleRequestSummarySuggestionsQuery : SampleRequestSummaryFilters,
    IRequest<IReadOnlyList<SampleRequestSummarySuggestionDto>>
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Q { get; set; } = string.Empty;
}
