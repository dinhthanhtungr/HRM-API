using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Queries.GetPartLookup;

internal sealed class GetEmployeePartLookupQueryHandler
    : IRequestHandler<GetEmployeePartLookupQuery, IReadOnlyList<EmployeePartLookupDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetEmployeePartLookupQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EmployeePartLookupDto>> Handle(
        GetEmployeePartLookupQuery request,
        CancellationToken cancellationToken)
    {
        var currentCompanyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");
        var companyId = request.CompanyId ?? currentCompanyId;

        if (companyId != currentCompanyId &&
            !_currentUser.IsInAnyRole(
                ApplicationRoleSets.EmployeeAdministration.GlobalCompanyManagers))
        {
            return [];
        }

        var companyExists = await _dbContext.Companies
            .AsNoTracking()
            .AnyAsync(
                company => company.CompanyId == companyId && company.IsActive,
                cancellationToken);
        if (!companyExists)
        {
            return [];
        }

        var query = _dbContext.Parts
            .AsNoTracking()
            .Where(part =>
                _dbContext.Employees.Any(employee =>
                    employee.CompanyId == companyId &&
                    employee.PartId == part.PartId) ||
                _dbContext.Groups.Any(group =>
                    group.CompanyId == companyId &&
                    group.PartId == part.PartId));

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(part =>
                part.ExternalId.Contains(keyword) ||
                part.PartName.Contains(keyword));
        }

        return await query
            .OrderBy(part => part.PartName)
            .ThenBy(part => part.ExternalId)
            .Select(part => new EmployeePartLookupDto
            {
                PartId = part.PartId,
                ExternalId = part.ExternalId,
                PartName = part.PartName
            })
            .ToListAsync(cancellationToken);
    }
}
