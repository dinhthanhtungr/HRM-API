using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseQcReportDto
    {
        public Guid? QCInputByQCId { get; set; }

        public long VoucherDetailId { get; set; }

        public Guid PurchaseOrderId { get; set; }
        public Guid? PurchaseOrderDetailId { get; set; }

        public string POExternalId { get; set; } = string.Empty;
        public int LineNo { get; set; }

        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;

        public decimal QtyKg { get; set; }

        public QcDecision? QCResult { get; set; }
        public string QCGroup { get; set; } = string.Empty;

        public DateTime ReceiptDate { get; set; }
        public DateTime? QCCreatedDate { get; set; }

        public int QCAgeDays { get; set; }

        public string BuyerAction { get; set; } = string.Empty;
    }
