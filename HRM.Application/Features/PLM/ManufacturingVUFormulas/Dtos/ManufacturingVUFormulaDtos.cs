using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;

public sealed class CreateManufacturingVUFormulaRequest
{
    public Guid FormulaId { get; set; }
    public decimal TotalProductionQuantity { get; set; }
    public int NumOfBatches { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? QcCheck { get; set; }
}

public sealed class PatchManufacturingVUFormulaRequest
{
    public decimal? TotalProductionQuantity { get; set; }
    public int? NumOfBatches { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? QcCheck { get; set; }
    public ManufacturingProductOrder? Status { get; set; }
}

public sealed class ManufacturingVUFormulaWriteResultDto
{
    public Guid ManufacturingVUFormulaId { get; set; }
    public ManufacturingProductOrder Status { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class ManufacturingVUFormulaListItemDto
{
    public Guid ManufacturingVUFormulaId { get; set; }
    public Guid FormulaId { get; set; }
    public string FormulaExternalId { get; set; } = string.Empty;
    public string FormulaName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
    public decimal? TotalProductionQuantity { get; set; }
    public int? NumOfBatches { get; set; }
    public ManufacturingProductOrder Status { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
}

public sealed class ManufacturingVUFormulaDetailDto
{
    public Guid ManufacturingVUFormulaId { get; set; }
    public Guid FormulaId { get; set; }
    public string FormulaExternalId { get; set; } = string.Empty;
    public string FormulaName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public double? UsageRate { get; set; }
    public decimal? TotalProductionQuantity { get; set; }
    public int? NumOfBatches { get; set; }
    public ManufacturingProductOrder Status { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? QcCheck { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string? UpdatedByName { get; set; }
    public IReadOnlyList<ManufacturingVUFormulaMaterialDto> Materials { get; set; } = [];
}

public sealed class ManufacturingVUFormulaMaterialDto
{
    public Guid FormulaMaterialSnapshotId { get; set; }
    public int LineNo { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public ItemType ItemType { get; set; }
    public string? MaterialCode { get; set; }
    public string? MaterialName { get; set; }
    public string? Unit { get; set; }
    public string? LotNo { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
}

public sealed class ManufacturingVUFormulaPdfFileDto
{
    public string FileName { get; set; } = "lenh-san-xuat-mau.pdf";
    public string ContentType { get; set; } = "application/pdf";
    public byte[] Content { get; set; } = [];
}

public sealed class ManufacturingVUFormulaPdfDocumentDto
{
    public Guid ProductId { get; set; }
    public string FormulaExternalId { get; set; } = string.Empty;
    public string FormulaName { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
    public string? CustomerCode { get; set; }
    public DateTime? RequestDate { get; set; }
    public double? UsageRate { get; set; }
    public decimal TotalProductionQuantity { get; set; }
    public int NumOfBatches { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? QcCheck { get; set; }
    public IReadOnlyList<ManufacturingVUFormulaPdfMaterialDto> Materials { get; set; } = [];
}

public sealed class ManufacturingVUFormulaPdfMaterialDto
{
    public int LineNo { get; set; }
    public string? CategoryName { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? LotNo { get; set; }
    public decimal Quantity { get; set; }
}
