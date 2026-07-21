namespace HRM.Application.Features.PLM.Dashboard.Dtos
{
    public class PlmTaskBreakdownDto
    {
        public string TaskType { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public int ProductCount { get; set; }
        public decimal ExpectedQuantity { get; set; }
        public decimal SampleQuantity { get; set; }
        public int FormulaAssignedCount { get; set; }
    }
}
