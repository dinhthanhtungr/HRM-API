using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public partial class ManufacturingFormulaMaterial
    {
        public Guid ManufacturingFormulaMaterialId { get; set; }

        public Guid ManufacturingFormulaId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid CategoryId { get; set; }
        public Guid? ProductId { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public ItemType itemType { get; set; }
        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
        public string? Unit { get; set; }
        public bool IsActive { get; set; } = true;
        public string? LotNo { get; set; }
        public int LineNo { get; set; }

        public virtual Product Product { get; set; } = null!;
        public virtual ManufacturingFormula ManufacturingFormula { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;

    }
}
