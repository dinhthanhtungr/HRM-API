using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Groups.Dtos;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Enums.Category;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Groups.Commands.CreateGroup;

internal sealed class CreateGroupCommandHandler
    : IRequestHandler<CreateGroupCommand, GroupCommandResult<GroupDto>>
{
    private const int NameMaxLength = 200;
    private const int GroupTypeMaxLength = 100;

    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalIdService _externalIdService;

    public CreateGroupCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser,
        IExternalIdService externalIdService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _externalIdService = externalIdService;
    }

    public async Task<GroupCommandResult<GroupDto>> Handle(
        CreateGroupCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!_currentUser.IsAuthenticated || companyId is null || employeeId is null)
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Forbidden,
                "Tài khoản hiện tại chưa được liên kết đầy đủ với công ty và nhân viên.");
        }

        if (!HasFullManagementAccess())
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Forbidden,
                "Bạn không có quyền tạo nhóm.");
        }

        if (!await _dbContext.Employees.AnyAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.CompanyId == companyId &&
                    item.IsActive,
                cancellationToken))
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Forbidden,
                "Nhân viên hiện tại không active hoặc không thuộc công ty hiện tại.");
        }

        var name = TrimToNull(request.Name);
        if (name?.Length > NameMaxLength)
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Validation,
                $"Tên nhóm không được vượt quá {NameMaxLength} ký tự.");
        }

        var groupType = TrimToNull(request.GroupType);
        if (groupType?.Length > GroupTypeMaxLength)
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Validation,
                $"Loại nhóm không được vượt quá {GroupTypeMaxLength} ký tự.");
        }

        if (request.PartId == Guid.Empty)
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Validation,
                "Bộ phận là bắt buộc.");
        }

        if (!await _dbContext.Parts.AnyAsync(
                part =>
                    part.PartId == request.PartId &&
                    (_dbContext.Employees.Any(item =>
                         item.CompanyId == companyId &&
                         item.PartId == part.PartId) ||
                     _dbContext.Groups.Any(item =>
                         item.CompanyId == companyId &&
                         item.PartId == part.PartId)),
                cancellationToken))
        {
            return GroupCommandResult<GroupDto>.Fail(
                GroupCommandError.Validation,
                "Bộ phận không tồn tại hoặc không thuộc công ty hiện tại.");
        }

        var externalId = await _externalIdService.GenerateMonthlyCodeAsync(
            companyId.Value,
            DocumentPrefix.GRP.ToString(),
            cancellationToken);

        var group = new Group
        {
            GroupId = Guid.CreateVersion7(),
            ExternalId = externalId,
            Name = name,
            GroupType = groupType,
            PartId = request.PartId,
            CompanyId = companyId,
            //IsActive = true,
            CreatedBy = employeeId,
            CreatedDate = DateTime.Now
        };

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupCommandResult<GroupDto>.Ok(new GroupDto
        {
            GroupId = group.GroupId,
            ExternalId = group.ExternalId,
            Name = group.Name,
            GroupType = group.GroupType,
            PartId = group.PartId,
        });
    }

    private bool HasFullManagementAccess()
        => ApplicationRoleSets.SuperUsers.Any(_currentUser.IsInRole);

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
