using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.DevandqaSchema
{
    public class ProductStandard
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        public string? externalId { get; set; }

        public string? ProductExternalId { get; set; }
        public string? Status { get; set; }
        public string? DeltaE { get; set; }
        public string? PelletSize { get; set; }
        public string? Moisture { get; set; }
        public string? Density { get; set; }
        public string? MeltIndex { get; set; }
        public string? TensileStrength { get; set; }
        public string? ElongationAtBreak { get; set; }
        public string? FlexuralStrength { get; set; }
        public string? FlexuralModulus { get; set; }
        public string? IzodImpactStrength { get; set; }
        public string? Hardness { get; set; }
        public string? DwellTime { get; set; }
        public string? BlackDots { get; set; }
        public string? MigrationTest { get; set; }


        public int Weight { get; set; }
        public string? packed { get; set; }

        public string? Shape { get; set; }

        public Guid CompanyId { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual Company? Company { get; set; }
        public virtual Product? Product { get; set; }
        public virtual Employee? CreatedByNavigation { get; set; }
    }

}
