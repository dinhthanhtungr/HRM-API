using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.ResolveTransferSource;

internal sealed class ResolveTransferSourceQueryHandler
    : IRequestHandler<ResolveTransferSourceQuery, OperationResult<TransferSourceResolutionDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public ResolveTransferSourceQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<TransferSourceResolutionDto>> Handle(
        ResolveTransferSourceQuery query,
        CancellationToken cancellationToken)
    {
        var request = query.Request;
        if (!Enum.IsDefined(request.TransferType))
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("TransferType is invalid.");
        }

        var customerIds = request.CustomerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (customerIds.Count == 0)
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("CustomerIds is required.");
        }

        if (customerIds.Count != request.CustomerIds.Count)
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("CustomerIds contains empty or duplicate values.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);

        if (!scope.HasFullCustomerView && scope.LeaderGroupIds.Count == 0)
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("Only sale leaders or full-view users can resolve transfer source.");
        }

        var visibleCustomerIds = await _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
            .Where(customer =>
                customer.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                customerIds.Contains(customer.CustomerId))
            .Select(customer => customer.CustomerId)
            .ToListAsync(cancellationToken);
        if (visibleCustomerIds.Count != customerIds.Count)
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("One or more customers were not found or are outside your visibility scope.");
        }

        var owners = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                customer.CompanyId == scope.CompanyId &&
                customerIds.Contains(customer.CustomerId))
            .Select(customer => new
            {
                customer.CustomerId,
                Owner = request.TransferType == TransferType.Saled
                    ? customer.CustomerAssignments
                        .Where(assignment => assignment.IsActive)
                        .OrderByDescending(assignment => assignment.CreatedDate)
                        .Select(assignment => new SourceOwner(
                            assignment.EmployeeId,
                            assignment.Employee.FullName,
                            assignment.GroupId,
                            assignment.Group.Name))
                        .FirstOrDefault()
                    : customer.CustomerClaims
                        .Where(claim =>
                            claim.IsActive &&
                            claim.Type == ClaimType.Work &&
                            claim.ExpiresAt > scope.Now)
                        .OrderByDescending(claim => claim.ExpiresAt)
                        .Select(claim => new SourceOwner(
                            claim.EmployeeId,
                            claim.Employee.FullName,
                            claim.GroupId,
                            claim.Group.Name))
                        .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        SourceOwner? sourceOwner = null;
        foreach (var row in owners)
        {
            var owner = row.Owner;

            if (owner is null)
            {
                return OperationResult<TransferSourceResolutionDto>.Fail(
                    $"Customer {row.CustomerId} has no active {request.TransferType} owner.");
            }

            if (sourceOwner is not null &&
                (sourceOwner.EmployeeId != owner.EmployeeId || sourceOwner.GroupId != owner.GroupId))
            {
                return OperationResult<TransferSourceResolutionDto>.Fail(
                    "Selected customers must have the same source employee and source group.");
            }

            sourceOwner = owner;
        }

        if (sourceOwner is null)
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("Source owner could not be resolved.");
        }

        if (!scope.HasFullCustomerView && !scope.LeaderGroupIds.Contains(sourceOwner.GroupId))
        {
            return OperationResult<TransferSourceResolutionDto>.Fail("Source group is outside your leader scope.");
        }

        return OperationResult<TransferSourceResolutionDto>.Ok(new TransferSourceResolutionDto
        {
            FromEmployeeId = sourceOwner.EmployeeId,
            FromEmployeeName = sourceOwner.EmployeeName,
            FromGroupId = sourceOwner.GroupId,
            FromGroupName = sourceOwner.GroupName,
            CustomerCount = owners.Count
        });
    }

    private sealed record SourceOwner(
        Guid EmployeeId,
        string EmployeeName,
        Guid GroupId,
        string? GroupName);
}
