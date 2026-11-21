namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;

public sealed record SampleRequestSummarySuggestionDto(
    string Type, string Value, string Label, string? Code = null, int MatchRank = 2);
