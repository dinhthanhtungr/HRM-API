using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.TransferCustomers;

internal sealed class ExecuteCustomerTransferCommandHandler
    : IRequestHandler<ExecuteCustomerTransferCommand, OperationResult<ExecuteCustomerTransferResultDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ExecuteCustomerTransferCommandHandler(
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

    public async Task<OperationResult<ExecuteCustomerTransferResultDto>> Handle(
        ExecuteCustomerTransferCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.ToEmployeeId == Guid.Empty)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail("ToEmployeeId is required.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!CustomerTransferRules.CanTransfer(scope))
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                CustomerTransferRules.TransferPermissionMessage);
        }

        var target = await ResolveTargetAsync(scope, request.ToEmployeeId, request.ToGroupId, cancellationToken);
        if (target is null)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                "Target employee does not belong to an allowed active group in this company.");
        }

        var selectedOwnersResult = await ResolveSelectedOwnersAsync(request, scope, cancellationToken);
        if (!selectedOwnersResult.Success || selectedOwnersResult.Data is null)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                selectedOwnersResult.Message ?? "Transfer customers could not be resolved.");
        }

        var selectedOwners = selectedOwnersResult.Data;
        var sameEmployee = selectedOwners.FirstOrDefault(x => x.SourceEmployeeId == request.ToEmployeeId);
        if (sameEmployee is not null)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                $"Customer {sameEmployee.CustomerId} is already owned by the target employee.");
        }

        var selectedCustomerIds = selectedOwners.Select(owner => owner.CustomerId).ToList();
        var customers = await _writeDbContext.Customers
            .Where(customer =>
                customer.CompanyId == scope.CompanyId &&
                selectedCustomerIds.Contains(customer.CustomerId))
            .ToListAsync(cancellationToken);
        if (customers.Count != selectedOwners.Count)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                "One or more customers were changed before transfer. Please reload and try again.");
        }

        var now = _dateTimeProvider.Now;
        var logs = new List<TransferCustomersResultDto>();
        var ownerByCustomerId = selectedOwners.ToDictionary(x => x.CustomerId);

        foreach (var batch in selectedOwners.GroupBy(x => new
                 {
                     x.TransferType,
                     x.SourceEmployeeId,
                     x.SourceGroupId
                 }))
        {
            var batchCustomerIds = batch.Select(x => x.CustomerId).ToList();
            var logId = Guid.CreateVersion7();

            await _writeDbContext.CustomerTransferLogs.AddAsync(new CustomerTransferLog
            {
                Id = logId,
                FromEmployeeId = batch.Key.SourceEmployeeId,
                ToEmployeeId = request.ToEmployeeId,
                FromGroupId = batch.Key.SourceGroupId,
                ToGroupId = target.GroupId,
                Note = NormalizeNote(request.Note),
                TransferType = batch.Key.TransferType,
                CreatedBy = scope.EmployeeId,
                CreatedDate = now,
                CompanyId = scope.CompanyId
            }, cancellationToken);

            if (batch.Key.TransferType == TransferType.Saled)
            {
                await DeactivateAssignmentsAsync(batchCustomerIds, scope.EmployeeId, now, scope.CompanyId, cancellationToken);
            }
            else
            {
                await DeactivateLeadClaimsAsync(batchCustomerIds, now, scope.CompanyId, cancellationToken);
            }

            foreach (var customer in customers.Where(customer => batchCustomerIds.Contains(customer.CustomerId)))
            {
                var owner = ownerByCustomerId[customer.CustomerId];
                if (owner.TransferType == TransferType.Saled)
                {
                    ApplySaledTransfer(customer, request.ToEmployeeId, scope.EmployeeId, now);
                    await _writeDbContext.CustomerAssignments.AddAsync(new CustomerAssignment
                    {
                        Id = Guid.CreateVersion7(),
                        CustomerId = customer.CustomerId,
                        EmployeeId = request.ToEmployeeId,
                        GroupId = target.GroupId,
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
                    ApplyLeadTransfer(customer, scope.EmployeeId, now);
                    await _writeDbContext.CustomerClaims.AddAsync(new CustomerClaim
                    {
                        Id = Guid.CreateVersion7(),
                        CustomerId = customer.CustomerId,
                        EmployeeId = request.ToEmployeeId,
                        GroupId = target.GroupId,
                        Type = ClaimType.Work,
                        ExpiresAt = now.AddDays(CustomerTransferRules.LeadClaimDaysAfterTransfer),
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

            logs.Add(new TransferCustomersResultDto
            {
                TransferLogId = logId,
                TransferredCount = batchCustomerIds.Count
            });
        }

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<ExecuteCustomerTransferResultDto>.Fail(
                BuildConcurrencyMessage(exception));
        }

        return OperationResult<ExecuteCustomerTransferResultDto>.Ok(new ExecuteCustomerTransferResultDto
        {
            TotalCount = selectedOwners.Count,
            LeadCount = selectedOwners.Count(x => x.TransferType == TransferType.Lead),
            SaledCount = selectedOwners.Count(x => x.TransferType == TransferType.Saled),
            TransferredCount = selectedOwners.Count,
            Logs = logs
        }, "Customers transferred successfully.");
    }

    private async Task<OperationResult<List<CustomerTransferOwnerRow>>> ResolveSelectedOwnersAsync(
        ExecuteCustomerTransferRequest request,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        if (request.TransferAll)
        {
            if (request.SourceEmployeeId is null || request.SourceEmployeeId == Guid.Empty)
            {
                return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                    "SourceEmployeeId is required when TransferAll is true.");
            }

            if (request.SourceCustomerId.HasValue || request.CustomerIds.Count > 0)
            {
                return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                    "SourceCustomerId and CustomerIds must be empty when TransferAll is true.");
            }
        }

        var owners = await CustomerTransferRules.BuildVisibleOwnerRowsAsync(
            _readDbContext,
            _visibilityService,
            scope,
            cancellationToken);

        IEnumerable<CustomerTransferOwnerRow> selected = owners;
        if (request.TransferAll)
        {
            selected = selected.Where(x => x.SourceEmployeeId == request.SourceEmployeeId);
        }
        else if (request.SourceCustomerId is { } sourceCustomerId && sourceCustomerId != Guid.Empty)
        {
            selected = selected.Where(x => x.CustomerId == sourceCustomerId);
        }
        else
        {
            var customerIds = request.CustomerIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            if (customerIds.Count == 0)
            {
                return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                    "CustomerIds or SourceCustomerId is required when TransferAll is false.");
            }

            if (customerIds.Count != request.CustomerIds.Count)
            {
                return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                    "CustomerIds contains empty or duplicate values.");
            }

            selected = selected.Where(x => customerIds.Contains(x.CustomerId));
        }

        if (!request.TransferAll &&
            request.SourceEmployeeId is { } sourceEmployeeId &&
            sourceEmployeeId != Guid.Empty)
        {
            selected = selected.Where(x => x.SourceEmployeeId == sourceEmployeeId);
        }

        var materialized = selected.ToList();
        if (materialized.Count == 0)
        {
            return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                "No visible transfer customers were found for the selected source.");
        }

        if (!request.TransferAll && request.SourceCustomerId is null)
        {
            var expectedCount = request.CustomerIds.Where(id => id != Guid.Empty).Distinct().Count();
            if (materialized.Count != expectedCount)
            {
                return OperationResult<List<CustomerTransferOwnerRow>>.Fail(
                    "One or more customers were not found or are outside your transfer scope.");
            }
        }

        return OperationResult<List<CustomerTransferOwnerRow>>.Ok(materialized);
    }

    private async Task<TargetEmployee?> ResolveTargetAsync(
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
                member.ProfileNavigation != null &&
                member.ProfileNavigation.IsActive &&
                member.ProfileNavigation.CompanyId == scope.CompanyId &&
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
            .Select(member => new TargetEmployee(
                member.Profile!.Value,
                member.ProfileNavigation!.FullName,
                member.GroupId,
                member.Group.Name))
            .FirstOrDefaultAsync(cancellationToken);
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
        Guid companyId,
        CancellationToken cancellationToken)
        => _writeDbContext.CustomerClaims
            .Where(claim =>
                claim.CompanyId == companyId &&
                claim.IsActive &&
                claim.Type == ClaimType.Work &&
                claim.ExpiresAt > now &&
                customerIds.Contains(claim.CustomerId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(claim => claim.IsActive, false),
                cancellationToken);

    private static void ApplySaledTransfer(
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

    private static void ApplyLeadTransfer(
        Customer customer,
        Guid actorEmployeeId,
        DateTime now)
    {
        customer.CurrentSaleId = null;
        customer.IsLead = true;
        customer.LeadStatus = LeadStatus.Claimed;
        customer.UpdatedBy = actorEmployeeId;
        customer.UpdatedDate = now;
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static string BuildConcurrencyMessage(DbUpdateConcurrencyException exception)
    {
        var entityNames = exception.Entries
            .Select(entry => entry.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var suffix = entityNames.Count == 0
            ? string.Empty
            : $" Affected entities: {string.Join(", ", entityNames)}.";

        return $"Customer transfer data was changed or deleted by another process. Please reload before saving.{suffix}";
    }

    private sealed record TargetEmployee(Guid EmployeeId, string EmployeeName, Guid GroupId, string? GroupName);
}
