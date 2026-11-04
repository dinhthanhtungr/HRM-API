namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

using System.Text.Json.Serialization;

public sealed class AvailableDeliveryLotDto
{
    /// <summary>Kg/bao từ yêu cầu mẫu active mới nhất của product/khách trên PO; null nếu không có giá trị hợp lệ.</summary>
    public int? BagWeightKg { get; init; }
    public string LotNo { get; init; } = string.Empty;
    /// <summary>Mã DHG active liên kết MFG đang chọn formula của lot, cùng product/company; không có thì rỗng.</summary>
    public IReadOnlyList<string> OrderExternalIds { get; init; } = Array.Empty<string>();
    public string StockType { get; init; } = "FinishedGood";
    public bool CanSelect { get; init; } = true;
    public decimal OnHandQuantity { get; init; }
    public decimal ReservedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostSnapshot { get; init; }
}
