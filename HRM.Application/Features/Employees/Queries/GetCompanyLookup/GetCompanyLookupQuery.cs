using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Queries.GetCompanyLookup;

/// <summary>
/// Lookup company active phục vụ tạo nhân viên; chỉ Developer được xem ngoài company hiện tại.
/// </summary>
public sealed class GetCompanyLookupQuery : IRequest<IReadOnlyList<CompanyLookupDto>>
{
    public string? Keyword { get; init; }

    public string? NormalizedKeyword
        => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
}
