using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Queries.GetCompanyLookup;

internal sealed class GetCompanyLookupQueryHandler
    : IRequestHandler<GetCompanyLookupQuery, IReadOnlyList<CompanyLookupDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetCompanyLookupQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CompanyLookupDto>> Handle(
        GetCompanyLookupQuery request,
        CancellationToken cancellationToken)
    {
        var currentCompanyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

        var query = _dbContext.Companies
            .AsNoTracking()
            .Where(company => company.IsActive);

        if (!_currentUser.IsInAnyRole(
                ApplicationRoleSets.EmployeeAdministration.GlobalCompanyManagers))
        {
            query = query.Where(company => company.CompanyId == currentCompanyId);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(company =>
                (company.Code ?? string.Empty).Contains(keyword) ||
                company.Name.Contains(keyword));
        }

        return await query
            .OrderBy(company => company.Name)
            .Select(company => new CompanyLookupDto
            {
                CompanyId = company.CompanyId,
                Code = company.Code ?? string.Empty,
                Name = company.Name
            })
            .ToListAsync(cancellationToken);
    }
}
