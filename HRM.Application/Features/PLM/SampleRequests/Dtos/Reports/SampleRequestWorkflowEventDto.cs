using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed record SampleRequestWorkflowEventDto(Guid Id, string EventType, DateTime? OccurredAt,
    DateTime RecordedAt, string? RecordedByName, string? Note, Guid? FormulaId,
    Guid? SampleRequestSampleTrialId, Guid? ConversationId, Guid? MessageId, Guid? NotificationId,
    string NotificationStatus, bool CanRetryNotification, int? TrialNo = null, string? FormulaExternalId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool IsNoteHidden = false);
