namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

public sealed record SamplePersonalWorkDto(Guid SampleRequestId, bool IsPinned, bool NotifyOnDue,
    Guid? TaskId, string? Note, DateTime? DueDate, DateTime? UpdatedDate, DateTime? PinnedAt = null);

public sealed class SaveSamplePersonalWorkRequest
{
    public bool? IsPinned { get; init; }
    public bool? NotifyOnDue { get; init; }
    public bool SaveNote { get; init; }
    public Guid? TaskId { get; init; }
    public string? Note { get; init; }
    public bool UpdateDueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed class SamplePersonalWorkBatchRequest
{
    public Guid[] SampleRequestIds { get; init; } = [];
}
