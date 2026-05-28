using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Dispatch;

public interface IDispatchReadDbContext
{
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
    DbSet<Deliverer> Deliverers { get; }
    DbSet<DelivererInfor> DelivererInfors { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; }
}
