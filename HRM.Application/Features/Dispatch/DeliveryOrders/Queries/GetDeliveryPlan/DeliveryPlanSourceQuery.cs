using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Application.Commons.Rules;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;

internal static class DeliveryPlanSourceQuery
{
    internal static IQueryable<DeliveryPlanSource> Project(
        IQueryable<MerchandiseOrder> orders, IQueryable<MfgOrderPO> links,
        IQueryable<DeliveryOrderDetail> details, Guid companyId, IQueryable<Employee> employees)
    {
        var activeLinks = ActiveLinks(links, companyId);
        var completed = CountedAsDelivered(details, companyId);
        return orders.Where(x => companyId != Guid.Empty && x.CompanyId == companyId && x.IsActive &&
                (x.CustomerExternalIdSnapshot == null || x.CustomerExternalIdSnapshot.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId) &&
                (x.Customer == null || x.Customer.ExternalId == null || x.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId) &&
                x.Status.Trim().ToLower() != "cancelled" && x.Status.Trim().ToLower() != "canceled" &&
                x.Status.Trim().ToLower() != "delivered" && x.Status.Trim().ToLower() != "completed")
            .Select(order => new DeliveryPlanSource
            {
                Id = order.MerchandiseOrderId, ExternalId = order.ExternalId, PONo = order.PONo, Status = order.Status,
                CustomerId = order.CustomerId, CustomerName = order.CustomerNameSnapshot,
                SalesEmployeeFullName = order.Customer != null && order.Customer.CompanyId == companyId && order.Customer.IsActive != false
                    ? employees.Where(employee => employee.CompanyId == companyId && employee.IsActive &&
                        employee.EmployeeId == (order.Customer.CustomerAssignments
                            .Where(assignment => assignment.IsActive && assignment.CompanyId == companyId)
                            .OrderByDescending(assignment => assignment.CreatedDate).ThenByDescending(assignment => assignment.Id)
                            .Select(assignment => (Guid?)assignment.EmployeeId).FirstOrDefault() ?? order.Customer.CurrentSaleId))
                        .Select(employee => employee.FullName).FirstOrDefault()
                    : null,
                Receiver = order.Receiver, Phone = order.PhoneSnapshot, Address = order.DeliveryAddress, Note = order.Note,
                IsDeliveryPaused = order.IsDeliveryPaused, DeliveryPauseReason = order.DeliveryPauseReason,
                DeliveryPausedFrom = order.DeliveryPausedFrom, DeliveryPausedTo = order.DeliveryPausedTo,
                PromisedDate = activeLinks.Where(x => x.Detail.MerchandiseOrderId == order.MerchandiseOrderId)
                    .Max(x => x.ProductionOrder.ExpectedDate),
                DeliveredQuantity = completed.Where(x => x.MerchandiseOrderDetail!.MerchandiseOrderId == order.MerchandiseOrderId)
                    .Sum(x => (decimal?)x.Quantity) ?? 0m,
                // Floor trên từng dòng: giao dư sản phẩm A không bù được phần thiếu sản phẩm B.
                RemainingQuantity = order.MerchandiseOrderDetails.Where(x => x.IsActive)
                    .Sum(line => (decimal?)Math.Max(0m, line.ExpectedQuantity -
                        (completed.Where(d => d.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId)
                            .Sum(d => (decimal?)d.Quantity) ?? 0m))) ?? 0m
            });
    }

    // Đồng nhất timeline: cộng phiếu còn hiệu lực, không phụ thuộc trạng thái chốt PO.
    // Dùng chung cho lọc/phân trang và lượng chi tiết để hai phần không lệch nhau.
    internal static IQueryable<DeliveryOrderDetail> CountedAsDelivered(IQueryable<DeliveryOrderDetail> details, Guid companyId)
        => details.CountedForAllocation(companyId)
            .Where(x => x.MerchandiseOrderDetail != null && x.MerchandiseOrderDetail.IsActive &&
                x.MerchandiseOrderDetail.MerchandiseOrder.IsActive &&
                x.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == companyId);

    internal static IQueryable<MfgOrderPO> ActiveLinks(IQueryable<MfgOrderPO> links, Guid companyId)
        => links.Where(x => companyId != Guid.Empty && x.IsActive && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive &&
            x.Detail.MerchandiseOrder.CompanyId == companyId && x.ProductionOrder.CompanyId == companyId && x.ProductionOrder.IsActive &&
            x.ProductionOrder.Status.Trim().ToLower() != "canceled" && x.ProductionOrder.Status.Trim().ToLower() != "cancelled");
}

internal sealed class DeliveryPlanSource
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? SalesEmployeeFullName { get; init; }
    public string? Receiver { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? Note { get; init; }
    public bool IsDeliveryPaused { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public DateTime? PromisedDate { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
}
