using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;
using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema
{
    public class QuotationStatusHistory
    {
        public Guid Id { get; set; }
        public Guid QuotationId { get; set; }

        public QuotationStatus FromStatus { get; set; }
        public QuotationStatus ToStatus { get; set; }

        public string? Note { get; set; }

        public Guid ChangedBy { get; set; }
        public DateTime ChangedDate { get; set; }

        public virtual Employee ChangedByNavigation { get; set; } = null!;

        public virtual Quotation Quotation { get; set; } = null!;
    }
}
