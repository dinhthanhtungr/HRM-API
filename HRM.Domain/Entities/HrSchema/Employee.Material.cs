using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<Material> MaterialCreatedByNavigations { get; set; } = new List<Material>();
    public virtual ICollection<Material> MaterialUpdatedByNavigations { get; set; } = new List<Material>();
    public virtual ICollection<MaterialsSupplier> MaterialsSupplierCreatedByNavigations { get; set; } = new List<MaterialsSupplier>();
    public virtual ICollection<MaterialsSupplier> MaterialsSupplierUpdatedByNavigations { get; set; } = new List<MaterialsSupplier>();
    public virtual ICollection<PriceHistory> PriceHistoryCreatedByNavigations { get; set; } = new List<PriceHistory>();
    public virtual ICollection<Supplier> SupplierCreatedByNavigations { get; set; } = new List<Supplier>();
    public virtual ICollection<Supplier> SupplierUpdatedByNavigations { get; set; } = new List<Supplier>();
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
