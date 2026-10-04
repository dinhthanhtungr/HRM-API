using System.Text.Json.Serialization;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>
/// So sánh giá chuẩn Approved với giá chuẩn tham chiếu đã điều chỉnh
/// theo biến động chi phí NVL realtime của đúng nguồn Formula/VA đã duyệt.
/// DTO này chỉ là read-model và không thay đổi giá chuẩn hoặc snapshot báo giá.
/// </summary>
public sealed class StandardPriceRealtimeComparisonDto
{
    public string Currency { get; init; } = string.Empty;
    public decimal ApprovedStandardPrice { get; init; }
    public decimal? RealtimeAdjustedStandardPrice { get; init; }
    public decimal? StandardPriceDifference { get; init; }
    public decimal? StandardPriceDifferencePercent { get; init; }

    /// <summary>
    /// Công thức display-only giải thích giá bán realtime. Field chỉ được trả khi user có quyền xem
    /// chi phí NVL, chi phí sản xuất và margin vì công thức chứa các giá trị nhạy cảm đó.
    /// </summary>
    public string? RealtimeAdjustedPriceFormula { get; init; }

    /// <summary>
    /// Chi phí NVL snapshot lúc duyệt. Field bị che khi current user không có quyền xem material cost.
    /// </summary>
    public decimal? ApprovedMaterialCostSnapshot { get; init; }

    /// <summary>
    /// Chi phí NVL realtime. Field bị che khi current user không có quyền xem material cost.
    /// </summary>
    public decimal? RealtimeMaterialCost { get; init; }

    /// <summary>
    /// Chênh lệch chi phí NVL tuyệt đối. Field bị che khi current user không có quyền xem material cost.
    /// </summary>
    public decimal? MaterialCostDifference { get; init; }

    public decimal? MaterialCostDifferencePercent { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialCostMovementStatus MovementStatus { get; init; }

    public bool IsMaterialCostComplete { get; init; }
    public bool IsIncreaseWarning { get; init; }
    public decimal WarningThresholdPercent { get; init; }
    public DateTime CalculatedAt { get; init; }
}

public enum MaterialCostMovementStatus
{
    Unknown = 0,
    Unchanged = 10,
    Increased = 20,
    Decreased = 30
}
