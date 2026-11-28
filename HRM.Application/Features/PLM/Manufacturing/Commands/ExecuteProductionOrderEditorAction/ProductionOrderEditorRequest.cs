using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;

public sealed class ProductionOrderEditorRequest
{
    [Required, MaxLength(40)] public string Action { get; init; } = string.Empty;
    [Required, StringLength(64, MinimumLength = 64)] public string Revision { get; init; } = string.Empty;
    public decimal? TotalQuantity { get; init; }
    public int? NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public DateTime? ManufacturingDate { get; init; }
    [MaxLength(100)] public string? BagType { get; init; }
    [MaxLength(20000)] public string? LabNote { get; init; }
    [MaxLength(20000)] public string? PlpuNote { get; init; }
    [MaxLength(20000)] public string? Requirement { get; init; }
    [MaxLength(20000)] public string? QcCheck { get; init; }
    [MaxLength(20000)] public string? FormulaNote { get; init; }
    public Guid? SelectedFormulaId { get; init; }
    public Guid? SourceFormulaId { get; init; }
    public ManufacturingFormulaOrigin? Origin { get; init; }
    public IReadOnlyList<ManufacturingFormulaInputItem>? Items { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
    public IReadOnlyList<ManufacturingDeliveryPromisePatch>? DeliveryPromises { get; init; }
}
