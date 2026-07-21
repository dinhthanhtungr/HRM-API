using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

/// <summary>
/// Yeu cau gui mot loi nhan/thong bao nghiep vu trong ngu canh SampleRequest.
/// FE chi gui loai yeu cau, noi dung va nguoi nhan them; BE tu quyet dinh nguoi nhan mac dinh.
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
}
