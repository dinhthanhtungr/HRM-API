using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Queries.GetGroupLeaders;

internal sealed class GetGroupLeadersQueryHandler
    : IRequestHandler<GetGroupLeadersQuery, GroupMembersDto?>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGroupLeadersQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GroupMembersDto?> Handle(
        GetGroupLeadersQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

        var group = await _dbContext.Groups
            .AsNoTracking()
            .Where(item => item.GroupId == request.GroupId && item.CompanyId == companyId)
            .Select(item => new
            {
                item.GroupId,
                item.ExternalId,
                item.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return null;
        }

        var leaders = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(member => member.GroupId == request.GroupId && member.IsActive &&
                member.IsAdmin == true && member.Profile != null &&
                member.ProfileNavigation != null && member.ProfileNavigation.IsActive &&
                member.ProfileNavigation.CompanyId == companyId)
            .OrderBy(member => member.ProfileNavigation!.FullName)
            .Select(member => new GroupMemberDto
            {
                MemberId = member.MemberId,
                EmployeeId = member.Profile!.Value,
                EmployeeExternalId = member.ProfileNavigation!.ExternalId,
                FullName = member.ProfileNavigation.FullName,
                IsLeader = true,
                IsActive = true
            })
            .ToListAsync(cancellationToken);

        return new GroupMembersDto
        {
            GroupId = group.GroupId,
            ExternalId = group.ExternalId,
            Name = group.Name,
            Members = leaders
        };
    }
}
