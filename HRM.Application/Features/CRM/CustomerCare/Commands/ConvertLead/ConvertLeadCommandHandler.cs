using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ConvertLead;

/// <summary>
/// Feature CRM CustomerCare - xử lý chuyển lead thành customer đã sale.
/// Handler kiểm tra lead trong visibility scope, nhân viên nhận thuộc company/group hợp lệ,
/// hủy claim Work còn active, tạo assignment mới và cập nhật CurrentSaleId.
/// </summary>
internal sealed class ConvertLeadCommandHandler : IRequestHandler<ConvertLeadCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConvertLeadCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(ConvertLeadCommand command, CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var employeeId = command.Request.EmployeeId is { } requestedEmployeeId && requestedEmployeeId != Guid.Empty
            ? requestedEmployeeId
            : scope.EmployeeId;

        if (!await IsEmployeeAllowedAsync(scope, employeeId, cancellationToken))
        {
            return OperationResult.Fail("EmployeeId is outside your allowed employee scope.");
        }

        var groupId = await ResolveEmployeeGroupAsync(scope, employeeId, command.Request.GroupId, cancellationToken);
        if (!groupId.HasValue)
        {
            return OperationResult.Fail("Employee does not belong to an allowed active group in this company.");
        }

        var canAccess = await _visibilityService
            .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(customer => customer.CustomerId == command.CustomerId && customer.IsLead, cancellationToken);
        if (!canAccess)
        {
            return OperationResult.Fail("Lead was not found or is outside your visibility scope.");
        }

        var now = _dateTimeProvider.Now;
        var customer = await _writeDbContext.Customers
            .Include(item => item.CustomerClaims)
            .Include(item => item.CustomerAssignments)
            .FirstOrDefaultAsync(item =>
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId &&
                item.IsLead,
                cancellationToken);
        if (customer is null)
        {
            return OperationResult.Fail("Lead was not found.");
        }

        foreach (var claim in customer.CustomerClaims.Where(claim =>
            claim.IsActive &&
            claim.Type == ClaimType.Work &&
            claim.ExpiresAt > now))
        {
            claim.IsActive = false;
        }

        foreach (var assignment in customer.CustomerAssignments.Where(assignment => assignment.IsActive))
        {
            assignment.IsActive = false;
            assignment.UpdatedBy = scope.EmployeeId;
            assignment.UpdatedDate = now;
        }

        customer.CustomerAssignments.Add(new CustomerAssignment
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.CustomerId,
            EmployeeId = employeeId,
            GroupId = groupId.Value,
            CompanyId = scope.CompanyId,
            CreatedBy = scope.EmployeeId,
            CreatedDate = now,
            UpdatedBy = scope.EmployeeId,
            UpdatedDate = now,
            IsActive = true
        });

        customer.IsLead = false;
        customer.LeadStatus = LeadStatus.Converted;
        customer.CurrentSaleId = employeeId;
        customer.UpdatedBy = scope.EmployeeId;
        customer.UpdatedDate = now;

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Lead converted successfully.");
    }

    private async Task<bool> IsEmployeeAllowedAsync(ViewerScope scope, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!scope.HasFullCustomerView && !scope.EmployeeIdsInScope.Contains(employeeId))
        {
            return false;
        }

        return await _readDbContext.Employees
            .AsNoTracking()
            .AnyAsync(employee =>
                employee.EmployeeId == employeeId &&
                employee.CompanyId == scope.CompanyId &&
                employee.IsActive,
                cancellationToken);
    }

    private async Task<Guid?> ResolveEmployeeGroupAsync(
        ViewerScope scope,
        Guid employeeId,
        Guid? requestedGroupId,
        CancellationToken cancellationToken)
    {
        var query = _readDbContext.MemberInGroups
            .AsNoTracking()
            .Where(member =>
                member.Profile == employeeId &&
                member.IsActive &&
                member.Group.CompanyId == scope.CompanyId);

        if (requestedGroupId is { } groupId && groupId != Guid.Empty)
        {
            query = query.Where(member => member.GroupId == groupId);
        }

        if (!scope.HasFullCustomerView)
        {
            var leaderGroupIds = scope.LeaderGroupIds.ToArray();
            query = query.Where(member => leaderGroupIds.Contains(member.GroupId));
        }

        return await query
            .OrderByDescending(member => member.IsAdmin == true)
            .ThenBy(member => member.MemberId)
            .Select(member => (Guid?)member.GroupId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
