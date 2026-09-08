using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.RepairCustomerAssignmentGroups;

internal sealed class RepairCustomerAssignmentGroupsCommandHandler
    : IRequestHandler<RepairCustomerAssignmentGroupsCommand, OperationResult<RepairCustomerAssignmentGroupsResult>>
{
    private const int PreviewLimit = 100;

    private readonly ICRMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RepairCustomerAssignmentGroupsCommandHandler(
        ICRMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<RepairCustomerAssignmentGroupsResult>> Handle(
        RepairCustomerAssignmentGroupsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } actorEmployeeId ||
            actorEmployeeId == Guid.Empty)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                "Tài khoản hiện tại chưa được liên kết đầy đủ với nhân viên và công ty.");
        }

        if (!_currentUser.IsInRole(ApplicationRoles.Admin))
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                "Bạn không có quyền sửa dữ liệu CustomerAssignment.");
        }

        if (request.EmployeeId == Guid.Empty)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail("EmployeeId không hợp lệ.");
        }

        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == request.EmployeeId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new { x.EmployeeId, x.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                "Không tìm thấy Sale active trong công ty hiện tại.");
        }

        var memberships = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(x =>
                x.Profile == request.EmployeeId &&
                x.IsActive &&
                x.Group.CompanyId == companyId &&
                (!request.TargetGroupId.HasValue || x.GroupId == request.TargetGroupId.Value))
            .Select(x => new { x.GroupId, x.Group.Name })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                request.TargetGroupId.HasValue
                    ? "Sale không thuộc targetGroupId active trong công ty hiện tại."
                    : "Sale không thuộc group active nào trong công ty hiện tại.");
        }

        if (memberships.Count > 1)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                "Sale đang thuộc nhiều group active; cần truyền targetGroupId để chọn group cần đồng bộ.");
        }

        var targetGroup = memberships[0];
        var assignments = await _dbContext.CustomerAssignments
            .Where(x =>
                x.CompanyId == companyId &&
                x.EmployeeId == request.EmployeeId &&
                x.IsActive)
            .Include(x => x.Customer)
            .Include(x => x.Group)
            .OrderBy(x => x.Customer.ExternalId)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var candidates = assignments.Where(x => x.GroupId != targetGroup.GroupId).ToList();
        var candidateIds = candidates.Select(x => x.Id).ToArray();
        var candidateCustomerIds = candidates.Select(x => x.CustomerId).Distinct().ToArray();

        var conflictCustomerIds = candidateCustomerIds.Length == 0
            ? []
            : await _dbContext.CustomerAssignments
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.GroupId == targetGroup.GroupId &&
                    candidateCustomerIds.Contains(x.CustomerId) &&
                    !candidateIds.Contains(x.Id))
                .Select(x => x.CustomerId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        var conflictCustomerIdSet = conflictCustomerIds.ToHashSet();

        var previewItems = candidates
            .Take(PreviewLimit)
            .Select(x => new CustomerAssignmentGroupRepairPreviewItem(
                x.Id,
                x.CustomerId,
                x.Customer.ExternalId,
                x.Customer.CustomerName,
                x.GroupId,
                x.Group.Name,
                conflictCustomerIdSet.Contains(x.CustomerId)))
            .ToArray();

        var result = new RepairCustomerAssignmentGroupsResult(
            companyId,
            employee.EmployeeId,
            employee.FullName,
            targetGroup.GroupId,
            targetGroup.Name,
            request.DryRun,
            assignments.Count,
            assignments.Count - candidates.Count,
            candidates.Count,
            conflictCustomerIdSet.Count,
            0,
            previewItems,
            candidates.Count > PreviewLimit);

        if (request.DryRun)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Ok(
                result,
                "Đã kiểm tra CustomerAssignment, chưa ghi dữ liệu.");
        }

        if (conflictCustomerIdSet.Count > 0)
        {
            return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                result,
                "Không thể cập nhật vì có customer đã có assignment active tại target group. Hãy xử lý conflict trước.");
        }

        var now = _dateTimeProvider.Now;
        foreach (var assignment in candidates)
        {
            assignment.GroupId = targetGroup.GroupId;
            assignment.UpdatedBy = actorEmployeeId;
            assignment.UpdatedDate = now;
        }

        if (candidates.Count > 0)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.ClearTrackedChanges();
                return OperationResult<RepairCustomerAssignmentGroupsResult>.Fail(
                    "Cập nhật CustomerAssignment thất bại do dữ liệu thay đổi hoặc vi phạm ràng buộc. Hãy chạy dryRun lại.");
            }
        }

        return OperationResult<RepairCustomerAssignmentGroupsResult>.Ok(
            result with { UpdatedCount = candidates.Count },
            "Đã đồng bộ group CustomerAssignment thành công.");
    }
}
