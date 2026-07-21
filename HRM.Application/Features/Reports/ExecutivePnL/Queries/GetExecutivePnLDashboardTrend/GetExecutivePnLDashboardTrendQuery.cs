using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using MediatR;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardTrend
{
    public sealed class GetExecutivePnLDashboardTrendQuery : IRequest<ExecutivePnLDashboardTabDto>
    {
        public Guid? CompanyId { get; set; }
        public string? BusinessUnit { get; set; }
        public string? Currency { get; set; }
        public DateTime? FromMonth { get; set; }
        public DateTime? ToMonth { get; set; }
    }
}

