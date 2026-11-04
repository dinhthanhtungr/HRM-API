using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

/// <summary>Historical customer feedback; explicit shared companion notes are public in record scope, private price text requires approved-selling-price visibility.</summary>
public sealed record SampleFeedbackSummaryDto(string Kind, string Outcome, DateTime OccurredAt, string? Note = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool IsNoteHidden = false)
{
    [JsonIgnore] internal bool IsSharedNote { get; init; }
}
