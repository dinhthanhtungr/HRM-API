using HRM.Application.Features.Groups.Dtos;
using MediatR;

namespace HRM.Application.Features.Groups.Queries.GetPartLookup;

/// <summary>
/// Lookup bộ phận có dữ liệu liên kết với công ty hiện tại để chọn khi tạo nhóm.
/// </summary>
public sealed class GetPartLookupQuery : IRequest<IReadOnlyList<PartLookupDto>>
{
    public string? Keyword { get; init; }

    public string? NormalizedKeyword
        => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
}
