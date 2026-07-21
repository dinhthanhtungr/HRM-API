using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.AddParticipants;

public sealed class AddInternalConversationParticipantsCommand : IRequest<OperationResult>
{
    public Guid ConversationId { get; set; }
    public IReadOnlyList<Guid> EmployeeIds { get; set; } = Array.Empty<Guid>();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalConversationParticipantRole Role { get; set; } = InternalConversationParticipantRole.Member;
}
