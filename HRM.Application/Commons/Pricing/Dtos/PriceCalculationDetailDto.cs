using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Dtos;

/// <summary>
/// Phân rã server-side của giá realtime để FE chỉ hiển thị, không tự tính lại giá vốn.
/// </summary>
public sealed class PriceCalculationDetailDto
{
    public string RuleCode { get; init; } = "DIRECT_PRICE";
    public string DisplayText { get; init; } = string.Empty;
    public Guid? BaseItemId { get; init; }
    public string? BaseItemName { get; init; }
    public decimal? BaseUnitPrice { get; init; }
    public LatestPriceSourceType? BasePriceSource { get; init; }
    public decimal? FormulaMaterialCost { get; init; }
    public decimal? FixedCostPerKg { get; init; }
    public decimal? Rate { get; init; }
    public decimal CalculatedUnitPrice { get; init; }
    public bool IsComplete { get; init; }
}
