using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

/// <summary>Header KPI sale theo kỳ ngày, dùng nguồn doanh thu và attribution của Executive P&amp;L.</summary>
public sealed class GetSalesReportHeaderQuery : IRequest<OperationResult<SalesReportHeaderDto>>
{
    public Guid? CompanyId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid? SaleGroup { get; init; }
    public Guid? SalePerson { get; init; }
}
