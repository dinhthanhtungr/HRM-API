using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderLineRaw
    {
        public Guid PurchaseOrderId { get; set; }
        public string POExternalId { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;

        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public Guid? CompanyId { get; set; }

        public DateTime? CreatedDate { get; set; }
        public Guid? CreatedBy { get; set; }

        public DateTime? HeaderRequestDeliveryDate { get; set; }
        public DateTime? RealDeliveryDate { get; set; }

        public string POStatus { get; set; } = string.Empty;
        public string PLPUComment { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public bool POIsActive { get; set; }

        public Guid PurchaseOrderDetailId { get; set; }
        public int LineNo { get; set; }

        public Guid MaterialId { get; set; }
        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; } // Category của vật tư, dùng để nhận diện bao bì.
        public string Package { get; set; } = string.Empty;

        public decimal OrderedQuantity { get; set; }

        public decimal? BaseCostSnapshot { get; set; }
        public DateTime? BaseDateSnapshot { get; set; }

        public decimal? UnitPriceAgreed { get; set; }
        public decimal? TotalPriceAgreed { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public string Note { get; set; } = string.Empty;
    }
