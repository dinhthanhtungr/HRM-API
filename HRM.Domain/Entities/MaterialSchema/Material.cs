using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.SupplyRequestSchema;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class Material
{
    public Guid MaterialId { get; set; }

    public string? ExternalId { get; set; }

    public string? CustomCode { get; set; }

    public string? Name { get; set; }

    public Guid CategoryId { get; set; }

    public double? Weight { get; set; }

    public string? Unit { get; set; }

    public string? Package { get; set; }

    public string? Comment { get; set; }

    public double? MinQuantity { get; set; }

    public Guid CompanyId { get; set; }

    public bool? IsActive { get; set; }

    public string? Barcode { get; set; } //mã “master” để quét ra sản phẩm/vật tư (thường là GTIN/EAN-13).

    public Guid? AttachmentCollectionId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual Company Company { get; set; } = null!;

    public virtual Employee? CreatedByNavigation { get; set; }
    public virtual Employee? UpdatedByNavigation { get; set; }
    public virtual ICollection<FormulaMaterial> FormulaMaterials { get; set; } = new List<FormulaMaterial>();
    public virtual ICollection<MaterialGroupName> MaterialGroupNames { get; set; } = new List<MaterialGroupName>();

    public virtual ICollection<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<ManufacturingFormulaMaterial>();
    public virtual ICollection<ManufacturingFormulaVersionItem> Items { get; set; } = new List<ManufacturingFormulaVersionItem>();

    public virtual ICollection<MaterialsSupplier> MaterialsSuppliers { get; set; } = new List<MaterialsSupplier>();

    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    public virtual ICollection<SupplyRequestDetail> SupplyRequestDetails { get; set; } = new List<SupplyRequestDetail>();
    //public virtual ICollection<QCInputByWarehouse> QCInputByWarehouses { get; set; } = new List<QCInputByWarehouse>();

}
