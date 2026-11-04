using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class PatchMfgFormula
    {
        public string? noteWhyStandardChanged { get; set; } // Lý do thay đổi tiêu chuẩn
        public Guid ManufacturingFormulaId { get; set; }
        public decimal? MfgTotalPrice { get; set; }
        public Guid? mfgProductionOrderId { get; set; }
        public string? Status { get; set; }
        public decimal? TotalPrice { get; set; }
        //public FormulaSource SourceType { get; set; }                     // "FromVA" hoặc "FromVU"
        public bool IsSelect { get; set; }
        public bool? IsStandard { get; set; }
        public string? Note { get; set; }
        public virtual ICollection<PatchMfgFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<PatchMfgFormulaMaterial>();
    }
}
