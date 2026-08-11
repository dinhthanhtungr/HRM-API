using System.Text.Json;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

internal static class SampleRequestDataChangePayloadTypes
{
    public const string Request = "SampleRequestDataChangeRequest";
}

internal static class SampleRequestDataChangeStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string PartiallyProcessed = "PartiallyProcessed";
    public const string Mixed = "Mixed";
}

public sealed class SampleRequestDataChangePayload
{
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid RequestedByEmployeeId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? BaseUpdatedDate { get; set; }
    public List<SampleRequestDataChangeFieldPayload> Changes { get; set; } = [];

    public string Status => SampleRequestDataChangeStatusRules.GetOverallStatus(Changes);
}

public sealed class SampleRequestDataChangeFieldPayload
{
    public string FieldCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public JsonElement OldValue { get; set; }
    public JsonElement NewValue { get; set; }
    public string Status { get; set; } = SampleRequestDataChangeStatuses.Pending;
    public Guid? DecidedByEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}

internal static class SampleRequestDataChangeStatusRules
{
    public static string GetOverallStatus(IReadOnlyCollection<SampleRequestDataChangeFieldPayload> changes)
    {
        if (changes.Count == 0 || changes.All(IsPending))
        {
            return SampleRequestDataChangeStatuses.Pending;
        }

        if (changes.Any(IsPending))
        {
            return SampleRequestDataChangeStatuses.PartiallyProcessed;
        }

        if (changes.All(x => IsStatus(x, SampleRequestDataChangeStatuses.Approved)))
        {
            return SampleRequestDataChangeStatuses.Approved;
        }

        if (changes.All(x => IsStatus(x, SampleRequestDataChangeStatuses.Rejected)))
        {
            return SampleRequestDataChangeStatuses.Rejected;
        }

        return SampleRequestDataChangeStatuses.Mixed;
    }

    public static bool IsPending(SampleRequestDataChangeFieldPayload change)
        => IsStatus(change, SampleRequestDataChangeStatuses.Pending);

    private static bool IsStatus(SampleRequestDataChangeFieldPayload change, string status)
        => string.Equals(change.Status, status, StringComparison.OrdinalIgnoreCase);
}
