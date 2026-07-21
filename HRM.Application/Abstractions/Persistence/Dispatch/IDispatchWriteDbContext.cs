using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Persistence.Dispatch
{
    public interface IDispatchWriteDbContext
    {
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; }
    DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
        DbSet<Deliverer> Deliverers { get; }
        DbSet<DelivererInfor> DelivererInfors { get; }
        DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
