using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Factories;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardTrend
{
    internal sealed class GetExecutivePnLDashboardTrendQueryHandler
        : IRequestHandler<GetExecutivePnLDashboardTrendQuery, ExecutivePnLDashboardTabDto>
    {
        private readonly ExecutivePnLTrendReader _reader;

        public GetExecutivePnLDashboardTrendQueryHandler(IReportReadDbContext dbContext)
        {
            _reader = new ExecutivePnLTrendReader(dbContext);
        }

        public async Task<ExecutivePnLDashboardTabDto> Handle(
            GetExecutivePnLDashboardTrendQuery query,
            CancellationToken cancellationToken)
        {
            var filter = ExecutivePnLDashboardFilterFactory.Create(
                query.CompanyId,
                query.BusinessUnit,
                query.Currency,
                query.FromMonth,
                query.ToMonth);
            var rows = await _reader.ReadAsync(filter, cancellationToken);

            return ExecutivePnLDashboardBuilder.BuildTrendTab(rows);
        }
    }
}

