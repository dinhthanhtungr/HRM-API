using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;
using LotNumberOptionDto = HRM.Application.Features.PLM.ProductionOrders.Dtos.ProductionOrderLotNumberDto;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class PatchMfgFormulaMaterial
    {
        public Guid ManufacturingFormulaMaterialId { get; set; }
        public Guid ManufacturingFormulaId { get; set; }
        public Guid ItemId { get; set; }
        public ItemType ItemType { get; set; }
        public Guid CategoryId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public LotNumberOptionDto? LotNumber { get; set; } = null;
        public string? MaterialNameSnapshot { get; set; } // NVARCHAR
        public string? MaterialExternalIdSnapshot { get; set; } // VARCHAR
        public string? Unit { get; set; } // VARCHAR
        public bool? IsActive { get; set; }
    }
}
