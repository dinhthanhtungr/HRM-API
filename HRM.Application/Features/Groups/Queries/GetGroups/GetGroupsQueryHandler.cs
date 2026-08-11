using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Queries.GetGroups;

internal sealed class GetGroupsQueryHandler
    : IRequestHandler<GetGroupsQuery, IReadOnlyList<GroupDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGroupsQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<GroupDto>> Handle(
        GetGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

        return await _dbContext.Groups
            .AsNoTracking()
            .Where(group => group.CompanyId == companyId)
            .OrderBy(group => group.Name)
            .ThenBy(group => group.ExternalId)
            .Select(group => new GroupDto
            {
                GroupId = group.GroupId,
                ExternalId = group.ExternalId,
                Name = group.Name,
                GroupType = group.GroupType,
                PartId = group.PartId,
                MemberCount = group.MemberInGroups.Count(member =>
                    member.IsActive && member.Profile != null),
                LeaderCount = group.MemberInGroups.Count(member =>
                    member.IsActive &&
                    member.Profile != null &&
                    member.IsAdmin == true)
            })
            .ToListAsync(cancellationToken);
    }
}
