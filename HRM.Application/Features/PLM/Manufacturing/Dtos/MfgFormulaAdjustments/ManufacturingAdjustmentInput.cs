using System.ComponentModel.DataAnnotations;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

/// <summary>Khối lượng QC nhập tính bằng gram; không nhận base/delta/final/snapshot/category từ client.</summary>
public sealed class ManufacturingAdjustmentItemInput
{
    public Guid ItemId { get; init; }
    public ItemType ItemType { get; init; }
    public decimal QuantityGram { get; init; }

    [MaxLength(100)]
    public string? LotNo { get; init; }

    [MaxLength(20000)]
    public string? Note { get; init; }
}

public sealed class ManufacturingAdjustmentRangeInput
{
    /// <summary>Chỉ dùng khi preview PDF, không ghi đè ngày tạo mẻ đã lưu.</summary>
    public DateTime? PreviewCreatedDate { get; init; }
    public int BatchFrom { get; init; }
    public int BatchTo { get; init; }
    /// <summary>Null khi thêm để BE chọn lần kế tiếp; khi sửa giữ lần hiện tại nếu null.</summary>
    public int? TrialNo { get; init; }
    public AdjustmentType AdjustmentType { get; init; } = AdjustmentType.AddQuantity;
    public AdjustmentStatus Status { get; init; } = AdjustmentStatus.Draft;
    public decimal? TakenQuantityGram { get; init; }
    public bool ApplyToNextBatches { get; init; }

    [MaxLength(20000)]
    public string? Note { get; init; }

    [Required]
    public IReadOnlyList<ManufacturingAdjustmentItemInput> Items { get; init; } = [];
}

public sealed class ManufacturingAdjustmentInput
{
    /// <summary>Cộng thay đổi vào lịch sử khi tính hoặc xem trước PDF.</summary>
    public bool AppendToStoredHistory { get; init; }
    /// <summary>Chỉ dùng khi preview PDF, không ghi đè ngày tạo phiếu đã lưu.</summary>
    public DateTime? PreviewCreatedDate { get; init; }
    public Guid? ManufacturingFormulaId { get; init; }
    public AdjustmentStatus? Status { get; init; }

    [MaxLength(20000)]
    public string? Note { get; init; }
    /// <summary>Null giữ batches khi patch; [] thay thế bằng danh sách rỗng.</summary>
    public IReadOnlyList<ManufacturingAdjustmentRangeInput>? Ranges { get; init; }
}
