using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

/// <summary>
/// Gửi một message nghiệp vụ trong thread InternalMail gắn với SampleRequest.
/// FE chỉ gửi loại message, nội dung và người nhận thêm; BE tự resolve required recipients trước khi gửi thật.
/// </summary>
public sealed class SendSampleRequestMessageCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid SampleRequestId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleRequestNotificationType Type { get; set; }

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<Guid> ExtraRecipientEmployeeIds { get; set; } = Array.Empty<Guid>();

    public Guid? ReplyToMessageId { get; set; }

    public bool IsUrgent { get; set; }

    public DateTime? ReminderAt { get; set; }

    [JsonIgnore]
    internal SampleRequestDataChangePayload? DataChangeRequest { get; set; }

    [JsonIgnore]
    internal SampleRequestFormulaChangePayload? FormulaChangeRequest { get; set; }

    [JsonIgnore]
    internal SampleRequestDirectPatchNotificationPayload? DirectPatchNotification { get; set; }

    [JsonIgnore]
    internal TopicNotifications? TopicOverride { get; set; }

    [JsonIgnore]
    internal string? TitleOverride { get; set; }

    [JsonIgnore]
    internal IReadOnlyCollection<Guid>? NotificationRecipientEmployeeIdsOverride { get; set; }
}
