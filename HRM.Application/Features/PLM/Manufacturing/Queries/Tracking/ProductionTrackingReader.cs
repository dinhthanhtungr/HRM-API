using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

internal sealed class ProductionTrackingReader(
    IManufacturingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    internal IQueryable<MfgProductionOrder>? Read()
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionTracking) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        return db.MfgProductionOrders.AsNoTracking().Where(order => order.IsActive && order.CompanyId == company &&
            order.Product.CompanyId == company && order.Product.IsActive &&
            order.CustomerId.HasValue && order.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            order.Customer != null && order.Customer.CompanyId == company && order.Customer.IsActive == true &&
            order.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId &&
            !db.SampleRequests.Any(sample => sample.ProductId == order.ProductId && sample.CompanyId == company &&
                sample.IsActive && sample.RequestType == SemiFinishedRequestRules.RequestType));
    }

    internal static IQueryable<ProductionTrackingDto> Project(IQueryable<MfgProductionOrder> query) =>
        query.Select(order => new ProductionTrackingDto(order.MfgProductionOrderId, order.ExternalId,
            order.ProductExternalIdSnapshot, order.ProductNameSnapshot,
            order.Status == nameof(ManufacturingProductOrder.New) ? "New" :
            order.Status == nameof(ManufacturingProductOrder.Canceled) ? "Canceled" :
            order.Status == nameof(ManufacturingProductOrder.Finished) ||
            order.Status == nameof(ManufacturingProductOrder.Done) ||
            order.Status == nameof(ManufacturingProductOrder.Stocked) ? "Completed" : "InProgress",
            order.TotalQuantityRequest, order.TotalQuantity, order.RequiredDate,
            order.ExpectedDate, order.ManufacturingDate));
}
