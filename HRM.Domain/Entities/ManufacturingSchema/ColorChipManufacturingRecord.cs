using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ColorChipManufacturingRecord
    {
        // =========================================================
        // 1. Identity
        // =========================================================
        public Guid ColorChipMfgRecordId { get; set; }

        // =========================================================
        // 2. Classification / Business Type
        // =========================================================
        public ResinType ResinType { get; set; }
        public LogoType LogoType { get; set; }
        public FormStyle FormStyle { get; set; }

        // =========================================================
        // 3.MfgProductionOrder Snapshot
        // =========================================================
        public Guid? MfgProductionOrderId { get; set; }

        // =========================================================
        // 4.ManufacturingFormula Snapshot
        // =========================================================
        public Guid? ManufacturingFormulaId { get; set; }

        // =========================================================
        // 6. Technical Information
        // =========================================================
        public string? Machine { get; set; }
        public string? StandardFormula { get; set; } 
        public string? Resin { get; set; }
        public string? TemperatureLimit { get; set; } = string.Empty;

        // =========================================================
        // 7. Physical / Form Information
        // =========================================================
        public string? SizeText { get; set; }          // ví dụ: 2.4 x 3.2
        public decimal? PelletWeightGram { get; set; } // trọng lượng hạt
        public string? NetWeightGram { get; set; }          // trọng lượng tịnh
        public bool? Electrostatic { get; set; }    // Tĩnh điện, ...
        public string? DeltaE { get; set; }              // Delta E (độ lệch màu)

        // =========================================================
        // 8. Document / Record Info
        // =========================================================
        public DateTime? RecordDate { get; set; }
        public string? Note { get; set; }
        public string? PrintNote { get; set; }
        // =========================================================
        // 9. Audit
        // =========================================================
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public Guid CompanyId { get; set; }
        public bool IsActive { get; set; }

        // =========================================================
        // 10. Navigation Properties
        // =========================================================
        public virtual ManufacturingFormula? ManufacturingFormula { get; set; } 
        public virtual MfgProductionOrder? MfgProductionOrder { get; set; }
    }
}
