using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public partial class MfgProductionOrder
    {
        public Guid MfgProductionOrderId { get; set; }
        public string ExternalId { get; set; } = string.Empty;

        //public string? ProductionType { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public string? ColorName { get; set; }

        public Guid? CustomerId { get; set; }
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerExternalIdSnapshot { get; set; }

        public Guid? FormulaId { get; set; }
        public string? FormulaExternalIdSnapshot { get; set; }

        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime RequiredDate { get; set; }

        public decimal TotalQuantityRequest { get; set; }
        public decimal? TotalQuantity { get; set; }

        public int? NumOfBatches { get; set; }
        public decimal UnitPriceAgreed { get; set; }

        public string Status { get; set; } = ManufacturingProductOrder.New.ToString();

        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? PlpuNote { get; set; }

        public string BagType { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public string? QcCheck { get; set; }
        public StepOfProduct? StepOfProduct { get; set; }

        public Guid CompanyId { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public Guid UpdatedBy { get; set; }


        public virtual Customer? Customer { get; set; }
        public virtual Product Product { get; set; } = null!;
        public virtual Formula Formula { get; set; } = null!;
        public virtual Company? Company { get; set; }
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? UpdatedByNavigation { get; set; }


        //public virtual ICollection<ManufacturingFormula> ManufacturingFormulas { get; set; } = new List<ManufacturingFormula>();
        public virtual ICollection<ProductionSelectVersion> ProductionSelectVersions { get; set; } = new List<ProductionSelectVersion>();
        public virtual ICollection<SchedualMfg> SchedualMfgs { get; set; } = new List<SchedualMfg>();
    }
}
