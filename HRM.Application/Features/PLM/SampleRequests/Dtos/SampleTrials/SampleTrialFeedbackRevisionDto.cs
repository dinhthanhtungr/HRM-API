using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

public sealed record SampleTrialFeedbackRevisionDto(Guid Id, DateTime ChangedAt, string? ChangedByName,
    DateTime? OldDate, string? OldNote, DateTime? NewDate, string? NewNote)
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public HRM.Domain.Enums.SampleRequests.SampleTrialStatus? OldStatus { get; init; }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public HRM.Domain.Enums.SampleRequests.SampleTrialStatus? NewStatus { get; init; }
    public string? CorrectionReason { get; init; }
}

public sealed record SampleTrialFeedbackRevisionsDto(Guid TrialId, int TrialNo,
    DateTime? CustomerReplyDate, string? CustomerReplyNote, DateTime? SentDate, DateTime? TrialUpdatedDate,
    DateTime? RequestUpdatedDate, bool CanEdit, PagedResult<SampleTrialFeedbackRevisionDto> History)
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public HRM.Domain.Enums.SampleRequests.SampleTrialStatus Status { get; init; }
    public bool CanEditResult { get; init; }
    public DateTime? FormulaUpdatedDate { get; init; }
}
