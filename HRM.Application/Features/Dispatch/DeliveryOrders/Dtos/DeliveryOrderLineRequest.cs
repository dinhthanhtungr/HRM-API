namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderLineRequest
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public decimal Quantity { get; init; }
    public int NumOfBags { get; init; }

    /// <summary>
    /// Dữ liệu tương thích FE cũ, chỉ được dùng khi Lots không được gửi.
    /// Backend luôn tự sinh lại giá trị lưu trữ từ danh sách lot đã chuẩn hóa.
    /// </summary>
    public string? LotNoList { get; init; }

    public IReadOnlyCollection<DeliveryOrderLotRequest>? Lots { get; init; }
}
