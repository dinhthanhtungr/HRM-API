using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.SampleRequestSchema
{
    public class FormulaMaterialSnapshot
    {
        public Guid FormulaMaterialSnapshotId { get; set; }   // PK
        public Guid ManufacturingVUFormulaId { get; set; }    // FK to ManufacturingVUFormula
        public Guid CategoryId { get; set; }

        public decimal Quantity { get; set; }                // DECIMAL(18,6)
        public decimal UnitPrice { get; set; }               // DECIMAL(16,2)
        public decimal TotalPrice { get; set; }              // DECIMAL(16,2)

        public ItemType itemType { get; set; }
        public string? LotNo { get; set; }                        // VARCHAR
        public int LineNo { get; set; }

        public string? MaterialNameSnapshot { get; set; }         // NVARCHAR
        public string? MaterialExternalIdSnapshot { get; set; }   // VARCHAR
        public string? Unit { get; set; }                         // VARCHAR
        public bool IsActive { get; set; } = true;

        public virtual ManufacturingVUFormula ManufacturingVUFormula { get; set; } = null!;

    }
}
