using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.ForwardMessage;

public sealed class ForwardInternalMessageCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    [JsonIgnore] public Guid MessageId { get; set; }
    public Guid TargetConversationId { get; set; }
}
