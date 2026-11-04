using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderRowDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string POExternalId { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;

        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }
        public DateTime? RequestDeliveryDate { get; set; }
        public DateTime? RealDeliveryDate { get; set; }
        public DateTime? LastReceiptDate { get; set; }

        public string POStatus { get; set; } = string.Empty;
        public string ReportStatus { get; set; } = string.Empty;
        public string AlertFlags { get; set; } = string.Empty;

        public int TotalLines { get; set; }

        public decimal OrderedQuantity { get; set; }
        public decimal WarehouseReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal PendingQcQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }

        public decimal RemainingToAccept { get; set; } // Còn thiếu để QC đạt đủ.
        public decimal CompletionPercent { get; set; } // AcceptedQuantity / OrderedQuantity * 100.
        public decimal TotalPurchaseValue { get; set; }
        public decimal ReceivedPurchaseValue { get; set; }

        public bool IsOverdue { get; set; }
        public int DelayDays { get; set; }

        public bool NeedBuyerAction { get; set; }
        public string BuyerAction { get; set; } = string.Empty;

        public string PLPUComment { get; set; } = string.Empty;
    }
