using HRM.Application.Commons.Models;
using HRM.Application.Features.Reports.Sales.Shared;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;

/// <summary>Khách có giao trong kỳ, cùng nguồn giao và attribution của header.</summary>
public sealed class GetSalesReportCustomersQuery : SalesReportDrawerRequest,
    IRequest<OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>>
{
    public string? Metric { get; init; }
}
