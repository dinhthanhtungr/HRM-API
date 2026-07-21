namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailManufacturingFormulaDto
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public decimal? TotalPrice { get; set; }
        public int MaterialCount { get; set; }
        public string MaterialsUrl { get; set; } = string.Empty;
        public bool IsStandard { get; set; }
        public bool IsSelectedInProductionOrder { get; set; }
        public Guid? SourceVUFormulaId { get; set; }
        public string? SourceVUExternalIdSnapshot { get; set; }
        public Guid? SourceManufacturingFormulaId { get; set; }
        public string? SourceManufacturingExternalIdSnapshot { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
