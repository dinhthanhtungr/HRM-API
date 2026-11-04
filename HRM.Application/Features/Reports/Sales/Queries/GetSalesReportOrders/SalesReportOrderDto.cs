using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;

public sealed class SalesReportOrderDto
{
    public Guid OrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public OrderType OrderType { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    /// <summary>Ngày hứa giao sớm nhất từ MfgProductionOrder.ExpectedDate qua MfgOrderPO; chỉ xét dòng còn thiếu ở metric chờ giao/quá hạn.</summary>
    public DateTime? PromisedDeliveryDate { get; set; }
    public decimal TotalQuantity { get; init; }
    /// <summary>Lượng giao hợp lệ của các dòng active trong toàn đơn, lũy kế đến hết To.</summary>
    public decimal DeliveredQuantity { get; set; }
    public decimal? DeliveredQuantityInPeriod { get; set; }
    public decimal? PendingQuantity { get; set; }
    public DateTime? EarliestPendingRequestedDate { get; set; }
}
