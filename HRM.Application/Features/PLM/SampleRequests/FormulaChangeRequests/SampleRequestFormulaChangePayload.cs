namespace HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;

internal static class SampleRequestFormulaChangePayloadTypes
{
    public const string Request = "SampleRequestFormulaChangeRequest";
}

internal static class SampleRequestFormulaChangeStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public sealed class SampleRequestFormulaChangePayload
{
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid CurrentFormulaId { get; set; }
    public string CurrentFormulaExternalId { get; set; } = string.Empty;
    public Guid RequestedFormulaId { get; set; }
    public string RequestedFormulaExternalId { get; set; } = string.Empty;
    public Guid RequestedByEmployeeId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string Status { get; set; } = SampleRequestFormulaChangeStatuses.Pending;
    public Guid? DecidedByEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}
