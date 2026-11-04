using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUSupplierPurchasePerformanceDto
    {
        public Guid? SupplierId { get; set; }            // Id nhà cung cấp.
        public string SupplierName { get; set; } = string.Empty; // Tên nhà cung cấp.

        public int TotalPO { get; set; }                 // Tổng số PO của NCC.
        public int TotalLines { get; set; }              // Tổng số dòng PO của NCC.

        public decimal TotalPurchaseValue { get; set; }  // Tổng giá trị mua từ NCC theo số lượng đặt.
        public decimal ReceivedPurchaseValue { get; set; } // Tổng giá trị từ NCC theo số lượng đã nhập kho.

        public decimal OrderedQuantity { get; set; }           // Tổng số lượng đặt.
        public decimal WarehouseReceivedQuantity { get; set; } // Tổng số lượng nhập kho.
        public decimal AcceptedQuantity { get; set; }          // Tổng số lượng QC đạt.
        public decimal PendingQcQuantity { get; set; }         // Tổng số lượng chờ QC.
        public decimal RejectedQuantity { get; set; }          // Tổng số lượng QC fail.

        public decimal SupplierOTD { get; set; }          // Field cũ, hiểu là SupplierDeliveryOTD.
        public decimal SupplierDeliveryOTD { get; set; }  // Tỷ lệ NCC giao đủ đúng hạn.
        public decimal SupplierAcceptanceOTD { get; set; } // Tỷ lệ hàng QC đạt đủ đúng hạn.

        public decimal SupplierFillRate { get; set; }       // Tỷ lệ NCC giao đủ hàng.
        public decimal SupplierAcceptanceRate { get; set; } // AcceptedQuantity / WarehouseReceivedQuantity * 100.
        public decimal SupplierQcFailRate { get; set; }     // RejectedQuantity / WarehouseReceivedQuantity * 100.

        public decimal AverageDeliveryDelayDays { get; set; }   // Trễ nhập kho trung bình.
        public decimal AverageAcceptanceDelayDays { get; set; } // Trễ QC đạt trung bình.
        public decimal AverageDelayDays { get; set; }           // Trễ trung bình chính.

        public decimal AverageUnitPrice { get; set; }     // TotalPurchaseValue / OrderedQuantity.
        public decimal PriceVarianceAmount { get; set; }  // Tổng ảnh hưởng tiền do lệch giá.

        public decimal RiskScore { get; set; }            // Điểm rủi ro: overdue, QC fail, thiếu, chờ QC, giao vượt.
        public List<PLPUSupplierRiskReasonDto> RiskReasons { get; set; } = new(); // Giải trình điểm rủi ro.
    }
