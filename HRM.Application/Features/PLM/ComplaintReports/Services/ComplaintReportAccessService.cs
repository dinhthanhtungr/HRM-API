using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

/// <summary>
/// Applies the shared customer ownership and company visibility boundary for complaint commands.
/// </summary>
internal sealed class ComplaintReportAccessService
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public ComplaintReportAccessService(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<bool> CanAccessCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        return await _visibilityService.ApplyCustomerVisibility(
                _dbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.CustomerId == customerId && x.CompanyId == scope.CompanyId, cancellationToken);
    }
}
