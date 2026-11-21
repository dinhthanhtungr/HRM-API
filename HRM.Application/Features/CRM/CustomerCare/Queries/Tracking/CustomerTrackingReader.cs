using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

internal sealed class CustomerTrackingReader(
    ICRMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions, IDateTimeProvider clock)
{
    internal IQueryable<Customer>? Read()
    {
        if (!permissions.HasPermission(ApplicationPermissions.CRM.ViewCustomerTracking) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        return db.Customers.AsNoTracking().Where(customer => customer.CompanyId == companyId &&
            customer.IsActive == true && customer.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId);
    }

    internal IQueryable<CustomerTrackingDto> Project(IQueryable<Customer> query)
    {
        var now = clock.Now;
        return query.Select(customer => new CustomerTrackingDto(
            customer.CustomerId, customer.ExternalId, customer.CustomerName, customer.IsLead,
            (customer.IsLead
                ? customer.CustomerClaims.Where(claim => claim.IsActive && claim.Type == ClaimType.Work &&
                        claim.ExpiresAt > now && claim.CompanyId == customer.CompanyId &&
                        claim.Employee.CompanyId == customer.CompanyId)
                    .OrderByDescending(claim => claim.ExpiresAt).ThenBy(claim => claim.EmployeeId)
                    .Select(claim => claim.Employee.FullName).FirstOrDefault()
                : customer.CustomerAssignments.Where(assignment => assignment.IsActive &&
                        assignment.CompanyId == customer.CompanyId && assignment.Employee.CompanyId == customer.CompanyId)
                    .OrderByDescending(assignment => assignment.CreatedDate).ThenBy(assignment => assignment.EmployeeId)
                    .Select(assignment => assignment.Employee.FullName).FirstOrDefault())
            ?? db.Employees.Where(employee => employee.EmployeeId == customer.CurrentSaleId &&
                    employee.CompanyId == customer.CompanyId)
                .Select(employee => employee.FullName).FirstOrDefault()));
    }
}
