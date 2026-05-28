using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ProductionSelectVersion
    {
        public Guid ProductionSelectVersionId { get; set; }

        public Guid MfgProductionOrderId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }


        //public bool IsSelected { get; set; }    // true = công thức được chọn, false = công thức không được chọn (dù có thể vẫn còn hiệu lực)

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }          // null = hiện hành

        public Guid CreatedBy { get; set; }
        public Guid? ClosedBy { get; set; }

        public Guid CompanyId { get; set; }

        public virtual MfgProductionOrder MfgProductionOrder { get; set; } = null!;   
        public virtual ManufacturingFormula? ManufacturingFormula { get; set; }
        public virtual Company? Company { get; set; }
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? ClosedByNavigation { get; set; }
    }
}
