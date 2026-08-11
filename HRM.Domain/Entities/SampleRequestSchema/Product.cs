using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class Product
{
    public Guid ProductId { get; set; }

    public string? ColourCode { get; set; }

    public string? Name { get; set; }

    public string? ColourName { get; set; }

    public string? Additive { get; set; }

    public double? UsageRate { get; set; }

    public string? DeltaE { get; set; }

    public string? Requirement { get; set; }

    public string? ExpiryType { get; set; }

    public bool? StorageCondition { get; set; }

    public string? LabComment { get; set; }

    public string? Procedure { get; set; }

    public double? RecycleRate { get; set; }

    public double? TaicalRate { get; set; }

    public string? Application { get; set; }

    public string? ProductUsage { get; set; }

    public string? PolymerMatchedIn { get; set; }

    public string? Code { get; set; }

    public string? EndUser { get; set; }

    public bool? FoodSafety { get; set; }

    public bool? RohsStandard { get; set; }
    public bool? ReachStandard { get; set; }

    public double? MaxTemp { get; set; }

    public string? WeatherResistance { get; set; }

    public string? LightCondition { get; set; }

    public string? VisualTest { get; set; }

    public bool? ReturnSample { get; set; }
    public bool IsRecycle { get; set; } = false;

    public string? OtherComment { get; set; }

    public Guid CategoryId { get; set; }

    public double? Weight { get; set; }

    public string? Unit { get; set; }

    public DateTime? CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Category? Category { get; set; }

    public virtual Company? Company { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual ICollection<FormulaMaterial> FormulaMaterials { get; set; } = new List<FormulaMaterial>();
    public virtual ICollection<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<ManufacturingFormulaMaterial>();
    public virtual ICollection<ManufacturingFormulaVersionItem> ManufacturingFormulaVersionItems { get; set; } = new List<ManufacturingFormulaVersionItem>();

    public virtual ICollection<DeliveryOrderDetail> DeliveryOrderDetails { get; set; } = new List<DeliveryOrderDetail>();
    public virtual ICollection<Formula> Formulas { get; set; } = new List<Formula>();

    public virtual ICollection<MerchandiseOrderDetail> MerchandiseOrderDetails { get; set; } = new List<MerchandiseOrderDetail>();
    public virtual ICollection<ComplaintReportLine> ComplaintReportLines { get; set; } = new List<ComplaintReportLine>();
    public virtual ICollection<ColorChipRecord> ColorChipRecords { get; set; } = new List<ColorChipRecord>();
    public virtual ICollection<MfgProductionOrder> MfgProductionOrders { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<SchedualMfg> SchedualMfgs { get; set; } = new List<SchedualMfg>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulas { get; set; } = new List<ProductStandardFormula>();
    public virtual ICollection<ProductStandard> ProductStandards { get; set; } = new List<ProductStandard>();
    public virtual ICollection<SampleRequest> SampleRequests { get; set; } = new List<SampleRequest>();
    public virtual Employee? UpdatedByNavigation { get; set; }
}
