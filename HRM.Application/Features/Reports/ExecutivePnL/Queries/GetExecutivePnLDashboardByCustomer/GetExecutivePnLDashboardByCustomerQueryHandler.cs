using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Factories;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByCustomer
{
    internal sealed class GetExecutivePnLDashboardByCustomerQueryHandler
        : IRequestHandler<GetExecutivePnLDashboardByCustomerQuery, ExecutivePnLAnalysisTabDto>
    {
        private readonly ExecutivePnLCustomerReader _reader;

        public GetExecutivePnLDashboardByCustomerQueryHandler(IReportReadDbContext dbContext)
        {
            _reader = new ExecutivePnLCustomerReader(dbContext);
        }

        public async Task<ExecutivePnLAnalysisTabDto> Handle(
            GetExecutivePnLDashboardByCustomerQuery query,
            CancellationToken cancellationToken)
        {
            var filter = ExecutivePnLDashboardFilterFactory.Create(
                query.CompanyId,
                query.BusinessUnit,
                query.Currency,
                query.FromMonth,
                query.ToMonth);

            var rows = await _reader.ReadAsync(filter, query.TopN, cancellationToken);

            return ExecutivePnLDashboardBuilder.BuildCustomerAnalysisTab(rows);
        }
    }
}

