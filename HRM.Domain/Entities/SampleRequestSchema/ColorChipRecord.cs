using System;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Domain.Entities.SampleRequestSchema
{
    public class ColorChipRecord
    {
        // =========================================================
        // 1. Identity
        // =========================================================
        public Guid ColorChipRecordId { get; set; }

        // =========================================================
        // 2. Classification / Business Type
        // =========================================================
        public RecordType RecordType { get; set; }
        public ResinType ResinType { get; set; }
        public LogoType LogoType { get; set; }
        public FormStyle FormStyle { get; set; }

        // =========================================================
        // 3. Product Snapshot
        // =========================================================
        public Guid? ProductId { get; set; }


        // =========================================================
        // 6. Technical Information
        // =========================================================
        public string? Machine { get; set; }
        public string? Resin { get; set; }
        public string? TemperatureLimit { get; set; } = string.Empty;

        // =========================================================
        // 7. Physical / Form Information
        // =========================================================
        public string? SizeText { get; set; }          // ví dụ: 2.4 x 3.2
        public decimal? PelletWeightGram { get; set; } // trọng lượng hạt
        public string? NetWeightGram { get; set; }          // trọng lượng tịnh
        public bool? Electrostatic { get; set; }    // Tĩnh điện, ...

        // =========================================================
        // 8. Document / Record Info
        // =========================================================
        public Guid? AttachmentCollectionId { get; set; }
        public DateTime? RecordDate { get; set; }
        public string? Note { get; set; }
        public string? PrintNote { get; set; }
        public decimal Lightness { get; set; }   // L*
        public decimal AValue { get; set; }      // a*
        public decimal BValue { get; set; }      // b*
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
        public virtual AttachmentCollection? AttachmentCollection { get; set; } = null!;
        public virtual ICollection<ColorChipRecordDevelopmentFormula> DevelopmentFormulas { get; set; }
            = new List<ColorChipRecordDevelopmentFormula>();
        public virtual Product? Product { get; set; }

    }
}
