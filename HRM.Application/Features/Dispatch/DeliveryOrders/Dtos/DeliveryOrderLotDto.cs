namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

using System.Text.Json.Serialization;

public sealed class DeliveryOrderLotDto
{
    public string LotNo { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostSnapshot { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TotalCostSnapshot { get; init; }
}
