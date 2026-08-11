using HRM.Domain.Enums.Logs;

namespace HRM.Application.Features.Timeline.Dtos;

public sealed class EventLogCreateRequest
{
    public Guid EmployeeId { get; init; }
    public Guid SourceId { get; init; }
    public string? SourceCode { get; init; }
    public EventType EventType { get; init; }
    public string? Status { get; init; }
    public string? Note { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? SourceType { get; init; }
    public string? ParentSourceType { get; init; }
    public Guid? ParentSourceId { get; init; }
    public string? PayloadJson { get; init; }
    public DateTime? CreatedDate { get; init; }
}

public sealed class TimelineItemDto
{
    public string? SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public EventType EventType { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTime CreatedDate { get; init; }
    public Guid CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public Guid CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string? PayloadJson { get; init; }
}
