using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

/// <summary>Form đầy đủ: nullable date gửi null để xóa; không nhận status/giá/snapshot.</summary>
public sealed class ProductionOrderInformRequest
{
    public Guid? SelectedFormulaId { get; init; }
    public decimal TotalQuantity { get; init; }
    public int NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [JsonRequired]
    public DateTime? ManufacturingDate { get; init; }

    [MaxLength(100)]
    public string? BagType { get; init; }

    [MaxLength(20000)]
    public string? PlpuNote { get; init; }

    [MaxLength(20000)]
    public string? LabNote { get; init; }

    [MaxLength(20000)]
    public string? Requirement { get; init; }

    [MaxLength(20000)]
    public string? QcCheck { get; init; }
    public IReadOnlyList<ManufacturingFormulaInputItem>? Items { get; init; }
}
