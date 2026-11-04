using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;

public sealed class CreateLabManufacturingFormulaRequest
{
    public Guid SourceFormulaId { get; init; }
    public FormulaSource SourceType { get; init; }
    public bool IsStandard { get; init; }

    [MaxLength(2000)]
    public string? Note { get; init; }

    [Required]
    public IReadOnlyList<ManufacturingFormulaInputItem> Items { get; init; } = [];
}
