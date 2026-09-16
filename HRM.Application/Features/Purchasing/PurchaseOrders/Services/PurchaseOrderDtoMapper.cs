using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderDtoMapper
{
    public static PurchaseOrderDetailDto Map(
        PurchaseOrder purchaseOrder,
        string? supplierName,
        PurchaseOrderSnapshot? snapshot,
        IReadOnlyList<PurchaseOrderLineDto> items,
        string merchandiseOrderCodes = "") => new()
    {
        PurchaseOrderId = purchaseOrder.PurchaseOrderId,
        ExternalId = purchaseOrder.ExternalId ?? string.Empty,
        Status = purchaseOrder.Status ?? string.Empty,
        SupplierId = purchaseOrder.SupplierId,
        SupplierName = supplierName ?? string.Empty,
        RequestDeliveryDate = purchaseOrder.RequestDeliveryDate,
        RealDeliveryDate = purchaseOrder.RealDeliveryDate,
        TotalPrice = snapshot?.TotalPrice ?? items.Sum(x => x.TotalPriceAgreed),
        RealTotalPrice = items.Sum(x => (x.RealQuantity ?? 0m) * x.UnitPriceAgreed),
        CreateDate = purchaseOrder.CreateDate,
        MerchandiseOrderCodes = merchandiseOrderCodes,
        OrderType = purchaseOrder.OrderType,
        Comment = purchaseOrder.Comment,
        PlpuComment = purchaseOrder.PLPUComment,
        DeliveryAddress = snapshot?.DeliveryAddress,
        PaymentTypes = snapshot?.PaymentTypes,
        Vat = snapshot?.Vat,
        Items = items
    };

    public static PurchaseOrderLineDto MapLine(PurchaseOrderDetail detail) => new()
    {
        PurchaseOrderDetailId = detail.PurchaseOrderDetailId,
        LineNo = detail.LineNo,
        MaterialId = detail.MaterialId,
        MaterialCode = detail.MaterialExternalIDSnapshot ?? string.Empty,
        MaterialName = detail.MaterialNameSnapshot ?? string.Empty,
        Quantity = detail.RequestQuantity ?? 0m,
        RealQuantity = detail.RealQuantity,
        Package = detail.Package,
        UnitPriceAgreed = detail.UnitPriceAgreed ?? 0m,
        TotalPriceAgreed = detail.TotalPriceAgreed ?? 0m,
        BaseCostSnapshot = detail.BaseCostSnapshot,
        BaseDateSnapshot = detail.BaseDateSnapshot,
        DeliveryDate = detail.DeliveryDate,
        Note = detail.Note
    };
}
