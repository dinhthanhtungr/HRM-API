using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByProductType
{
    public sealed class GetExecutivePnLDashboardByProductTypeQuery : IRequest<ExecutivePnLAnalysisTabDto>
    {
        public Guid? CompanyId { get; set; }
        public string? BusinessUnit { get; set; }
        public string? Currency { get; set; }
        public DateTime? FromMonth { get; set; }
        public DateTime? ToMonth { get; set; }
        public ExecutivePnLDashboardPeriodType PeriodType { get; set; } = ExecutivePnLDashboardPeriodType.Month;
        public int TopN { get; set; } = 10;
    }
}

