using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

internal sealed class SampleProductionTrackingReader(
    IPLMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    internal IQueryable<SampleProductionTrackingDto>? Read()
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewSampleProductionTracking) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        var samples = SampleRequestTrackingReader.VisibleRequests(db, company);
        var visibleIds = samples.Select(sample => sample.SampleRequestId);
        return db.ManufacturingVUFormulas.AsNoTracking()
            .Where(order => order.Formula.Product.CompanyId == company &&
                (order.Formula.CompanyId == null || order.Formula.CompanyId == company) &&
                !db.SampleRequests.Any(sample => sample.IsActive && sample.FormulaId == order.FormulaId &&
                    sample.ProductId == order.Formula.ProductId && !visibleIds.Contains(sample.SampleRequestId)))
            .SelectMany(order => samples.Where(sample => sample.FormulaId == order.FormulaId &&
                    sample.ProductId == order.Formula.ProductId)
                .OrderByDescending(sample => sample.CreatedDate).ThenBy(sample => sample.SampleRequestId).Take(1),
                (order, sample) => new SampleProductionTrackingDto
                {
                    Id = order.ManufacturingVUFormulaId,
                    SampleRequestId = sample.SampleRequestId,
                    SampleRequestCode = sample.ExternalId,
                    ProductCode = sample.Product.Code,
                    ProductName = sample.Product.Name,
                    Status = order.status == ManufacturingProductOrder.New ? "New" :
                    order.status == ManufacturingProductOrder.Canceled ? "Canceled" :
                    order.status == ManufacturingProductOrder.Finished ||
                    order.status == ManufacturingProductOrder.Done ||
                    order.status == ManufacturingProductOrder.Stocked ? "Completed" : "InProgress",
                    ProductionQuantity = order.TotalProductionQuantity,
                    CreatedDate = order.CreatedDate,
                });
    }
}
