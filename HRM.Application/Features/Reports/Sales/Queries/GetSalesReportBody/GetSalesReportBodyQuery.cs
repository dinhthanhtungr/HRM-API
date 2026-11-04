using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;

public enum SalesReportDimension { Sale = 1, Product = 2, CustomerType = 3 }

public sealed class GetSalesReportBodyQuery : IRequest<OperationResult<SalesReportBodyDto>>
{
    public Guid? CompanyId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid? SaleGroup { get; init; }
    public Guid? SalePerson { get; init; }
    public SalesReportDimension GroupBy { get; init; } = SalesReportDimension.Sale;
}
