using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed class CustomerSampleSharingService(
    ICustomerSampleSharingDbContext db, ICustomerVisibilityService visibility,
    IOptions<CustomerSampleSharingOptions> options)
{
    public bool Enabled => options.Value.Enabled;

    public async Task<Guid[]> ManageableCustomerIdsAsync(Guid company, bool companyManager, CancellationToken ct)
    {
        var scope = await visibility.BuildScopeAsync(ct);
        if (scope.CompanyId != company || scope.EmployeeId == Guid.Empty) return [];
        // Read-all roles must not expand the scoped sample-sharing mutation grant.
        var query = companyManager ? db.Customers.AsNoTracking()
            : visibility.ApplyCustomerVisibility(db.Customers.AsNoTracking(), scope with { HasFullCustomerView = false });
        return await query.Where(x => x.CompanyId == company && x.IsActive == true
            && x.ExternalId != PLMCustomerRules.InternalCustomerExternalId).Select(x => x.CustomerId).ToArrayAsync(ct);
    }

    // Shared by customer-scoped sample lookup and document validation.
    // null = unauthorized target customer; empty and null are deliberately distinct.
    public async Task<Guid[]?> ResolveOwnerIdsAsync(Guid company, Guid customer, CancellationToken ct, bool forWrite = false)
    {
        if (!Enabled) return [customer];
        var scope = await visibility.BuildReadScopeAsync(ct);
        if (scope.CompanyId != company || company == Guid.Empty || scope.EmployeeId == Guid.Empty) return null;
        var visible = visibility.ApplyCustomerVisibility(db.Customers.AsNoTracking(), scope);
        var allowed = await visible.AnyAsync(x => x.CustomerId == customer && x.CompanyId == company && x.IsActive == true, ct)
            || await db.Customers.AsNoTracking().AnyAsync(x => x.CustomerId == customer && x.CompanyId == company
                && x.IsActive == true && x.ExternalId == PLMCustomerRules.InternalCustomerExternalId, ct);
        if (!allowed) return null;
        if (forWrite) await db.LockCustomerSampleSharingAsync(company, ct);
        var groupIds = db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company
            && x.CustomerId == customer && x.IsActive && x.Group.CompanyId == company && x.Group.IsActive)
            .Select(x => x.GroupId);
        var owners = await db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company
            && x.IsActive && groupIds.Contains(x.GroupId) && x.Group.CompanyId == company && x.Group.IsActive
            && x.Customer.CompanyId == company && x.Customer.IsActive == true)
            .Select(x => x.CustomerId).Distinct().ToArrayAsync(ct);
        return owners.Append(customer).Distinct().ToArray();
    }

    public Task<Guid[]> EligibleProductsAsync(Guid company, Guid[] owners, Guid[] products, CancellationToken ct)
        => db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive
            && x.Customer.CompanyId == company && x.Customer.IsActive == true
            && x.Product.CompanyId == company && x.Product.IsActive && products.Contains(x.ProductId)
            && (owners.Contains(x.CustomerId) || x.Customer.ExternalId == PLMCustomerRules.InternalCustomerExternalId)
            && (x.Status == nameof(SampleRequestStatus.SampleSent) || x.Status == nameof(SampleRequestStatus.Completed)))
            .Select(x => x.ProductId).Distinct().ToArrayAsync(ct);
}
