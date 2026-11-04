using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport
{
    internal sealed class GetExecutivePnLReportQueryHandler
        : IRequestHandler<GetExecutivePnLReportQuery, ExecutivePnLReportDto>
    {
        private readonly ExecutivePnLActualReader _actualReader;
        private readonly ExecutivePnLReportBuilder _reportBuilder;

        private readonly ICurrentUser _currentUser;

        public GetExecutivePnLReportQueryHandler(IReportReadDbContext dbContext, ICurrentUser currentUser)
        {
            _currentUser = currentUser;
            _actualReader = new ExecutivePnLActualReader(dbContext);
            _reportBuilder = new ExecutivePnLReportBuilder();
        }

        public async Task<ExecutivePnLReportDto> Handle(
            GetExecutivePnLReportQuery request,
            CancellationToken cancellationToken)
        {
            var period = ExecutivePnLPeriod.Create(request.FromMonth, request.ToMonth);
            var filter = new ExecutivePnLFilter
            {
                CompanyId = ReportCompanyScope.Resolve(_currentUser, request.CompanyId),
                BusinessUnit = request.BusinessUnit,
                Currency = request.Currency,
                FromMonth = period.FromMonth,
                ToMonth = period.ToMonth
            };

            var actuals = await _actualReader.GetActualsAsync(
                filter,
                period.Months,
                cancellationToken);

            return _reportBuilder.Build(filter, period.Months, actuals);
        }
    }
}

