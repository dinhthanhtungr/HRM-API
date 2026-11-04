namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

/// <summary>Hàng kèm phiếu, không phân bổ lượng PO và không ghi nhận RealQuantity.</summary>
public sealed record DeliveryAttachedGoodsRequest(Guid ProductId, decimal Quantity, int NumOfBags, string? LotNoList, string? PONo);
