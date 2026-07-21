using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;
using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema
{
    public class Quotation
    {
        public Guid QuotationId { get; set; }

        public string ExternalId { get; set; } = string.Empty;

        public Guid CustomerId { get; set; }
        public Guid? ContactId { get; set; }
        public string? ContactName { get; set; }

        public Guid CompanyId { get; set; }
        public Guid SaleEmployeeId { get; set; }

        public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

        public string Currency { get; set; } = "VND";
        public decimal ExchangeRate { get; set; } = 1;

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime QuotationDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public DateTime? SentDate { get; set; }

        public string? PaymentTerms { get; set; }
        public string? DeliveryTerms { get; set; }
        public string? Note { get; set; }

        public int Version { get; set; } = 1;
        public Guid? PreviousQuotationId { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public virtual Customer Customer { get; set; } = null!;
        public virtual Contact? Contact { get; set; } = null;
        public virtual Company Company { get; set; } = null!;
        public virtual Employee SaleEmployee { get; set; } = null!;
        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? UpdatedByNavigation { get; set; }

        public virtual Quotation? PreviousQuotation { get; set; }
        public virtual ICollection<Quotation> RevisedQuotations { get; set; } = [];
        public virtual ICollection<QuotationLine> Lines { get; set; } = [];
        public virtual ICollection<QuotationStatusHistory> StatusHistories { get; set; } = [];
    }
}
