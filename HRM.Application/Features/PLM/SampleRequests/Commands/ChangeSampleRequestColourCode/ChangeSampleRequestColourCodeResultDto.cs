namespace HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;

public sealed class ChangeSampleRequestColourCodeResultDto
{
    public Guid SampleRequestId { get; init; }
    public Guid ProductId { get; init; }
    public string OldColourCode { get; init; } = string.Empty;
    public string NewColourCode { get; init; } = string.Empty;
    public int UpdatedDraftTrialCount { get; init; }
    public int UpdatedConversationCount { get; init; }
    public int UpdatedMessageCount { get; init; }
    public int UpdatedNotificationCount { get; init; }
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public Guid NotificationId { get; init; }
}
