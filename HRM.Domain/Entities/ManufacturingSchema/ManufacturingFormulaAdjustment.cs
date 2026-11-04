using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustment
    {
        public Guid ManufacturingFormulaAdjustmentId { get; set; }

        public Guid MfgProductionOrderId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }

        public string Status { get; set; } = AdjustmentStatus.Draft.ToString();

        public string? Note { get; set; }

        public Guid CompanyId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public virtual MfgProductionOrder MfgProductionOrder { get; set; } = default!;
        public virtual ManufacturingFormula? ManufacturingFormula { get; set; }
        public virtual Company Company { get; set; } = default!;
        public virtual Employee CreatedByNavigation { get; set; } = default!;
        public virtual Employee? UpdatedByNavigation { get; set; }
        public virtual ICollection<ManufacturingFormulaAdjustmentBatch> Batches { get; set; } = new List<ManufacturingFormulaAdjustmentBatch>();
    }
}
