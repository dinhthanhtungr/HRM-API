namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services
{
    public class ExecutivePnLFilter
    {
        public Guid? CompanyId { get; init; }
        public string? BusinessUnit { get; init; }
        public string? Currency { get; init; }
        public DateTime FromMonth { get; init; }
        public DateTime ToMonth { get; init; }
        public DateTime RangeEnd => ToMonth.AddMonths(1);
    }
}

