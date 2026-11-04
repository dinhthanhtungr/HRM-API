using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOverviewSummaryDto
    {
        /// <summary>All PO records in the existing accounting view; NVL metrics above remain independent.</summary>
        public PLPUAccountingSummaryDto? Accounting { get; set; }
        public int TotalPO { get; set; }              // Tổng số PO.
        public int ActivePO { get; set; }             // PO chưa bị hủy.
        public int CancelledPO { get; set; }          // PO đã hủy.
        public int ClosedPO { get; set; }             // PO đã đóng.
        public int CompletedPO { get; set; }          // PO đã hoàn thành, QC đạt đủ.

        public int NeedActionPO { get; set; } // Số PO cần buyer xử lý.
                                              // Được tính bằng:
                                              // headers.Count(x => x.NeedBuyerAction)
                                              //
                                              // Một PO được xem là cần xử lý nếu:
                                              // - Có quá hạn (Overdue)
                                              // - Có thiếu hàng (Shortage)
                                              // - Có hàng QC fail (HasQCFail)
                                              // - Có hàng chờ QC (WaitingQC)
                                              // - Có giao vượt (OverDelivered)
                                              // - Hoặc trạng thái = ClosedWithShortage
                                              //
                                              // Nhưng sẽ KHÔNG tính nếu PO:
                                              // - Đã hủy (Cancelled)
                                              // - Đã đóng hoàn toàn (Closed)
                                              // - Đã hoàn thành (Completed)

        public int OverduePO { get; set; }            // Số PO quá hạn.
        public int QCFailPO { get; set; }             // Số PO có hàng QC fail.
        public int PendingQCPO { get; set; }          // Số PO có hàng đang chờ QC.
        public int OverDeliveryPO { get; set; }       // Số PO có giao vượt.

        public decimal TotalPurchaseValue { get; set; } // Tổng giá trị mua hàng theo số lượng đặt.
        public decimal ReceivedPurchaseValue { get; set; } // Tổng giá trị theo số lượng đã nhập kho.

        public decimal OrderedQuantity { get; set; }            // Tổng số lượng đặt.
        public decimal WarehouseReceivedQuantity { get; set; }  // Tổng số lượng kho đã nhập.
        public decimal AcceptedQuantity { get; set; }           // Tổng số lượng QC đạt.
        public decimal PendingQcQuantity { get; set; }          // Tổng số lượng chờ QC.
        public decimal RejectedQuantity { get; set; }           // Tổng số lượng QC fail.

        public decimal RemainingToReceive { get; set; } // Còn thiếu để nhập kho đủ, có tolerance 2%.
        public decimal RemainingToAccept { get; set; }  // Còn thiếu để QC đạt đủ, có tolerance 2%.
        public decimal OverReceivedQuantity { get; set; } // Số lượng giao vượt quá mức cho phép.

        public decimal CompletionPercent { get; set; }       // AcceptedQuantity / OrderedQuantity * 100.
        public decimal WarehouseReceiptPercent { get; set; } // WarehouseReceivedQuantity / OrderedQuantity * 100.

        public decimal OnTimeDeliveryRate { get; set; }    // Tỷ lệ dòng nhập kho đủ đúng hạn.
        public decimal OnTimeAcceptanceRate { get; set; }  // Tỷ lệ dòng QC đạt đủ đúng hạn.

        public decimal ShortageRate { get; set; }       // Tỷ lệ thiếu hàng.
        public decimal OverDeliveryRate { get; set; }   // Tỷ lệ giao vượt.
        public decimal QcFailRate { get; set; }         // RejectedQuantity / WarehouseReceivedQuantity * 100.
        public decimal QcAcceptanceRate { get; set; }   // AcceptedQuantity / WarehouseReceivedQuantity * 100.
        public decimal PendingQcRate { get; set; }      // PendingQcQuantity / WarehouseReceivedQuantity * 100.

        public decimal AverageDeliveryDelayDays { get; set; }    // Số ngày trễ nhập kho trung bình.
        public decimal AverageAcceptanceDelayDays { get; set; }  // Số ngày trễ QC đạt trung bình.
        public decimal AverageDelayDays { get; set; }            // Số ngày trễ chính, hiện lấy theo QC đạt.

        public int MaxDeliveryDelayDays { get; set; }    // Số ngày trễ nhập kho lớn nhất.
        public int MaxAcceptanceDelayDays { get; set; }  // Số ngày trễ QC đạt lớn nhất.
        public int MaxDelayDays { get; set; }            // Số ngày trễ lớn nhất.
    }
