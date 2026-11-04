using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetParticipants;

public sealed class GetInternalConversationParticipantsQuery : IRequest<IReadOnlyList<InternalConversationParticipantDto>?>
{
    public Guid ConversationId { get; init; }
    public string? AreaCode { get; init; }
}
