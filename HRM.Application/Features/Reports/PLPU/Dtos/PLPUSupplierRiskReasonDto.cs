using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUSupplierRiskReasonDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string POExternalId { get; set; } = string.Empty;
        public Guid? PurchaseOrderDetailId { get; set; }
        public int? LineNo { get; set; }

        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;

        public string ReasonCode { get; set; } = string.Empty;
        public string ReasonText { get; set; } = string.Empty;
        public decimal ImpactScore { get; set; }

        public decimal OrderedQuantity { get; set; }
        public decimal WarehouseReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal PendingQcQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public int DelayDays { get; set; }
        public DateTime? RequestDeliveryDate { get; set; }
        public DateTime? ActualDate { get; set; }
    }
