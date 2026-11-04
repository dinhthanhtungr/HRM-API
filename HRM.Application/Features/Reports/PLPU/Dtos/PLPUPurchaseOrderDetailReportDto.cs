using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderDetailReportDto
    {
        public Guid PurchaseOrderDetailId { get; set; } // Id dòng PO.
        public Guid PurchaseOrderId { get; set; }       // Id PO cha.

        public string POExternalId { get; set; } = string.Empty; // Mã PO.
        public int LineNo { get; set; }                 // Số thứ tự dòng.

        public Guid MaterialId { get; set; }            // Id vật tư.
        public string MaterialCode { get; set; } = string.Empty; // Mã vật tư.
        public string MaterialName { get; set; } = string.Empty; // Tên vật tư.
        public string Package { get; set; } = string.Empty;      // Quy cách/bao bì.

        public decimal OrderedQuantity { get; set; }            // Số lượng đặt.
        public decimal WarehouseReceivedQuantity { get; set; }  // Số lượng nhập kho.
        public decimal AcceptedQuantity { get; set; }           // Số lượng QC đạt.
        public decimal PendingQcQuantity { get; set; }          // Số lượng chờ QC.
        public decimal RejectedQuantity { get; set; }           // Số lượng QC fail.

        public decimal RemainingToReceive { get; set; } // Còn thiếu để nhập kho đủ.
        public decimal RemainingToAccept { get; set; }  // Còn thiếu để QC đạt đủ.
        public decimal OverReceivedQuantity { get; set; } // Số lượng giao vượt.

        public DateTime? DeliveryDate { get; set; }        // Ngày giao yêu cầu ở dòng PO.
        public DateTime? RequestDeliveryDate { get; set; } // Ngày dùng để tính hạn giao.

        public DateTime? FirstReceiptDate { get; set; }    // Ngày nhập kho đầu tiên.
        public DateTime? LastReceiptDate { get; set; }     // Ngày nhập kho cuối cùng.

        public DateTime? DateWhenReceivedEnough { get; set; } // Ngày nhập kho cộng dồn đủ.
        public DateTime? DateWhenAcceptedEnough { get; set; } // Ngày QC đạt cộng dồn đủ.

        public int DelayDays { get; set; }              // Số ngày trễ chính, hiện lấy theo QC đạt.
        public int DeliveryDelayDays { get; set; }      // Số ngày trễ nhập kho.
        public int AcceptanceDelayDays { get; set; }    // Số ngày trễ QC đạt.

        public string LineReportStatus { get; set; } = string.Empty; // Trạng thái báo cáo của dòng.
        public string AlertFlags { get; set; } = string.Empty;       // Các cảnh báo của dòng.

        public bool NeedBuyerAction { get; set; }        // Dòng này cần buyer xử lý không.
        public string BuyerAction { get; set; } = string.Empty; // Gợi ý hành động buyer.

        public decimal? BaseCostSnapshot { get; set; }   // Giá gốc tại thời điểm tạo PO.
        public DateTime? BaseDateSnapshot { get; set; }  // Ngày giá gốc.

        public decimal? UnitPriceAgreed { get; set; }    // Đơn giá đã chốt.
        public decimal? TotalPriceAgreed { get; set; }   // Thành tiền đã chốt.
        public decimal ReceivedPurchaseValue { get; set; } // Thành tiền theo số lượng đã nhập kho.

        public decimal? PriceVariance { get; set; }        // Chênh lệch đơn giá.
        public decimal? PriceVariancePercent { get; set; } // % chênh lệch giá.
        public decimal? PriceImpactAmount { get; set; }    // Ảnh hưởng tiền do lệch giá.

        public string Note { get; set; } = string.Empty; // Ghi chú dòng PO.
    }
