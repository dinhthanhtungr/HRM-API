using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ManufacturingFormulaVersion
    {
        public Guid ManufacturingFormulaVersionId { get; set; }
        public Guid ManufacturingFormulaId { get; set; }

        public int VersionNo { get; set; }                // 1,2,3...
        public string Status { get; set; } = "Draft";     // Draft/Released/Obsolete
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Note { get; set; }

        public ICollection<ManufacturingFormulaVersionItem> Items { get; set; } = new List<ManufacturingFormulaVersionItem>();

        public ManufacturingFormula ManufacturingFormula { get; set; } = null!;
    }
}
