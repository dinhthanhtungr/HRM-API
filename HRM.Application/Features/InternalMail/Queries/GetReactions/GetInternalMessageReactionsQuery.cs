using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetReactions;

public sealed record GetInternalMessageReactionsQuery(Guid ConversationId, Guid MessageId, int PageNumber = 1, int PageSize = 30)
    : IRequest<PagedResult<InternalMessageReactionPersonDto>?>;
