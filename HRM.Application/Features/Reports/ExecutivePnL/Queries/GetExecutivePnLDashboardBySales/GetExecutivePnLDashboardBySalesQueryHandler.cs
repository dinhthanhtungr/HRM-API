using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Authorization;
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
        private readonly ICurrentUserPermissionService _permissions;

        public GetExecutivePnLDashboardBySalesQueryHandler(IReportReadDbContext dbContext, ICurrentUser currentUser, ICurrentUserPermissionService permissions)
        {
            _currentUser = currentUser;
            _permissions = permissions;
            _reader = new ExecutivePnLSalesReader(dbContext, currentUser);
        }

        public async Task<ExecutivePnLAnalysisTabDto> Handle(
            GetExecutivePnLDashboardBySalesQuery query,
            CancellationToken cancellationToken)
        {
            var period = ExecutivePnLPeriod.Create(query.FromMonth, query.ToMonth);
            if (!_currentUser.IsAuthenticated
                || !_currentUser.IsInAnyRole(ApplicationRoleSets.Reports.SalesScopedReaders))
                throw new UnauthorizedAccessException("Không có quyền xem báo cáo sale.");
            var filter = ExecutivePnLDashboardFilterFactory.CreateSaleFilter(
                ReportCompanyScope.Resolve(_currentUser, query.CompanyId),
                query.BusinessUnit,
                query.Currency,
                GetComparisonFromMonth(period.FromMonth, query.PeriodType),
                query.ToMonth,
                query.SaleGroup,
                query.SalePerson);
            var canViewProfit = _permissions.HasPermission(ApplicationPermissions.Reports.ViewSalesProfit);
            var rows = await _reader.ReadMonthlyAsync(filter, cancellationToken, canViewProfit);

            return ExecutivePnLDashboardBuilder.BuildSalesAnalysisTab(
                period.Months,
                rows,
                query.TopN,
                query.PeriodType, canViewProfit);
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

