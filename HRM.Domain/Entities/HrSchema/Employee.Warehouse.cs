using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<WarehouseTempStock> WarehouseTempStockCreatedByNavigations { get; set; } = new List<WarehouseTempStock>();
    public virtual ICollection<WarehouseShelfStock> WarehouseShelfStockUpdatedByNavigations { get; set; } = new List<WarehouseShelfStock>();
    public virtual ICollection<WarehouseRequest> WarehouseRequestCreatedByNavigations { get; set; } = new List<WarehouseRequest>();
    public virtual ICollection<WarehouseRequest> WarehouseRequestUpdatedByNavigations { get; set; } = new List<WarehouseRequest>();
    public virtual ICollection<WarehouseVoucher> WarehouseVoucherCreatedByNavigations { get; set; } = new List<WarehouseVoucher>();
    public virtual ICollection<WarehouseShelfLedger> WarehouseShelfLedgerCreatedByNavigations { get; set; } = new List<WarehouseShelfLedger>();
}
