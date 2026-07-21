using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Common
{
    public sealed class SampleRequestProductionOrderDto
    {
        public Guid MfgProductionOrderId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string FormulaExternalId { get; set; } = string.Empty;
        public SampleRequestSelectedManufacturingFormulaDto? SelectedManufacturingFormula { get; set; }
        public SampleRequestStandardManufacturingFormulaDto? StandardManufacturingFormula { get; set; }
    }

    public sealed class SampleRequestSelectedManufacturingFormulaDto
    {
        public Guid ManufacturingFormulaId { get; set; }
        public DateTime? ValidFrom { get; set; }
        public string FormulaExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsStandard { get; set; }
        public decimal? TotalPrice { get; set; }
        public int MaterialCount { get; set; }
        public string MaterialsUrl { get; set; } = string.Empty;
    }

    public sealed class SampleRequestStandardManufacturingFormulaDto
    {
        public Guid ProductStandardFormulaId { get; set; }
        public Guid ManufacturingFormulaId { get; set; }
        public string FormulaExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public int MaterialCount { get; set; }
        public string MaterialsUrl { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public Guid? PreviousManufacturingFormulaId { get; set; }
        public string? PreviousFormulaExternalId { get; set; }
        public string? PreviousFormulaName { get; set; }
        public decimal? PreviousTotalPrice { get; set; }
        public int? PreviousMaterialCount { get; set; }
        public string? PreviousMaterialsUrl { get; set; }
        public DateTime? PreviousValidFrom { get; set; }
    }

}
