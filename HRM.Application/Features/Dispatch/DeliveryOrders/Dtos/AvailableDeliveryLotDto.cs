namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

using System.Text.Json.Serialization;

public sealed class AvailableDeliveryLotDto
{
    public string LotNo { get; init; } = string.Empty;
    public decimal OnHandQuantity { get; init; }
    public decimal ReservedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostSnapshot { get; init; }
}
