namespace HRM.Application.Features.PLM.Dashboard.Dtos
{
    public class PlmMonthlySummaryDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthKey { get; set; } = string.Empty;

        public int SampleRequests { get; set; }
        public int SampleRequestFinish { get; set; }
        public decimal SampleRequestFinishRate { get; set; }

        public int ProductionOrders { get; set; }
        public int ProductionOrderFinish { get; set; }
        public decimal ProductionOrderFinishRate { get; set; }

        public int Orders { get; set; }
        public int OrderFinish { get; set; }
        public decimal OrderFinishRate { get; set; }
    }
}
