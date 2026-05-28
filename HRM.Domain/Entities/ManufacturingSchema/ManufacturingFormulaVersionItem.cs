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
    public class ManufacturingFormulaVersionItem
    {
        public Guid ManufacturingFormulaVersionItemId { get; set; }
        public Guid ManufacturingFormulaVersionId { get; set; }

        public Guid? MaterialId { get; set; }              // để tính MRP/đối chiếu
        public Guid CategoryId { get; set; }
        public Guid? ProductId { get; set; }               // để lọc công thức theo sản phẩm

        public ItemType itemType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // snapshot hiển thị (không đổi nghĩa)
        public string MaterialNameSnapshot { get; set; } = default!;
        public string MaterialExternalIdSnapshot { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public int LineNo { get; set; }

        public virtual Product Product { get; set; } = null!;
        public ManufacturingFormulaVersion Version { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}
