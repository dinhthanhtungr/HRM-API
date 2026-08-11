using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Groups.Dtos;
using HRM.Domain.Entities.CompanySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Commands.AddGroupMember;

internal sealed class AddGroupMemberCommandHandler
    : IRequestHandler<AddGroupMemberCommand, GroupCommandResult<GroupMemberDto>>
{
    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AddGroupMemberCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GroupCommandResult<GroupMemberDto>> Handle(
        AddGroupMemberCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var currentEmployeeId = _currentUser.EmployeeId;
        if (!_currentUser.IsAuthenticated || companyId is null || currentEmployeeId is null)
        {
            return GroupCommandResult<GroupMemberDto>.Fail(
                GroupCommandError.Forbidden,
                "Tài khoản hiện tại chưa được liên kết đầy đủ với công ty và nhân viên.");
        }

        var groupExists = await _dbContext.Groups.AnyAsync(
            group => group.GroupId == request.GroupId && group.CompanyId == companyId,
            cancellationToken);

        if (!groupExists)
        {
            return GroupCommandResult<GroupMemberDto>.Fail(
                GroupCommandError.NotFound,
                "Không tìm thấy nhóm trong công ty hiện tại.");
        }

        var hasFullManagementAccess = HasFullManagementAccess();
        var isCurrentGroupLeader = await _dbContext.MemberInGroups.AnyAsync(
            member =>
                member.GroupId == request.GroupId &&
                member.Profile == currentEmployeeId &&
                member.IsActive &&
                member.IsAdmin == true,
            cancellationToken);

        if (!hasFullManagementAccess && !isCurrentGroupLeader)
        {
            return GroupCommandResult<GroupMemberDto>.Fail(
                GroupCommandError.Forbidden,
                "Bạn không có quyền quản lý thành viên của nhóm này.");
        }

        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Where(item =>
                item.EmployeeId == request.EmployeeId &&
                item.CompanyId == companyId &&
                item.IsActive)
            .Select(item => new
            {
                item.EmployeeId,
                item.ExternalId,
                item.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return GroupCommandResult<GroupMemberDto>.Fail(
                GroupCommandError.NotFound,
                "Không tìm thấy nhân viên active trong công ty hiện tại.");
        }

        var activeMember = await _dbContext.MemberInGroups.FirstOrDefaultAsync(
            member =>
                member.GroupId == request.GroupId &&
                member.Profile == request.EmployeeId &&
                member.IsActive,
            cancellationToken);

        if (!hasFullManagementAccess &&
            (request.IsLeader || activeMember?.IsAdmin == true))
        {
            return GroupCommandResult<GroupMemberDto>.Fail(
                GroupCommandError.Forbidden,
                "Chỉ Admin, President hoặc Developer được gán hay thay đổi quyền leader.");
        }

        if (activeMember is null)
        {
            activeMember = new MemberInGroup
            {
                MemberId = Guid.CreateVersion7(),
                GroupId = request.GroupId,
                Profile = request.EmployeeId,
                IsAdmin = request.IsLeader,
                IsActive = true
            };
            _dbContext.MemberInGroups.Add(activeMember);
        }
        else
        {
            activeMember.IsAdmin = request.IsLeader;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupCommandResult<GroupMemberDto>.Ok(new GroupMemberDto
        {
            MemberId = activeMember.MemberId,
            EmployeeId = employee.EmployeeId,
            EmployeeExternalId = employee.ExternalId,
            FullName = employee.FullName,
            IsLeader = activeMember.IsAdmin == true
        });
    }

    private bool HasFullManagementAccess()
        => ApplicationRoleSets.SuperUsers.Any(_currentUser.IsInRole);
}
