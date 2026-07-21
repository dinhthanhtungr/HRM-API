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

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ClaimLead;

/// <summary>
/// Feature CRM CustomerCare - xử lý thêm hoặc gia hạn người chăm sóc lead cho sale/current scope.
/// Handler kiểm tra lead trong visibility scope, nhân viên nhận thuộc company và group hợp lệ,
/// sau đó tạo claim Work mới hoặc gia hạn claim active của cùng employee/group.
/// </summary>
internal sealed class ClaimLeadCommandHandler : IRequestHandler<ClaimLeadCommand, OperationResult>
{
    private const int MinimumClaimTtlDays = 1;
    private const int MaximumClaimTtlDays = 3650;

    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ClaimLeadCommandHandler(
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

    public async Task<OperationResult> Handle(ClaimLeadCommand command, CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId is invalid.");
        }

        var request = command.Request;
        if (request.ClaimTtlDays is < MinimumClaimTtlDays or > MaximumClaimTtlDays)
        {
            return OperationResult.Fail($"ClaimTtlDays must be between {MinimumClaimTtlDays} and {MaximumClaimTtlDays}.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var employeeId = request.EmployeeId is { } requestedEmployeeId && requestedEmployeeId != Guid.Empty
            ? requestedEmployeeId
            : scope.EmployeeId;

        if (!await IsEmployeeAllowedAsync(scope, employeeId, cancellationToken))
        {
            return OperationResult.Fail("EmployeeId is outside your allowed employee scope.");
        }

        var groupId = await ResolveEmployeeGroupAsync(scope, employeeId, request.GroupId, cancellationToken);
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
            .FirstOrDefaultAsync(item =>
                item.CustomerId == command.CustomerId &&
                item.CompanyId == scope.CompanyId &&
                item.IsLead,
                cancellationToken);
        if (customer is null)
        {
            return OperationResult.Fail("Lead was not found.");
        }

        var existingClaim = customer.CustomerClaims
            .Where(claim =>
                claim.IsActive &&
                claim.Type == ClaimType.Work &&
                claim.EmployeeId == employeeId &&
                claim.GroupId == groupId.Value)
            .OrderByDescending(claim => claim.ExpiresAt)
            .FirstOrDefault();

        if (existingClaim is not null)
        {
            existingClaim.ExpiresAt = now.AddDays(request.ClaimTtlDays);
        }
        else
        {
            await _writeDbContext.CustomerClaims.AddAsync(new CustomerClaim
            {
                Id = Guid.CreateVersion7(),
                CustomerId = customer.CustomerId,
                EmployeeId = employeeId,
                GroupId = groupId.Value,
                Type = ClaimType.Work,
                ExpiresAt = now.AddDays(request.ClaimTtlDays),
                IsActive = true,
                CompanyId = scope.CompanyId
            }, cancellationToken);
        }

        customer.IsLead = true;
        customer.LeadStatus = LeadStatus.Claimed;
        customer.CurrentSaleId = null;
        customer.UpdatedBy = scope.EmployeeId;
        customer.UpdatedDate = now;

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult.Fail(BuildConcurrencyMessage("Lead claim", exception));
        }

        return OperationResult.Ok(existingClaim is null
            ? "Lead caretaker added successfully."
            : "Lead caretaker claim extended successfully.");
    }

    private static string BuildConcurrencyMessage(string featureName, DbUpdateConcurrencyException exception)
    {
        var entityNames = exception.Entries
            .Select(entry => entry.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var suffix = entityNames.Count == 0
            ? string.Empty
            : $" Affected entities: {string.Join(", ", entityNames)}.";

        return $"{featureName} data was changed or deleted by another process. Please reload before saving.{suffix}";
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
