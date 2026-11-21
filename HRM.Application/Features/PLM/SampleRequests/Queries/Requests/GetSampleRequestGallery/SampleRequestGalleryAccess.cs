using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;

internal static class SampleRequestGalleryAccess
{
    public static async Task<IQueryable<SampleRequest>> ReadableAsync(
        IPLMReadDbContext db, ICustomerVisibilityService visibility, Guid id, CancellationToken ct)
    {
        var scope = await visibility.BuildReadScopeAsync(ct);
        // Summary cho phép tìm TP nội bộ bằng keyword; mở ảnh của TP đó vẫn giữ company/ownership.
        return visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(),
                db.Customers.AsNoTracking(), scope with { CanViewInternalCustomer = true })
            .Where(x => x.SampleRequestId == id && x.CompanyId == scope.CompanyId && x.IsActive &&
                (x.RequestType == null || x.RequestType != SemiFinishedRequestRules.RequestType));
    }
}
