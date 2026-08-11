using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationAttachments;

public sealed class GetInternalConversationAttachmentsQuery
    : PaginationQuery, IRequest<PagedResult<InternalConversationAttachmentDto>?>
{
    public Guid ConversationId { get; init; }
    public string? Kind { get; init; }
}
