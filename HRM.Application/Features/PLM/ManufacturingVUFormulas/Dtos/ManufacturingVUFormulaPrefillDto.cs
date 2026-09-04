using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;

/// <summary>
/// Dữ liệu hiện hành dùng để điền trước form tạo lệnh sản xuất mẫu từ Formula.
/// </summary>
public sealed class ManufacturingVUFormulaPrefillDto
{
    public Guid FormulaId { get; set; }
    public string FormulaExternalId { get; set; } = string.Empty;
    public string FormulaName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
    public double? UsageRate { get; set; }
    public Guid? SampleRequestId { get; set; }
    public string? SampleRequestExternalId { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public IReadOnlyList<ManufacturingVUFormulaPrefillMaterialDto> Materials { get; set; } = [];
}

public sealed class ManufacturingVUFormulaPrefillMaterialDto
{
    public Guid FormulaMaterialId { get; set; }
    public int LineNo { get; set; }
    public Guid ItemId { get; set; }
    public ItemType ItemType { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? MaterialCode { get; set; }
    public string? MaterialName { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
}
