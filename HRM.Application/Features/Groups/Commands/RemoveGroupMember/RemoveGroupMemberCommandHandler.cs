using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Commands.RemoveGroupMember;

internal sealed class RemoveGroupMemberCommandHandler
    : IRequestHandler<RemoveGroupMemberCommand, GroupCommandResult<GroupMemberDto>>
{
    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public RemoveGroupMemberCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GroupCommandResult<GroupMemberDto>> Handle(
        RemoveGroupMemberCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var currentEmployeeId = _currentUser.EmployeeId;
        if (!_currentUser.IsAuthenticated || companyId is null || currentEmployeeId is null)
        {
            return Fail(GroupCommandError.Forbidden,
                "Tài khoản hiện tại chưa liên kết đầy đủ với công ty và nhân viên.");
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

        var hasFullAccess = ApplicationRoleSets.SuperUsers.Any(_currentUser.IsInRole);
        var isCurrentLeader = await _dbContext.MemberInGroups.AnyAsync(
            member => member.GroupId == request.GroupId &&
                member.Profile == currentEmployeeId && member.IsActive && member.IsAdmin == true,
            cancellationToken);
        if (!hasFullAccess && !isCurrentLeader)
        {
            return Fail(GroupCommandError.Forbidden,
                "Bạn không có quyền gỡ thành viên khỏi nhóm này.");
        }

        var membership = await _dbContext.MemberInGroups
            .Where(member => member.GroupId == request.GroupId &&
                member.Profile == request.EmployeeId && member.IsActive)
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
                "Nhân viên không phải thành viên active của nhóm.");
        }

        if (membership.Membership.IsAdmin == true)
        {
            if (!hasFullAccess)
            {
                return Fail(GroupCommandError.Forbidden,
                    "Leader không được gỡ một leader khác khỏi nhóm.");
            }

            var activeLeaderCount = await _dbContext.MemberInGroups.CountAsync(
                member => member.GroupId == request.GroupId && member.IsActive &&
                    member.Profile != null && member.IsAdmin == true,
                cancellationToken);
            if (!GroupManagementRules.CanRemoveLeader(activeLeaderCount))
            {
                return Fail(GroupCommandError.Conflict,
                    "Không thể gỡ leader cuối cùng của nhóm.");
            }
        }

        membership.Membership.IsActive = false;
        membership.Membership.IsAdmin = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupCommandResult<GroupMemberDto>.Ok(new GroupMemberDto
        {
            MemberId = membership.Membership.MemberId,
            EmployeeId = request.EmployeeId,
            EmployeeExternalId = membership.EmployeeExternalId,
            FullName = membership.FullName,
            IsLeader = false,
            IsActive = false
        });
    }

    private static GroupCommandResult<GroupMemberDto> Fail(GroupCommandError error, string message)
        => GroupCommandResult<GroupMemberDto>.Fail(error, message);
}
