using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Factories;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales
{
    internal sealed class GetExecutivePnLDashboardBySalesQueryHandler
        : IRequestHandler<GetExecutivePnLDashboardBySalesQuery, ExecutivePnLAnalysisTabDto>
    {
        private readonly ExecutivePnLSalesReader _reader;
        private readonly ICurrentUser _currentUser;

        public GetExecutivePnLDashboardBySalesQueryHandler(IReportReadDbContext dbContext, ICurrentUser currentUser)
        {
            _currentUser = currentUser;
            _reader = new ExecutivePnLSalesReader(dbContext, currentUser);
        }

        public async Task<ExecutivePnLAnalysisTabDto> Handle(
            GetExecutivePnLDashboardBySalesQuery query,
            CancellationToken cancellationToken)
        {
            var period = ExecutivePnLPeriod.Create(query.FromMonth, query.ToMonth);
            var filter = ExecutivePnLDashboardFilterFactory.CreateSaleFilter(
                query.CompanyId,
                query.BusinessUnit,
                query.Currency,
                GetComparisonFromMonth(period.FromMonth, query.PeriodType),
                query.ToMonth,
                query.SaleGroup,
                query.SalePerson);
            var rows = await _reader.ReadMonthlyAsync(filter, cancellationToken);

            return ExecutivePnLDashboardBuilder.BuildSalesAnalysisTab(
                period.Months,
                rows,
                query.TopN,
                query.PeriodType);
        }

        private static DateTime GetComparisonFromMonth(
            DateTime fromMonth,
            ExecutivePnLDashboardPeriodType periodType)
        {
            return periodType switch
            {
                ExecutivePnLDashboardPeriodType.Year => fromMonth.AddYears(-1),
                ExecutivePnLDashboardPeriodType.Quarter => fromMonth.AddMonths(-3),
                _ => fromMonth.AddMonths(-1)
            };
        }
    }
}

