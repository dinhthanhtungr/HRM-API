namespace HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;

/// <summary>
/// Metadata phu luu trong InternalMessage.PayloadJson va Notification.PayloadJson
/// de FE mo dung thread/message ma khong phai dua vao title/message text.
/// </summary>
internal sealed class SampleRequestThreadMessagePayload
{
    public string ContentType { get; init; } = "InternalMailMessage";

    public Guid? ConversationId { get; init; }

    public Guid? MessageId { get; init; }

    public Guid? SampleRequestId { get; init; }

    public string? ExternalId { get; init; }

    public string? Type { get; init; }

    public string? SaleMessage { get; init; }

    public bool IsUrgent { get; init; }

    public Guid? ReplyToMessageId { get; init; }
}
