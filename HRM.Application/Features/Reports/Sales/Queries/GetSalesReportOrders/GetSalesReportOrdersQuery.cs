using HRM.Application.Commons.Models;
using HRM.Application.Features.Reports.Sales.Shared;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;

/// <summary>Danh sách đơn đối soát KPI header, cùng kỳ ngày và customer attribution.</summary>
public sealed class GetSalesReportOrdersQuery : SalesReportDrawerRequest,
    IRequest<OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>>
{
    public string? Metric { get; init; }
}
