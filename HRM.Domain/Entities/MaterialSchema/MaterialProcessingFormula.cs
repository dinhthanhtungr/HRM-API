using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.MaterialSchema;

public sealed class MaterialProcessingFormula
{
    public Guid MaterialProcessingFormulaId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string ProcessingType { get; set; } = string.Empty;
    public Guid MaterialId { get; set; }
    public int? NumOfBatches { get; set; }
    public decimal TotalQuantityRequest { get; set; }
    public StepOfProduct? StepOfProduct { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public string Status { get; set; } = ManufacturingProductOrder.New.ToString();
    public string? Note { get; set; }
    public string? QcCheck { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public int Revision { get; set; } = 1;
    public Material Material { get; set; } = null!;
    public ICollection<MaterialProcessingFormulaDetail> Details { get; set; } = new List<MaterialProcessingFormulaDetail>();
}
