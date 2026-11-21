using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetRelatedProducts;

public sealed record GetInternalConversationRelatedProductsQuery(Guid ConversationId)
    : IRequest<IReadOnlyList<InternalConversationRelatedProductDto>?>;
