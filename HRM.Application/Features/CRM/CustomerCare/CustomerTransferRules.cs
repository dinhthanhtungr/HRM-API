using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare;

internal static class CustomerTransferRules
{
    public const int LeadClaimDaysAfterTransfer = 365;
    public const string TransferPermissionMessage = "Only sale leaders or full-view users can transfer customers.";

    public static bool CanTransfer(ViewerScope scope)
        => scope.HasFullCustomerView || scope.LeaderGroupIds.Count > 0;

    public static async Task<List<CustomerTransferOwnerRow>> BuildVisibleOwnerRowsAsync(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        ViewerScope scope,
        CancellationToken cancellationToken)
    {
        var visibleCustomers = visibilityService
            .ApplyCustomerVisibility(dbContext.Customers.AsNoTracking(), scope)
            .Where(customer => customer.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId);

        var rows = await visibleCustomers
            .Select(customer => new
            {
                customer.CustomerId,
                customer.ExternalId,
                customer.CustomerName,
                customer.IsLead,
                LeadOwner = customer.CustomerClaims
                    .Where(claim =>
                        customer.IsLead &&
                        claim.IsActive &&
                        claim.Type == ClaimType.Work &&
                        claim.ExpiresAt > scope.Now)
                    .OrderByDescending(claim => claim.ExpiresAt)
                    .Select(claim => new
                    {
                        claim.EmployeeId,
                        EmployeeName = claim.Employee.FullName,
                        claim.GroupId,
                        GroupName = claim.Group.Name,
                        LeadExpiresAt = (DateTime?)claim.ExpiresAt
                    })
                    .FirstOrDefault(),
                SaledOwner = customer.CustomerAssignments
                    .Where(assignment => !customer.IsLead && assignment.IsActive)
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => new
                    {
                        assignment.EmployeeId,
                        EmployeeName = assignment.Employee.FullName,
                        assignment.GroupId,
                        GroupName = assignment.Group.Name,
                        LeadExpiresAt = (DateTime?)null
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new { Row = row, Owner = row.IsLead ? row.LeadOwner : row.SaledOwner })
            .Where(x => x.Owner is not null)
            .Where(x => scope.HasFullCustomerView || scope.LeaderGroupIds.Contains(x.Owner!.GroupId))
            .Select(x => new CustomerTransferOwnerRow(
                x.Row.CustomerId,
                x.Row.ExternalId,
                x.Row.CustomerName,
                x.Row.IsLead ? TransferType.Lead : TransferType.Saled,
                x.Owner!.EmployeeId,
                x.Owner.EmployeeName,
                x.Owner.GroupId,
                x.Owner.GroupName,
                x.Owner.LeadExpiresAt))
            .ToList();
    }

    public static CustomerTransferCustomerOptionDto ToCustomerOption(CustomerTransferOwnerRow row)
        => new()
        {
            CustomerId = row.CustomerId,
            ExternalId = row.ExternalId,
            CustomerName = row.CustomerName,
            IsLead = row.TransferType == TransferType.Lead,
            TransferType = row.TransferType,
            SourceEmployeeId = row.SourceEmployeeId,
            SourceEmployeeName = row.SourceEmployeeName,
            SourceGroupId = row.SourceGroupId,
            SourceGroupName = row.SourceGroupName,
            LeadExpiresAt = row.LeadExpiresAt
        };

    public static CustomerTransferOwnerDto ToOwnerDto(CustomerTransferOwnerRow row)
        => new()
        {
            SourceEmployeeId = row.SourceEmployeeId,
            SourceEmployeeName = row.SourceEmployeeName,
            SourceGroupId = row.SourceGroupId,
            SourceGroupName = row.SourceGroupName,
            TransferType = row.TransferType,
            IsLead = row.TransferType == TransferType.Lead
        };
}

internal sealed record CustomerTransferOwnerRow(
    Guid CustomerId,
    string ExternalId,
    string CustomerName,
    TransferType TransferType,
    Guid SourceEmployeeId,
    string SourceEmployeeName,
    Guid SourceGroupId,
    string? SourceGroupName,
    DateTime? LeadExpiresAt);
