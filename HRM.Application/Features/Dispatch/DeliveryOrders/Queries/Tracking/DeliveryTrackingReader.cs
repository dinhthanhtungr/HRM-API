using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

internal sealed class DeliveryTrackingReader(
    IDispatchReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    internal IQueryable<DeliveryOrder>? Read()
    {
        if (!permissions.HasPermission(ApplicationPermissions.Dispatch.ViewDeliveryTracking) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        return db.DeliveryOrders.AsNoTracking().Where(order =>
            order.CompanyId == companyId && order.IsActive &&
            order.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            order.Customer.CompanyId == companyId && order.Customer.IsActive == true &&
            order.Customer.ExternalId != null &&
            order.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId);
    }

    internal static IQueryable<DeliveryTrackingDto> Project(IQueryable<DeliveryOrder> query) =>
        query.Select(order => new DeliveryTrackingDto(
            order.Id, order.ExternalId, order.Customer.CustomerName,
            order.Status == "Completed" || order.Status == "Delivered" ? "Completed" :
            order.Status == "Canceled" || order.Status == "Cancelled" ? "Canceled" :
            order.Status == "Pending" ? "Pending" : "InProgress",
            order.CreatedDate));

    internal static IQueryable<DeliveryTrackingLineDto> ProjectLines(IQueryable<DeliveryOrder> query) =>
        query.SelectMany(order => order.Details, (order, line) => new { Order = order, Line = line })
            .Where(row => row.Line.IsActive && !row.Line.IsAttach &&
                row.Line.MerchandiseOrderDetail != null &&
                row.Line.MerchandiseOrderDetail.IsActive &&
                row.Line.MerchandiseOrderDetail.MerchandiseOrder.IsActive &&
                row.Line.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == row.Order.CompanyId &&
                row.Line.MerchandiseOrderDetail.MerchandiseOrder.CustomerId == row.Order.CustomerId &&
                row.Line.MerchandiseOrderDetail.MerchandiseOrder.OrderType != OrderType.Internal)
            .OrderBy(row => row.Line.Id)
            .Select(row => new DeliveryTrackingLineDto(row.Line.Id, row.Line.ProductExternalIdSnapShot,
                row.Line.ProductNameSnapShot, row.Line.Quantity));
}
