using System;
using System.Collections.Generic;
using System.Text;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.PrintectSchema
{
    public class CustomerLabelHeader
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }
        public string? ColorCode { get; set; }

        public Guid CustomerId { get; set; }
        public string? CustomerExternalId { get; set; }

        public string? LabelType { get; set; }

        // Mẫu và logo mặc định cho cấu hình riêng theo khách hàng + ColorCode.
        public Guid? PrintLabelTemplateId { get; set; }
        public Guid? DefaultPrintLabelLogoId { get; set; }

        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public virtual Product Product { get; set; } = null!;
        public virtual Customer Customer { get; set; } = null!;
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? UpdatedByNavigation { get; set; }
        public virtual PrintLabelTemplate? PrintLabelTemplate { get; set; }
        public virtual PrintLabelLogo? DefaultPrintLabelLogo { get; set; }

        public virtual ICollection<CustomerLabelDetail> Details { get; set; } = new List<CustomerLabelDetail>();
    }
}
