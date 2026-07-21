using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

/// <summary>
/// Lay hom thu da gop theo conversation cua nhan vien hien tai.
/// RelatedType la bo loc nghiep vu; null nghia la lay tat ca.
/// </summary>
public sealed class GetInternalConversationsQuery
    : PaginationQuery, IRequest<PagedResult<InternalConversationListItemDto>>
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType? RelatedType { get; init; }

    public bool UnreadOnly { get; init; }
    public bool Archived { get; init; }
}
