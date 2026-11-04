using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseReceiptReportDto
    {
        public int? WarehouseRequestId { get; set; }     // Id yêu cầu kho.
        public string CodeFromRequest { get; set; } = string.Empty; // Mã yêu cầu kho.

        public long WarehouseVoucherId { get; set; }     // Id phiếu kho.
        public string WarehouseVoucherCode { get; set; } = string.Empty; // Mã phiếu kho.

        public long WarehouseVoucherDetailId { get; set; } // Id dòng phiếu kho.
        public int VoucherType { get; set; }             // Loại phiếu kho.

        public DateTime ReceiptDate { get; set; }        // Ngày nhập kho.

        public Guid PurchaseOrderId { get; set; }        // Id PO được match.
        public Guid? PurchaseOrderDetailId { get; set; } // Id dòng PO được allocate.

        public string POExternalId { get; set; } = string.Empty; // Mã PO.
        public int LineNo { get; set; }                  // Dòng PO được allocate.

        public Guid? SupplierId { get; set; }            // Id nhà cung cấp.
        public string SupplierName { get; set; } = string.Empty; // Tên nhà cung cấp.

        public string ProductCode { get; set; } = string.Empty; // Mã vật tư trên phiếu kho.
        public string ProductName { get; set; } = string.Empty; // Tên vật tư.
        public string LotNumber { get; set; } = string.Empty;   // Số lot.

        public decimal QtyKg { get; set; }               // Số lượng nhập kho.

        public QcDecision? QCResult { get; set; }        // Kết quả QC gốc.
        public DateTime? QCCreatedDate { get; set; }     // Ngày tạo kết quả QC.

        public string QCGroup { get; set; } = string.Empty; // Nhóm QC: Accepted/Rejected/Pending.

        public VoucherDetailType VoucherDetailType { get; set; } // Loại dòng phiếu kho.

        public decimal AcceptedQty { get; set; }         // Số lượng QC đạt.
        public decimal PendingQcQty { get; set; }        // Số lượng chờ QC.
        public decimal RejectedQty { get; set; }         // Số lượng QC fail.
    }
