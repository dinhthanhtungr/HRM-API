namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailQuickSummaryDto
    {
        public Guid ProductId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid BranchId { get; set; }
        public Guid ManagerBy { get; set; }
        public Guid? SelectedDevelopmentFormulaId { get; set; }
        public Guid AttachmentCollectionId { get; set; }
        public Guid CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public double? ExpectedQuantity { get; set; }
        public double? SampleQuantity { get; set; }
        public decimal? ExpectedPrice { get; set; }
        public double? Weight { get; set; }
        public int DevelopmentFormulaCount { get; set; }
        public int ManufacturingFormulaCount { get; set; }
        public int ProductionOrderCount { get; set; }
        public int AttachmentCount { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
