namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderLotRequest
{
    /// <summary>FinishedGood hoặc DefectiveFinishedGood; null/absent mặc định FinishedGood để tương thích request cũ.</summary>
    public string? StockType { get; init; }
    public string? LotNo { get; init; }
    public int? NumOfBags { get; init; }
    public decimal Quantity { get; init; }
}
