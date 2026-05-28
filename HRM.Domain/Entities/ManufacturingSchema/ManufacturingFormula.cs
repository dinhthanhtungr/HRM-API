using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public partial class ManufacturingFormula
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = ManufacturingProductOrderFormula.New.ToString();
        public decimal? TotalPrice { get; set; }

        // Nguồn gốc (đa hình 1-trong-2)
        public Guid? SourceManufacturingFormulaId { get; set; }
        public string? SourceManufacturingExternalIdSnapshot { get; set; }
        public Guid? SourceVUFormulaId { get; set; }
        public string? SourceVUExternalIdSnapshot { get; set; }
        public FormulaSource SourceType { get; set; } // FromVA / FromVU (không nullable)


        public bool IsActive { get; set; }
        public string? Note { get; set; }

        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public Guid UpdatedBy { get; set; }
        public Guid CompanyId { get; set; }

        public virtual ICollection<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<ManufacturingFormulaMaterial>();
        public virtual ICollection<ProductStandardFormula> ProductStandardFormulas { get; set; } = new List<ProductStandardFormula>();
        public virtual ICollection<ProductionSelectVersion> ProductionSelectVersions { get; set; } = new List<ProductionSelectVersion>();
        public virtual ICollection<ManufacturingFormulaVersion> ManufacturingFormulaVersions { get; set; } = new List<ManufacturingFormulaVersion>();
      
        public virtual ManufacturingFormula? SourceManufacturingFormula { get; set; }
        public virtual Formula? SourceVUFormula { get; set; }
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? UpdatedByNavigation { get; set; }
        public virtual Company? Company { get; set; }
    }
}
