namespace HRM.Application.Features.PLM.Dashboard.Dtos
{
    public class PlmDashboardSummaryDto
    {
        public int TotalSampleRequests { get; set; }
        public int TotalSampleRequestFinish { get; set; }
        public int TotalProductionOrders { get; set; }
        public int TotalProductionOrderFinish { get; set; }
        public int TotalOrders { get; set; }
        public int TotalOrderFinish { get; set; }
        public decimal SampleRequestFinishRate { get; set; }
        public decimal ProductionOrderFinishRate { get; set; }
        public decimal OrderFinishRate { get; set; }
    }
}
