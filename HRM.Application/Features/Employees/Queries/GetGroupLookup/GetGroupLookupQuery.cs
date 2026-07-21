using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Queries.GetGroupLookup;

/// <summary>
/// Lookup group dùng chung cho UI chọn nhóm/phòng ban.
/// Với màn CRM transfer, FE lọc nhóm sale bằng `groupTypePrefix=CMR` để lấy `CMR` và các nhóm con `CMR.*`.
/// </summary>
public sealed class GetGroupLookupQuery : PaginationQuery, IRequest<PagedResult<GroupLookupDto>>
{
    public string? GroupType { get; init; }
    public string? GroupTypePrefix { get; init; }
}
