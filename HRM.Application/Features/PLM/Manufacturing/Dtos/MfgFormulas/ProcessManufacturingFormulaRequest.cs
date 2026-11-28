using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;

public sealed class ProcessManufacturingFormulaRequest
{
    public bool? IsStandard { get; init; }

    [MaxLength(20000)]
    public string? StandardChangeReason { get; init; }

    [MaxLength(20000)]
    public string? Note { get; init; }
    public bool ClearNote { get; init; }
    public IReadOnlyList<ManufacturingFormulaInputItem>? Items { get; init; }
}

public sealed record ProcessManufacturingFormulaResult(
    Guid ManufacturingFormulaId,
    string FormulaStatus,
    string ProductionOrderStatus,
    bool SelectionEffective
);
