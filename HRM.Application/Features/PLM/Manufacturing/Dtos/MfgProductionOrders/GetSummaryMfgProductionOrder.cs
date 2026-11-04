using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders
{
    public class GetSummaryMfgProductionOrder
    {
        public Guid MfgProductionOrderId { get; set; }
        public string? ExternalId { get; set; }
        public string? MfgFormualaExternalIdSnapshot { get; set; }
        public Guid MerchandiseOrderId { get; set; }
        public string? MerchandiseOrderExternalId { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerExternalIdSnapshot { get; set; }
        public decimal RequestedQuantity { get; set; } // Khối lượng yêu cầu
        public decimal? TotalQuantity { get; set; } // Khối lượng sản xuất
        public string? Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? BagType { get; set; }
        // ======================================================================== CheckName ========================================================================
        public bool? IsPrintedStock { get; set; } = false; // Đã lưu trữ công thức
        public string? CheckName { get; set; } // Người kiểm tra công thức
        public DateTime? CheckedDate { get; set; } // Ngày kiểm tra công thức
        // ======================================================================== History ========================================================================
        public List<GetMFGFormulaHistories> MFGFormulaHistories { get; set; } = new List<GetMFGFormulaHistories>();
    }
}
