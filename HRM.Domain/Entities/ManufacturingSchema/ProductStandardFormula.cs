using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ProductStandardFormula
    {
        public Guid ProductStandardFormulaId { get; set; }

        public Guid ProductId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }

        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }          // null = hiện hành

        public Guid CreatedBy { get; set; }
        public Guid? ClosedBy { get; set; }

        public Guid CompanyId { get; set; }

        public string? Note { get; set; } = string.Empty;

        public virtual Product Product { get; set; } = null!;
        public virtual ManufacturingFormula? ManufacturingFormula { get; set; }
        public virtual Company? Company { get; set; }
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? ClosedByNavigation { get; set; }
    }
}
