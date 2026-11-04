using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Dispatch;

public interface IDispatchReadDbContext
{
    DbSet<Employee> Employees { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; }
    DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
    DbSet<Deliverer> Deliverers { get; }
    DbSet<DelivererInfor> DelivererInfors { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<WarehouseRequest> WarehouseRequests { get; }
    DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; }
    DbSet<WarehouseTempStock> WarehouseTempStocks { get; }
    DbSet<ManufacturingFormula> ManufacturingFormulas { get; }
    DbSet<MfgOrderPO> MfgOrderPOs { get; }
    DbSet<Formula> Formulas { get; }
}
