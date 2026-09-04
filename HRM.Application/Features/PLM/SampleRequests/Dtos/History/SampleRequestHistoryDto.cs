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
    public IReadOnlyList<SampleRequestHistoryDetailDto> Details { get; set; } = [];
}

/// <summary>
/// Read model cho màn hình lịch sử thay đổi của một yêu cầu phối mẫu.
/// Timeline giữ từng lần lưu, còn Fields được tổng hợp để dựng chế độ xem theo trường.
/// </summary>
public sealed class SampleRequestHistoryResponseDto
{
    public SampleRequestHistoryDto? LatestChange { get; init; }
    public IReadOnlyList<SampleRequestHistoryFieldDto> Fields { get; init; } = [];
    public IReadOnlyList<SampleRequestHistoryDto> Timeline { get; init; } = [];
}

public sealed class SampleRequestHistoryFieldDto
{
    public string Source { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string? InitialValue { get; init; }
    public string? CurrentValue { get; init; }
    public int ChangeCount { get; init; }
    public DateTime LastChangedAt { get; init; }
    public string? LastChangedByName { get; init; }
}

public sealed class SampleRequestHistoryDetailDto
{
    public string Source { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
