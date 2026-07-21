using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Factories;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByProductType
{
    internal sealed class GetExecutivePnLDashboardByProductTypeQueryHandler
        : IRequestHandler<GetExecutivePnLDashboardByProductTypeQuery, ExecutivePnLAnalysisTabDto>
    {
        private readonly ExecutivePnLProductTypeReader _reader;
        private readonly ICurrentUser _currentUser;
        public GetExecutivePnLDashboardByProductTypeQueryHandler(IReportReadDbContext dbContext, ICurrentUser currentUser)
        {
            _currentUser = currentUser;
            _reader = new ExecutivePnLProductTypeReader(dbContext, currentUser);
        }

        public async Task<ExecutivePnLAnalysisTabDto> Handle(
            GetExecutivePnLDashboardByProductTypeQuery query,
            CancellationToken cancellationToken)
        {
            var period = ExecutivePnLPeriod.Create(query.FromMonth, query.ToMonth);


            var filter = ExecutivePnLDashboardFilterFactory.CreateProductTypeFilter(
                query.CompanyId,
                query.BusinessUnit,
                query.Currency,
                GetComparisonFromMonth(period.FromMonth, query.PeriodType),
                query.ToMonth);

            var rows = await _reader.ReadMonthlyAsync(filter, cancellationToken);

            return ExecutivePnLDashboardBuilder.BuildProductTypeAnalysisTab(
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

