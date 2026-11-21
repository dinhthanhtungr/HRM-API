using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

internal sealed class QuotationTrackingReader(
    ICRMReadDbContext db, ICustomerVisibilityService visibility,
    ICurrentUser user, ICurrentUserPermissionService permissions)
{
    internal async Task<IQueryable<Quotation>?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!permissions.HasPermission(ApplicationPermissions.Quotations.ViewTracking) ||
            user.CompanyId.GetValueOrDefault() == Guid.Empty || user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        var scope = await visibility.BuildScopeAsync(cancellationToken);
        return visibility.ApplyQuotationVisibility(
            db.Quotations.AsNoTracking(), db.Customers.AsNoTracking(),
            scope with { HasFullCustomerView = true, CanViewInternalCustomer = false })
            .Where(quotation => quotation.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                quotation.Customer.ExternalId != null &&
                quotation.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId);
    }

    internal static IQueryable<QuotationTrackingDto> Project(IQueryable<Quotation> query) =>
        query.Select(quotation => new QuotationTrackingDto(
            quotation.QuotationId, quotation.ExternalId, quotation.Customer.CustomerName,
            quotation.Status == QuotationStatus.Sent ? "Sent" :
            quotation.Status == QuotationStatus.Accepted ? "Accepted" :
            quotation.Status == QuotationStatus.Rejected ? "Rejected" :
            quotation.Status == QuotationStatus.Cancelled ? "Cancelled" :
            quotation.Status == QuotationStatus.Expired ? "Expired" : "InProgress",
            quotation.QuotationDate));
}
