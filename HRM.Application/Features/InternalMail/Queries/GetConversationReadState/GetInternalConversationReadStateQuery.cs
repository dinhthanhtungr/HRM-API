using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationReadState;

public sealed class GetInternalConversationReadStateQuery : IRequest<InternalConversationReadStateDto?>
{
    public Guid ConversationId { get; init; }
    public string? AreaCode { get; init; }
    public Guid? ThroughMessageId { get; init; }
}
