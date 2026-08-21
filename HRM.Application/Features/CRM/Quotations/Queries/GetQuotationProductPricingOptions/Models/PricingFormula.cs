using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions.Models
{
    internal class PricingFormula
    {
        public Guid ProductId { get; init; }
        public Guid FormulaId { get; init; }
        public string FormulaExternalId { get; init; } = string.Empty;
        public string FormulaName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal MaterialCost { get; init; }
        public bool IsSelected { get; init; }
        public DateTime? PricingUpdatedDate { get; init; }
    }
}
