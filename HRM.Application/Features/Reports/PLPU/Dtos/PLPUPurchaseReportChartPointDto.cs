using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseReportChartPointDto
    {
        public string Label { get; set; } = string.Empty;
        public Guid? Id { get; set; }

        public int PurchaseOrderCount { get; set; }
        public int LineCount { get; set; }

        public decimal OrderedQuantity { get; set; }
        public decimal WarehouseReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal PendingQcQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }

        public decimal TotalPurchaseValue { get; set; }
        public decimal ReceivedPurchaseValue { get; set; }
        public decimal CompletionPercent { get; set; }
        public decimal WarehouseReceiptPercent { get; set; }

        public decimal QcFailRate { get; set; }
        public decimal QcAcceptanceRate { get; set; }

        public decimal AverageDelayDays { get; set; }
        public decimal AverageDeliveryDelayDays { get; set; }
        public decimal AverageAcceptanceDelayDays { get; set; }

        public decimal PriceVarianceAmount { get; set; }

        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
    }
