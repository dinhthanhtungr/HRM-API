using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;

/// <summary>
/// Metadata phu luu trong InternalMessage.PayloadJson va Notification.PayloadJson
/// de FE mo dung thread/message ma khong phai dua vao title/message text.
/// </summary>
internal sealed class SampleRequestThreadMessagePayload
{
    public string ContentType { get; set; } = "InternalMailMessage";

    public Guid? ConversationId { get; set; }

    public Guid? MessageId { get; set; }

    public Guid? SampleRequestId { get; set; }

    public string? ExternalId { get; set; }

    public string? Type { get; set; }

    public string? SaleMessage { get; set; }

    public bool IsUrgent { get; set; }

    public Guid? ReplyToMessageId { get; set; }

    public SampleRequestDataChangePayload? DataChangeRequest { get; set; }

    public SampleRequestFormulaChangePayload? FormulaChangeRequest { get; set; }

    public SampleRequestDirectPatchNotificationPayload? DirectPatchNotification { get; set; }

    public SampleReceiptActionPayload? SampleReceiptAction { get; set; }

    public SampleRequestPriceQuotePayload? PriceQuoteRequest { get; set; }
}
