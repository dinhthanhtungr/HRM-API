using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<MfgProductionOrder> MfgProductionOrderCheckedByNavigations { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<MfgProductionOrder> MfgProductionOrderCreatedByNavigations { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<MfgProductionOrder> MfgProductionOrderUpdatedByNavigations { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<ManufacturingFormula> ManufacturingFormulaCreatedByNavigations { get; set; } = new List<ManufacturingFormula>();
    public virtual ICollection<ManufacturingFormula> ManufacturingFormulaUpdatedByNavigations { get; set; } = new List<ManufacturingFormula>();
    public virtual ICollection<ProductionSelectVersion> ProductionSelectVersionCreatedByNavigations { get; set; } = new List<ProductionSelectVersion>();
    public virtual ICollection<ProductionSelectVersion> ProductionSelectVersionClosedByNavigations { get; set; } = new List<ProductionSelectVersion>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulaCreatedByNavigations { get; set; } = new List<ProductStandardFormula>();
    public virtual ICollection<ProductStandardFormula> ProductStandardFormulaClosedByNavigations { get; set; } = new List<ProductStandardFormula>();
}
