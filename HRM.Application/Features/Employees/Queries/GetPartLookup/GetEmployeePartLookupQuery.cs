using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Queries.GetPartLookup;

/// <summary>
/// Lookup bộ phận theo company được phép quản lý để phân bộ phận cho nhân viên.
/// </summary>
public sealed class GetEmployeePartLookupQuery : IRequest<IReadOnlyList<EmployeePartLookupDto>>
{
    public Guid? CompanyId { get; init; }
    public string? Keyword { get; init; }

    public string? NormalizedKeyword
        => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
}
