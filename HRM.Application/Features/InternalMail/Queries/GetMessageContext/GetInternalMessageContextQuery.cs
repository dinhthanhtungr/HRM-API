using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetMessageContext;

/// <summary>
/// Lay cum message quanh mot message dich de FE scroll/highlight duoc ket qua search chua load.
/// </summary>
public sealed class GetInternalMessageContextQuery : IRequest<InternalMessageContextDto?>
{
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public int Before { get; init; } = 10;
    public int After { get; init; } = 10;
}
