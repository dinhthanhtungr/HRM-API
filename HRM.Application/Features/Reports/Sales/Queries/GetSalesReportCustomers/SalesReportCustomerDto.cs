namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;

public sealed class SalesReportCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int DeliveredOrderCount { get; init; }
    public decimal DeliveredQuantityInPeriod { get; init; }
}
