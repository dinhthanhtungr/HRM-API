using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class PostMfgFormula
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string status { get; set; } = string.Empty;
        public Guid? mfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public string? Name { get; set; }
        // các field từ MfgProductionOrder bạn muốn hiển thị
        public Guid ProductId { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
        public bool IsRecycle { get; set; } // Lấy từ Product.IsRecycle
        public string? CustomerNameSnapshot { get; set; }
        public decimal? SaleTotalPrice { get; set; } // từ MerchandiseOrder.TotalPrice qua MfgProductionOrder giá sale lên
        public decimal TotalQuantityRequest { get; set; }
        public Guid? VUFormulaId { get; set; } // ID công thức từ VU nếu có sales chọn trên đơn hàng
        public string? FormulaExternalIdSnapshot { get; set; }
        public FormulaSource? SourceType { get; set; } // "FromVA" hoặc "FromVU"
        public Guid FormulaSourceId { get; set; } // ID của công thức gốc (có thể là ManufacturingFormulaId hoặc VUFormulaId tùy SourceType)
        public string FormulaSourceNameSnapshot { get; set; } = string.Empty; // Tên công thức gốc
        public bool IsSelect { get; set; }
        public bool IsStandard { get; set; }
        public string? Note { get; set; }
        public virtual ICollection<PostManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<PostManufacturingFormulaMaterial>();
    }
}
