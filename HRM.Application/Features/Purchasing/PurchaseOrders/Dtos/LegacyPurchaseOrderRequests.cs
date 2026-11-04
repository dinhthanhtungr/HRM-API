namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
/// <summary>Adapter cho tên field form cũ; company/audit/mã/cost snapshot vẫn do server xác định.</summary>
public sealed class LegacyCreatePurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    public string? OrderType { get; init; }
    public DateTime? RequestDeliveryDate { get; init; }
    public DateTime? RealDeliveryDate { get; init; }
    public string? Comment { get; init; }
    public string? PLPUComment { get; init; }
    public IReadOnlyList<Guid> MerchadiseOrderIds { get; init; } = [];
    public IReadOnlyList<LegacyPurchaseOrderLineRequest> PurchaseOrderDetails { get; init; } = [];
    public LegacyPurchaseOrderSnapshotRequest? PurchaseOrderSnapshot { get; init; }
    public CreatePurchaseOrderRequest ToRequest() => new()
    {
        SupplierId = SupplierId,
        OrderType = OrderType,
        RequestDeliveryDate = RequestDeliveryDate,
        RealDeliveryDate = RealDeliveryDate,
        Comment = Comment,
        PlpuComment = PLPUComment,
        MerchandiseOrderIds = MerchadiseOrderIds,
        DeliveryAddress = PurchaseOrderSnapshot?.DeliveryAddress,
        PaymentTypes = PurchaseOrderSnapshot?.PaymentTypes,
        Vat = PurchaseOrderSnapshot?.Vat,
        Items = PurchaseOrderDetails.Select(x => new PurchaseOrderLineRequest
        {
            MaterialId = x.MaterialId,
            Quantity = x.RequestQuantity ?? 0m,
            UnitPriceAgreed = x.UnitPriceAgreed ?? -1m,
            Package = x.Package,
            DeliveryDate = x.DeliveryDate,
            Note = x.Note
        }).ToArray()
    };
}
public sealed class LegacyPurchaseOrderLineRequest
{
    public Guid MaterialId { get; init; }
    public decimal? RequestQuantity { get; init; }
    public decimal? UnitPriceAgreed { get; init; }
    public string? Package { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Note { get; init; }
}
public sealed class LegacyPurchaseOrderSnapshotRequest
{
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
}
public sealed class LegacyUpdatePurchaseOrderRequest
{
    public Guid PurchaseOrderId { get; init; }
    public string? Comment { get; init; }
    public string? PLPUComment { get; init; }
    public bool? IsActive { get; init; }
    public IReadOnlyCollection<string> ClearFields { get; init; } = [];
}
