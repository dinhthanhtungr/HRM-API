using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Queries.GetPartLookup;

internal sealed class GetPartLookupQueryHandler
    : IRequestHandler<GetPartLookupQuery, IReadOnlyList<PartLookupDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetPartLookupQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PartLookupDto>> Handle(
        GetPartLookupQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

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
            .Select(part => new PartLookupDto
            {
                PartId = part.PartId,
                ExternalId = part.ExternalId,
                PartName = part.PartName
            })
            .ToListAsync(cancellationToken);
    }
}
