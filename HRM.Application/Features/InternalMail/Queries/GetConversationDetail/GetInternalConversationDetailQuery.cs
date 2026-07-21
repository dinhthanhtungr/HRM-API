using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationDetail;

public sealed class GetInternalConversationDetailQuery : IRequest<InternalConversationDetailDto?>
{
    public Guid ConversationId { get; init; }
}
