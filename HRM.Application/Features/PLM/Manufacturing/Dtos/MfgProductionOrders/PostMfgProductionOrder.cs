using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders
{
    public class PostMfgProductionOrder
    {
        public string? ExternalId { get; set; }
        public Guid MerchandiseOrderId { get; set; }
        public string? MerchandiseOrderExternalId { get; set; }
        public string? ProductionType { get; set; }
        public Guid ProductId { get; set; } //
        public string? ProductExternalIdSnapshot { get; set; } //
        public string? ProductNameSnapshot { get; set; } //
        public Guid? CustomerId { get; set; } //
        public string? CustomerNameSnapshot { get; set; } //
        public string? CustomerExternalIdSnapshot { get; set; } //
        public Guid? FormulaId { get; set; }
        public string? FormulaNameSnapshot { get; set; }
        public string? FormulaExternalIdSnapshot { get; set; }
        public string? Status { get; set; }
        public Guid? CompanyId { get; set; }
        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime? requiredDate { get; set; }
        public int? TotalQuantity { get; set; }
        public int? NumOfBatches { get; set; }
        public int? QuantityPerBatch { get; set; }
        public string? Comment { get; set; }
        public string? Requirement { get; set; }
        public string? BagType { get; set; }
        public DateTime? CreateDate { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
