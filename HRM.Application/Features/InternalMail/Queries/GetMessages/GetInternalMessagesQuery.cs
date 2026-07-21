using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetMessages;

public sealed class GetInternalMessagesQuery : PaginationQuery, IRequest<PagedResult<InternalMessageDto>?>
{
    public Guid ConversationId { get; init; }
}
