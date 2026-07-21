namespace HRM.Application.Features.PLM.SampleRequests.Dtos.History;

public sealed class SampleRequestHistoryDto
{
    public Guid AuditLogId { get; init; }
    public string Source { get; init; } = string.Empty;
    public IReadOnlyList<string> Sources { get; init; } = [];
    public Guid RecordId { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public Guid? ChangedBy { get; init; }
    public string? ChangedByName { get; init; }
    public DateTime ChangedAt { get; init; }
    public string? Reason { get; init; }
    public Guid? CorrelationId { get; init; }
    public IReadOnlyList<SampleRequestHistoryDetailDto> Details { get; init; } = [];
}

public sealed class SampleRequestHistoryDetailDto
{
    public string Source { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
