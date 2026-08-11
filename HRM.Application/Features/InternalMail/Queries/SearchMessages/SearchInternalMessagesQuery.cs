using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.SearchMessages;

/// <summary>
/// Tim message trong mot conversation ma current employee la participant.
/// Ket qua dung cho UI "Find in conversation": hien total, next/previous va scroll toi message.
/// </summary>
public sealed class SearchInternalMessagesQuery
    : PaginationQuery, IRequest<PagedResult<InternalMessageSearchResultDto>?>
{
    public Guid ConversationId { get; init; }
    public string? Q { get; init; }
    public string? Search { get; init; }

    [JsonIgnore]
    public string? NormalizedSearchText
    {
        get
        {
            var raw = !string.IsNullOrWhiteSpace(Q)
                ? Q
                : !string.IsNullOrWhiteSpace(Search)
                    ? Search
                    : Keyword;

            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
    }
}
