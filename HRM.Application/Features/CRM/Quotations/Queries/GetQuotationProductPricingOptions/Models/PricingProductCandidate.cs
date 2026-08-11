using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions.Models
{
    internal class PricingProductCandidate
    {
        public Guid SampleRequestId { get; init; }
        public string SampleRequestExternalId { get; init; } = string.Empty;
        public DateTime CompletedDate { get; init; }
        public Guid ProductId { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;

        public Guid CustomerId { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string CustomerExternalId { get; init; } = string.Empty;
    }
}
