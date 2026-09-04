using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.Notifications;
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

    /// <summary>
    /// Optional Notification Hub event-group filter. Conversation filtering is based on
    /// the structured payloads of its messages, not on notification recipients.
    /// </summary>
    public string? EventGroupCode { get; init; }

    /// <summary>
    /// Alias de FE dung search=... trong Notification Hub; Keyword van duoc giu cho contract phan trang chung.
    /// </summary>
    public string? Search { get; init; }

    [JsonIgnore]
    public string? NormalizedSearchKeyword =>
        string.IsNullOrWhiteSpace(Search) ? NormalizedKeyword : Search.Trim();

    [JsonIgnore]
    public string? NormalizedEventGroupCode =>
        NotificationTopicCatalog.NormalizeCode(EventGroupCode);
}
