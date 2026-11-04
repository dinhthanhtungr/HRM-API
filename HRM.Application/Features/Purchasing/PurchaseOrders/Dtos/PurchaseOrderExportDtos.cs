using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

public sealed class PurchaseOrderExportLine
{
    public Guid PurchaseOrderId { get; set; }
    public string POExternalId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? HeaderRequestDeliveryDate { get; set; }
    public string? SupplierExternalId { get; set; }
    public string? SupplierName { get; set; }
    public Guid PurchaseOrderDetailId { get; set; }
    public int LineNo { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal? UnitPriceAgreed { get; set; }
    public decimal? TotalPriceAgreed { get; set; }
    public decimal? BaseCostSnapshot { get; set; }
    public DateTime? DeliveryDate { get; set; }
}

public sealed class PurchaseOrderReceiptExportRow
{
    public string POExternalId { get; set; } = string.Empty;
    public Guid? PurchaseOrderDetailId { get; set; }
    public long VoucherDetailId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public QcDecision? QCResult { get; set; }
    public VoucherDetailType VoucherDetailType { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal PendingQcQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
}

public sealed class PurchaseOrderExcelRow
{
    public DateTime? OrderDate { get; set; }
    public string PurchaseOrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public DateTime? ActualDeliveryDate { get; set; }
    public DateTime? RequestDeliveryDate { get; set; }
    public int? LateDays { get; set; }
    public string DeliveryResult { get; set; } = string.Empty;
    public string? SupplierExternalId { get; set; }
    public string? SupplierName { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal PendingQcQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RealTotalAmount { get; set; }
}
