using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.SampleRequestSchema
{
    public class ManufacturingVUFormula
    {
        public Guid ManufacturingVUFormulaId { get; set; }
        public Guid FormulaId { get; set; }

        public ManufacturingProductOrder status { get; set; }

        public decimal? TotalProductionQuantity { get; set; }
        public int? NumOfBatches { get; set; }

        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? QcCheck { get; set; }

        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public Guid UpdatedBy { get; set; }

        public virtual Formula Formula { get; set; } = null!;
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? UpdatedByNavigation { get; set; }

        public virtual ICollection<FormulaMaterialSnapshot> FormulaMaterialSnapshots { get; set; } = new List<FormulaMaterialSnapshot>();
    }
}
