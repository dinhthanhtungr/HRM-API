using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

public sealed record SampleTrialFeedbackRevisionDto(Guid Id, DateTime ChangedAt, string? ChangedByName,
    DateTime? OldDate, string? OldNote, DateTime? NewDate, string? NewNote);

public sealed record SampleTrialFeedbackRevisionsDto(Guid TrialId, int TrialNo,
    DateTime? CustomerReplyDate, string? CustomerReplyNote, DateTime? SentDate, DateTime? TrialUpdatedDate,
    DateTime? RequestUpdatedDate, bool CanEdit, PagedResult<SampleTrialFeedbackRevisionDto> History);
