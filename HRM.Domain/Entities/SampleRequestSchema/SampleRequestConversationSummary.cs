namespace HRM.Domain.Entities.SampleRequestSchema;

/// <summary>Current General-visible projection; never a message or notification receipt.</summary>
public sealed class SampleRequestConversationSummary
{
    public Guid CompanyId { get; set; }
    public Guid SampleRequestId { get; set; }
    public string? ContentJson { get; set; }
    public int FormatVersion { get; set; }
    public string? AppliedSourceFingerprint { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? LastErrorCode { get; set; }
}
