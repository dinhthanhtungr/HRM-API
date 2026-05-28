using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Domain.Entities.OrderSchema;

public partial class MerchandiseOrder
{
    public Guid MerchandiseOrderId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public OrderType OrderType { get; set; } = OrderType.Merchandise;

    public Guid AttachmentCollectionId { get; set; }


    public Guid CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerExternalIdSnapshot { get; set; } = string.Empty;
    public string PhoneSnapshot { get; set; } = string.Empty;

    public Guid ManagerById { get; set; }
    public string ManagerByNameSnapshot { get; set; } = string.Empty;
    public string ManagerExternalIdSnapshot { get; set; } = string.Empty;

    public string Receiver { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;

    public decimal? TotalPrice { get; set; }

    public string? PaymentType { get; set; }

    public decimal? Vat { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Currency { get; set; }
    public decimal? ExchangeRate { get; set; }

    public Guid CompanyId { get; set; }
    public bool IsPaid { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime? PaymentDate { get; set; }

    public string? Note { get; set; }
    public string? ShippingMethod { get; set; }
    public string PONo { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public virtual Company? Company { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Employee? ManagerBy { get; set; }


    public virtual AttachmentCollection AttachmentCollection { get; set; } = null!;
    public virtual ICollection<DeliveryOrderPO> DeliveryOrderPOs { get; set; } = new List<DeliveryOrderPO>();
    public virtual ICollection<PurchaseOrderLink> PurchaseOrderLinks { get; set; } = new List<PurchaseOrderLink>();
    //public virtual ICollection<DeliveryOrderDetail> DeliveryOrderDetails { get; set; } = new List<DeliveryOrderDetail>();
    public virtual ICollection<MerchandiseOrderDetail> MerchandiseOrderDetails { get; set; } = new List<MerchandiseOrderDetail>();

    public virtual Employee? UpdatedByNavigation { get; set; }
}
