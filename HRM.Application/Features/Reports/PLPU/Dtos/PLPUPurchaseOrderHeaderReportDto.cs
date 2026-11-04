using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderHeaderReportDto
    {
        public Guid PurchaseOrderId { get; set; }       // Id PO.
        public string POExternalId { get; set; } = string.Empty; // Mã PO.
        public string OrderType { get; set; } = string.Empty;    // Loại đơn mua.

        public Guid? SupplierId { get; set; }           // Id nhà cung cấp.
        public string SupplierName { get; set; } = string.Empty; // Tên nhà cung cấp.

        public Guid? CompanyId { get; set; }            // Id công ty.
        public DateTime? CreatedDate { get; set; }      // Ngày tạo PO.
        public Guid? CreatedBy { get; set; }            // Người tạo PO.

        public DateTime? RequestDeliveryDate { get; set; } // Ngày yêu cầu giao ở header PO.
        public DateTime? RealDeliveryDate { get; set; }    // Ngày giao thực tế nếu PO có lưu.
        public DateTime? LastReceiptDate { get; set; }     // Ngày nhập kho cuối cùng theo ledger.

        public string POStatus { get; set; } = string.Empty;     // Trạng thái gốc của PO.
        public string ReportStatus { get; set; } = string.Empty; // Trạng thái báo cáo do service tự tính.
        public string AlertFlags { get; set; } = string.Empty;   // Cảnh báo: Overdue, Shortage, WaitingQC...

        public int TotalLines { get; set; }              // Số dòng vật tư trong PO.

        public decimal OrderedQuantity { get; set; }             // Tổng số lượng đặt.
        public decimal WarehouseReceivedQuantity { get; set; }   // Tổng số lượng đã nhập kho.
        public decimal AcceptedQuantity { get; set; }            // Tổng số lượng QC đạt.
        public decimal PendingQcQuantity { get; set; }           // Tổng số lượng chờ QC.
        public decimal RejectedQuantity { get; set; }            // Tổng số lượng QC fail.

        public decimal RemainingToReceive { get; set; }  // Còn thiếu để nhập kho đủ.
        public decimal RemainingToAccept { get; set; }   // Còn thiếu để QC đạt đủ: OrderedQuantity - AcceptedQuantity.
        public decimal OverReceivedQuantity { get; set; } // Tổng số lượng giao vượt.

        public decimal CompletionPercent { get; set; }       // AcceptedQuantity / OrderedQuantity * 100.
        public decimal WarehouseReceiptPercent { get; set; } // Tỷ lệ nhập kho.

        public decimal TotalPurchaseValue { get; set; }  // Tổng giá trị PO theo số lượng đặt.
        public decimal ReceivedPurchaseValue { get; set; } // Tổng giá trị PO theo số lượng đã nhập kho.

        public bool IsOverdue { get; set; }              // PO trễ nhập kho theo ngày yêu cầu giao.
        public int DelayDays { get; set; }               // Số ngày trễ nhập kho lớn nhất của PO.
        public int DeliveryDelayDays { get; set; }       // Số ngày trễ nhập kho.
        public int AcceptanceDelayDays { get; set; }     // Số ngày trễ QC đạt.

        public bool HasQCFail { get; set; }              // Có hàng QC fail.
        public bool HasPendingQC { get; set; }           // Có hàng chờ QC.
        public bool HasShortage { get; set; }            // Có thiếu hàng.
        public bool HasOpenBalance { get; set; }         // Chưa QC đạt đủ.
        public bool HasOverDelivery { get; set; }        // Có giao vượt.

        public bool NeedBuyerAction { get; set; }        // Buyer cần xử lý hay không.
        public string BuyerAction { get; set; } = string.Empty; // Gợi ý việc buyer cần làm.

        public string PLPUComment { get; set; } = string.Empty; // Ghi chú PLPU.
        public string Comment { get; set; } = string.Empty;     // Ghi chú PO.
    }
