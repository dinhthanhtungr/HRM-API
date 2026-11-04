namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

public sealed record SalesReportMonthlyRevenueDto(DateTime Month, DateTime From, DateTime To, decimal Revenue);
