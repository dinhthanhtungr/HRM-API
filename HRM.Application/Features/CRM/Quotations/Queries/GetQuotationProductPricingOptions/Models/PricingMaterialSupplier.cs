using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions.Models
{
    internal class PricingMaterialSupplier
    {
        public Guid MaterialId { get; init; }
        public Guid MaterialsSupplierId { get; init; }
        public Guid SupplierId { get; init; }
        public string SupplierCode { get; init; } = string.Empty;
        public string SupplierName { get; init; } = string.Empty;
        public decimal? CurrentPrice { get; init; }
        public string? Currency { get; init; }
        public bool IsPreferred { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }
}
