using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;

public sealed record SampleRequestTrackingDto(
    Guid SampleRequestId,
    string ExternalId,
    string? ProductCode,
    string? ProductName,
    string Status,
    DateTime? ExpectedDeliveryDate)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SampleRequestTrackingMilestoneDto>? Milestones { get; init; }
}

public sealed record SampleRequestTrackingMilestoneDto(string Status, DateTime At);
