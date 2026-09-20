using HRM.Domain.Enums.Formulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationProductPricingOptions.Models
{
    internal class PricingMaterial
    {
        public Guid FormulaId { get; init; }
        public Guid FormulaMaterialId { get; init; }
        public Guid? ItemId { get; init; }
        public ItemType ItemType { get; init; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public MaterialPurchaseStatus? PurchaseStatus { get; init; }
        public string? PurchaseStatusReason { get; init; }
        public DateTime? PurchaseStatusEffectiveFrom { get; init; }
        public DateTime? ExpectedAvailableDate { get; init; }
        public int LineNo { get; init; }
    }
}
