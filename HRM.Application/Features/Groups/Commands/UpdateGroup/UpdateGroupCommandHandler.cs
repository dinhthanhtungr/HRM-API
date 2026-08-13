using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Groups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Commands.UpdateGroup;

internal sealed class UpdateGroupCommandHandler
    : IRequestHandler<UpdateGroupCommand, GroupCommandResult<GroupDto>>
{
    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateGroupCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GroupCommandResult<GroupDto>> Handle(
        UpdateGroupCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        if (!_currentUser.IsAuthenticated ||
            companyId is null ||
            _currentUser.EmployeeId is null ||
            !ApplicationRoleSets.SuperUsers.Any(_currentUser.IsInRole))
        {
            return Fail(GroupCommandError.Forbidden, "Bạn không có quyền cập nhật nhóm.");
        }

        var name = GroupManagementRules.TrimToNull(request.Name);
        if (name?.Length > GroupManagementRules.NameMaxLength)
        {
            return Fail(GroupCommandError.Validation,
                $"Tên nhóm không được vượt quá {GroupManagementRules.NameMaxLength} ký tự.");
        }

        var groupType = GroupManagementRules.TrimToNull(request.GroupType);
        if (groupType?.Length > GroupManagementRules.GroupTypeMaxLength)
        {
            return Fail(GroupCommandError.Validation,
                $"Loại nhóm không được vượt quá {GroupManagementRules.GroupTypeMaxLength} ký tự.");
        }

        if (request.PartId == Guid.Empty ||
            !await PartBelongsToCompanyAsync(request.PartId, companyId.Value, cancellationToken))
        {
            return Fail(GroupCommandError.Validation,
                "Bộ phận không tồn tại hoặc không thuộc công ty hiện tại.");
        }

        var group = await _dbContext.Groups.FirstOrDefaultAsync(
            item => item.GroupId == request.GroupId && item.CompanyId == companyId,
            cancellationToken);
        if (group is null)
        {
            return Fail(GroupCommandError.NotFound,
                "Không tìm thấy nhóm trong công ty hiện tại.");
        }

        group.Name = name;
        group.GroupType = groupType;
        group.PartId = request.PartId;
        group.UpdatedBy = _currentUser.EmployeeId;
        group.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var counts = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(member => member.GroupId == group.GroupId && member.IsActive)
            .GroupBy(_ => 1)
            .Select(items => new
            {
                MemberCount = items.Count(item => item.Profile != null),
                LeaderCount = items.Count(item => item.Profile != null && item.IsAdmin == true)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return GroupCommandResult<GroupDto>.Ok(new GroupDto
        {
            GroupId = group.GroupId,
            ExternalId = group.ExternalId,
            Name = group.Name,
            GroupType = group.GroupType,
            PartId = group.PartId,
            MemberCount = counts?.MemberCount ?? 0,
            LeaderCount = counts?.LeaderCount ?? 0
        });
    }

    private Task<bool> PartBelongsToCompanyAsync(
        Guid partId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.Parts.AnyAsync(
            part => part.PartId == partId &&
                (_dbContext.Employees.Any(employee =>
                     employee.CompanyId == companyId && employee.PartId == part.PartId) ||
                 _dbContext.Groups.Any(group =>
                     group.CompanyId == companyId && group.PartId == part.PartId)),
            cancellationToken);

    private static GroupCommandResult<GroupDto> Fail(GroupCommandError error, string message)
        => GroupCommandResult<GroupDto>.Fail(error, message);
}
