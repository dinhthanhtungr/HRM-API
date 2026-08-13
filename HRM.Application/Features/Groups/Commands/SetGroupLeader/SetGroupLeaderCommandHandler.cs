using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Commands.SetGroupLeader;

internal sealed class SetGroupLeaderCommandHandler
    : IRequestHandler<SetGroupLeaderCommand, GroupCommandResult<GroupMemberDto>>
{
    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public SetGroupLeaderCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GroupCommandResult<GroupMemberDto>> Handle(
        SetGroupLeaderCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        if (!_currentUser.IsAuthenticated ||
            companyId is null ||
            _currentUser.EmployeeId is null ||
            !ApplicationRoleSets.SuperUsers.Any(_currentUser.IsInRole))
        {
            return Fail(GroupCommandError.Forbidden,
                "Bạn không có quyền bổ nhiệm hoặc bãi nhiệm leader.");
        }

        var group = await _dbContext.Groups
            .AsNoTracking()
            .Where(item => item.GroupId == request.GroupId && item.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return Fail(GroupCommandError.NotFound,
                "Không tìm thấy nhóm trong công ty hiện tại.");
        }

        var membership = await _dbContext.MemberInGroups
            .Where(member => member.GroupId == request.GroupId &&
                member.Profile == request.EmployeeId && member.IsActive &&
                member.ProfileNavigation != null && member.ProfileNavigation.IsActive &&
                member.ProfileNavigation.CompanyId == companyId)
            .Select(member => new
            {
                Membership = member,
                EmployeeExternalId = member.ProfileNavigation!.ExternalId,
                FullName = member.ProfileNavigation.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return Fail(GroupCommandError.NotFound,
                "Leader phải là thành viên active cùng công ty của nhóm.");
        }

        if (membership.Membership.IsAdmin == true && !request.IsLeader)
        {
            var activeLeaderCount = await _dbContext.MemberInGroups.CountAsync(
                member => member.GroupId == request.GroupId && member.IsActive &&
                    member.Profile != null && member.IsAdmin == true,
                cancellationToken);
            if (!GroupManagementRules.CanRemoveLeader(activeLeaderCount))
            {
                return Fail(GroupCommandError.Conflict,
                    "Không thể bãi nhiệm leader cuối cùng của nhóm.");
            }
        }

        membership.Membership.IsAdmin = request.IsLeader;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupCommandResult<GroupMemberDto>.Ok(new GroupMemberDto
        {
            MemberId = membership.Membership.MemberId,
            EmployeeId = request.EmployeeId,
            EmployeeExternalId = membership.EmployeeExternalId,
            FullName = membership.FullName,
            IsLeader = membership.Membership.IsAdmin == true,
            IsActive = membership.Membership.IsActive
        });
    }

    private static GroupCommandResult<GroupMemberDto> Fail(GroupCommandError error, string message)
        => GroupCommandResult<GroupMemberDto>.Fail(error, message);
}
