using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

/// <summary>Client gửi lựa chọn nguồn và dữ liệu nhập; không gửi giá, snapshot hay trạng thái.</summary>
public sealed class CreateProductionOrderFormulaRequest
{
    public Guid SourceFormulaId { get; init; }
    public ManufacturingFormulaOrigin Origin { get; init; }
    public decimal TotalQuantity { get; init; }
    public int NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [JsonRequired]
    public DateTime? ManufacturingDate { get; init; }

    [MaxLength(20000)]
    public string? PlpuNote { get; init; }

    [MaxLength(20000)]
    public string? LabNote { get; init; }

    [MaxLength(20000)]
    public string? Requirement { get; init; }

    [MaxLength(20000)]
    public string? QcCheck { get; init; }
    /// <summary>Null: dùng nguyên nguồn. Danh sách: toàn bộ thành phần người dùng muốn lưu.</summary>
    public IReadOnlyList<ManufacturingFormulaInputItem>? Items { get; init; }
}

public sealed class ManufacturingFormulaInputItem
{
    public Guid? SourceItemId { get; init; }
    public Guid ItemId { get; init; }
    public ItemType ItemType { get; init; }
    public decimal Quantity { get; init; }

    [MaxLength(100)]
    public string? LotNo { get; init; }
}

public sealed record ProductionOrderFormulaCreatedDto(
    Guid ManufacturingFormulaId,
    string ExternalId,
    string ProductionOrderStatus,
    bool IsSelectionEffective
);
