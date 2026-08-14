using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema
{
    public class QuotationLine
    {
        public Guid QuotationLineId { get; set; }
        public Guid QuotationId { get; set; }
        public Guid? SampleRequestId { get; set; }
        public Guid? ProductPricingVersionId { get; set; }

        public Guid ProductId { get; set; }
        public string ProductExternalIdSnapshot { get; set; } = string.Empty;
        public string ProductNameSnapshot { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal LineTotal { get; set; }

        public QuotationLinePriceMode PriceMode { get; set; } = QuotationLinePriceMode.Fixed;

        public string? Note { get; set; }
        public int SortOrder { get; set; }

        public virtual Product ProductNavigation { get; set; } = null!;
        public virtual SampleRequest? SampleRequest { get; set; }
        public virtual ProductPricingVersion? ProductPricingVersion { get; set; }
        public virtual Quotation Quotation { get; set; } = null!;
        public virtual ICollection<QuotationLinePriceTier> PriceTiers { get; set; } = [];
    }
}
