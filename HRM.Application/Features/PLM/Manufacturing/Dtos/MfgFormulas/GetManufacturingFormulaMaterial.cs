using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Formulas;
using LotNumberOptionDto = HRM.Application.Features.PLM.ProductionOrders.Dtos.ProductionOrderLotNumberDto;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class GetManufacturingFormulaMaterial
    {
        public Guid ManufacturingFormulaMaterialId { get; set; }
        public Guid ManufacturingFormulaId { get; set; }
        public Guid ItemId { get; set; }
        public ItemType itemType { get; set; }
        public Guid CategoryId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime? ExpiryDate { get; set; } // DATETIME
        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
        public List<LotNumberOptionDto>? LotNumber { get; set; } = new List<LotNumberOptionDto>();
        public string? Unit { get; set; }
        public bool IsActive { get; set; } = true;
        // Lấy dữ liệu tồn kho
        public decimal OnHandKg { get; set; }
        public decimal? ReservedOpenAllKg { get; set; }
        public decimal? AvailableKg { get; set; }
    }
}
