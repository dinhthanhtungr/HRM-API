using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;

/// <summary>
/// Feature CRM CustomerCare - xử lý chuyển giao khách hàng cho leader/director.
/// Handler kiểm tra company, visibility scope, nhân viên nhận/chuyển và group phụ trách
/// trước khi soft-disable owner cũ, tạo owner mới và ghi log chuyển giao.
/// Khi `transferAll=true`, handler tự lấy toàn bộ lead/customer visible đang thuộc fromEmployee.
/// Khi FE gửi danh sách customer mà không gửi fromEmployee/fromGroup, handler tự suy nguồn từ owner active
/// và chỉ cho chuyển nếu tất cả customer có cùng một sale/group nguồn.
/// </summary>
internal sealed class TransferCustomersCommandHandler
    : IRequestHandler<TransferCustomersCommand, OperationResult<TransferCustomersResultDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TransferCustomersCommandHandler(
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

    public async Task<OperationResult<TransferCustomersResultDto>> Handle(
        TransferCustomersCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.ToEmployeeId == Guid.Empty)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("ToEmployeeId is required.");
        }

        if (request.FromEmployeeId is { } fromEmployeeId &&
            fromEmployeeId != Guid.Empty &&
            fromEmployeeId == request.ToEmployeeId)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Cannot transfer customers to the same employee.");
        }

        if (!Enum.IsDefined(request.TransferType))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("TransferType is invalid.");
        }

        if (request.TransferAll && request.CustomerIds.Count > 0)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("CustomerIds must be empty when TransferAll is true.");
        }

        if (request.TransferAll &&
            (request.FromEmployeeId is null || request.FromEmployeeId == Guid.Empty))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("FromEmployeeId is required when TransferAll is true.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!scope.HasFullCustomerView && scope.LeaderGroupIds.Count == 0)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Only sale leaders or full-view users can transfer customers.");
        }

        if (request.FromEmployeeId is { } requestedFromEmployeeId &&
            requestedFromEmployeeId != Guid.Empty &&
            !await IsEmployeeAllowedAsync(scope, requestedFromEmployeeId, cancellationToken))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("FromEmployeeId is outside your allowed employee scope.");
        }

        if (!await IsEmployeeAllowedAsync(scope, request.ToEmployeeId, cancellationToken))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("ToEmployeeId is outside your allowed employee scope.");
        }

        var customerIdsResult = await ResolveCustomerIdsAsync(request, scope, cancellationToken);
        if (!customerIdsResult.Success || customerIdsResult.Data is null)
        {
            return OperationResult<TransferCustomersResultDto>.Fail(
                customerIdsResult.Message ?? "CustomerIds could not be resolved.");
        }

        var customerIds = customerIdsResult.Data;
        var now = _dateTimeProvider.Now;
        var customers = await _writeDbContext.Customers
            .Include(customer => customer.CustomerAssignments)
            .Include(customer => customer.CustomerClaims)
            .Where(customer =>
                customer.CompanyId == scope.CompanyId &&
                customerIds.Contains(customer.CustomerId))
            .ToListAsync(cancellationToken);

        SourceOwner? sourceOwner = null;
        foreach (var customer in customers)
        {
            var owner = ResolveSourceOwner(customer, request, now);

            if (owner is null)
            {
                return OperationResult<TransferCustomersResultDto>.Fail(
                    $"Customer {customer.CustomerId} has no active {request.TransferType} owner matching the selected source.");
            }

            if (sourceOwner is not null &&
                (sourceOwner.EmployeeId != owner.EmployeeId || sourceOwner.GroupId != owner.GroupId))
            {
                return OperationResult<TransferCustomersResultDto>.Fail(
                    "All customers in one transfer must have the same source employee and source group because CustomerTransferLog stores one FromEmployeeId and one FromGroupId.");
            }

            sourceOwner = owner;
        }

        if (sourceOwner is null)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Source owner could not be resolved.");
        }

        if (sourceOwner.EmployeeId == request.ToEmployeeId)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Cannot transfer customers to the same employee.");
        }

        if (!await IsEmployeeAllowedAsync(scope, sourceOwner.EmployeeId, cancellationToken))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Resolved source employee is outside your allowed employee scope.");
        }

        if (!scope.HasFullCustomerView && !scope.LeaderGroupIds.Contains(sourceOwner.GroupId))
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Source group is outside your leader scope.");
        }

        var toGroupId = await ResolveEmployeeGroupAsync(
            scope,
            request.ToEmployeeId,
            request.ToGroupId,
            cancellationToken);
        if (!toGroupId.HasValue)
        {
            return OperationResult<TransferCustomersResultDto>.Fail("Target employee does not belong to an allowed active group in this company.");
        }

        var logId = Guid.CreateVersion7();
        await _writeDbContext.CustomerTransferLogs.AddAsync(new CustomerTransferLog
        {
            Id = logId,
            FromEmployeeId = sourceOwner.EmployeeId,
            ToEmployeeId = request.ToEmployeeId,
            FromGroupId = sourceOwner.GroupId,
            ToGroupId = toGroupId.Value,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            TransferType = request.TransferType,
            CreatedBy = scope.EmployeeId,
            CreatedDate = now,
            CompanyId = scope.CompanyId
        }, cancellationToken);

        if (request.TransferType == TransferType.Saled)
        {
            await DeactivateAssignmentsAsync(customerIds, scope.EmployeeId, now, scope.CompanyId, cancellationToken);
        }
        else
        {
            await DeactivateLeadClaimsAsync(customerIds, now, cancellationToken);
        }

        foreach (var customer in customers)
        {
            if (request.TransferType == TransferType.Saled)
            {
                TransferAssignedCustomer(customer, request.ToEmployeeId, scope.EmployeeId, now);
                await _writeDbContext.CustomerAssignments.AddAsync(new CustomerAssignment
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    EmployeeId = request.ToEmployeeId,
                    GroupId = toGroupId.Value,
                    CompanyId = scope.CompanyId,
                    CreatedBy = scope.EmployeeId,
                    CreatedDate = now,
                    UpdatedBy = scope.EmployeeId,
                    UpdatedDate = now,
                    IsActive = true
                }, cancellationToken);
            }
            else
            {
                TransferLeadCustomer(customer, request.ToEmployeeId);
                await _writeDbContext.CustomerClaims.AddAsync(new CustomerClaim
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = customer.CustomerId,
                    EmployeeId = request.ToEmployeeId,
                    GroupId = toGroupId.Value,
                    Type = ClaimType.Work,
                    ExpiresAt = now.AddHours(48),
                    IsActive = true,
                    CompanyId = scope.CompanyId
                }, cancellationToken);
            }

            await _writeDbContext.DetailCustomerTransfers.AddAsync(new DetailCustomerTransfer
            {
                LogId = logId,
                CustomerId = customer.CustomerId
            }, cancellationToken);
        }

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<TransferCustomersResultDto>.Fail(
                BuildConcurrencyMessage("Customer transfer", exception));
        }

        return OperationResult<TransferCustomersResultDto>.Ok(new TransferCustomersResultDto
        {
            TransferLogId = logId,
            TransferredCount = customers.Count
        }, "Customers transferred successfully.");
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

    private Task<int> DeactivateAssignmentsAsync(
        IReadOnlyCollection<Guid> customerIds,
        Guid actorEmployeeId,
        DateTime now,
        Guid companyId,
        CancellationToken cancellationToken)
        => _writeDbContext.CustomerAssignments
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                assignment.IsActive &&
                customerIds.Contains(assignment.CustomerId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(assignment => assignment.IsActive, false)
                .SetProperty(assignment => assignment.UpdatedBy, actorEmployeeId)
                .SetProperty(assignment => assignment.UpdatedDate, now),
                cancellationToken);

    private Task<int> DeactivateLeadClaimsAsync(
        IReadOnlyCollection<Guid> customerIds,
        DateTime now,
        CancellationToken cancellationToken)
        => _writeDbContext.CustomerClaims
            .Where(claim =>
                claim.IsActive &&
                claim.Type == ClaimType.Work &&
                claim.ExpiresAt > now &&
                customerIds.Contains(claim.CustomerId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(claim => claim.IsActive, false),
                cancellationToken);

    private async Task<bool> IsEmployeeAllowedAsync(
        Commons.Authorization.ViewerScope scope,
        Guid employeeId,
        CancellationToken cancellationToken)
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

    private async Task<OperationResult<List<Guid>>> ResolveCustomerIdsAsync(
        TransferCustomersRequest request,
        Commons.Authorization.ViewerScope scope,
        CancellationToken cancellationToken)
    {
        if (request.TransferAll)
        {
            var fromEmployeeId = request.FromEmployeeId!.Value;
            var visibleCustomers = _visibilityService.ApplyCustomerVisibility(
                _readDbContext.Customers.AsNoTracking(),
                scope);

            var query = request.TransferType == TransferType.Saled
                ? visibleCustomers.Where(customer =>
                    !customer.IsLead &&
                    customer.CustomerAssignments.Any(assignment =>
                        assignment.IsActive &&
                        assignment.EmployeeId == fromEmployeeId &&
                        (!request.FromGroupId.HasValue ||
                         request.FromGroupId == Guid.Empty ||
                         assignment.GroupId == request.FromGroupId.Value)))
                : visibleCustomers.Where(customer =>
                    customer.IsLead &&
                    customer.CustomerClaims.Any(claim =>
                        claim.IsActive &&
                        claim.Type == ClaimType.Work &&
                        claim.ExpiresAt > scope.Now &&
                        claim.EmployeeId == fromEmployeeId &&
                        (!request.FromGroupId.HasValue ||
                         request.FromGroupId == Guid.Empty ||
                         claim.GroupId == request.FromGroupId.Value)));

            var resolvedIds = await query
                .Select(customer => customer.CustomerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return resolvedIds.Count == 0
                ? OperationResult<List<Guid>>.Fail("No visible customers were found for TransferAll.")
                : OperationResult<List<Guid>>.Ok(resolvedIds);
        }

        var customerIds = request.CustomerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (customerIds.Count == 0)
        {
            return OperationResult<List<Guid>>.Fail("CustomerIds is required when TransferAll is false.");
        }

        if (customerIds.Count != request.CustomerIds.Count)
        {
            return OperationResult<List<Guid>>.Fail("CustomerIds contains empty or duplicate values.");
        }

        var visibleCustomerIds = await _visibilityService
            .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
            .Where(customer => customerIds.Contains(customer.CustomerId))
            .Select(customer => customer.CustomerId)
            .ToListAsync(cancellationToken);
        if (visibleCustomerIds.Count != customerIds.Count)
        {
            return OperationResult<List<Guid>>.Fail("One or more customers were not found or are outside your visibility scope.");
        }

        return OperationResult<List<Guid>>.Ok(customerIds);
    }

    private async Task<Guid?> ResolveEmployeeGroupAsync(
        Commons.Authorization.ViewerScope scope,
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

    private static SourceOwner? ResolveSourceOwner(
        Customer customer,
        TransferCustomersRequest request,
        DateTime now)
    {
        if (request.TransferType == TransferType.Saled)
        {
            var assignment = ResolveActiveAssignment(
                customer,
                request.FromEmployeeId,
                request.FromGroupId);

            return assignment is null
                ? null
                : new SourceOwner(assignment.EmployeeId, assignment.GroupId);
        }

        var claim = ResolveActiveClaim(
            customer,
            request.FromEmployeeId,
            request.FromGroupId,
            now);

        return claim is null
            ? null
            : new SourceOwner(claim.EmployeeId, claim.GroupId);
    }

    private static CustomerAssignment? ResolveActiveAssignment(
        Customer customer,
        Guid? fromEmployeeId,
        Guid? fromGroupId)
        => customer.CustomerAssignments
            .Where(assignment =>
                assignment.IsActive &&
                (!fromEmployeeId.HasValue ||
                 fromEmployeeId == Guid.Empty ||
                 assignment.EmployeeId == fromEmployeeId.Value) &&
                (!fromGroupId.HasValue ||
                 fromGroupId == Guid.Empty ||
                 assignment.GroupId == fromGroupId.Value))
            .OrderByDescending(assignment => assignment.CreatedDate)
            .FirstOrDefault();

    private static CustomerClaim? ResolveActiveClaim(
        Customer customer,
        Guid? fromEmployeeId,
        Guid? fromGroupId,
        DateTime now)
        => customer.CustomerClaims
            .Where(claim =>
                claim.IsActive &&
                claim.Type == ClaimType.Work &&
                claim.ExpiresAt > now &&
                (!fromEmployeeId.HasValue ||
                 fromEmployeeId == Guid.Empty ||
                 claim.EmployeeId == fromEmployeeId.Value) &&
                (!fromGroupId.HasValue ||
                 fromGroupId == Guid.Empty ||
                 claim.GroupId == fromGroupId.Value))
            .OrderByDescending(claim => claim.ExpiresAt)
            .FirstOrDefault();

    private sealed record SourceOwner(Guid EmployeeId, Guid GroupId);

    private static void TransferAssignedCustomer(
        Customer customer,
        Guid toEmployeeId,
        Guid actorEmployeeId,
        DateTime now)
    {
        customer.CurrentSaleId = toEmployeeId;
        customer.IsLead = false;
        customer.LeadStatus = LeadStatus.Converted;
        customer.UpdatedBy = actorEmployeeId;
        customer.UpdatedDate = now;
    }

    private static void TransferLeadCustomer(
        Customer customer,
        Guid toEmployeeId)
    {
        customer.CurrentSaleId = null;
        customer.IsLead = true;
        customer.LeadStatus = LeadStatus.Claimed;
    }
}
