using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.AuditSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;

public interface IDeliveryScheduleDbContext
{
    DbSet<global::HRM.Domain.Entities.CustomerSchema.Customer> Customers { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<global::HRM.Domain.Entities.ManufacturingSchema.MfgOrderPO> MfgOrderPOs { get; }
    DbSet<MerchandiseOrderDeliverySchedule> MerchandiseOrderDeliverySchedules { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderScheduleAllocation> DeliveryOrderScheduleAllocations { get; }
    DbSet<AuditLog> AuditLogs { get; }
    Task<IDbContextTransaction> BeginDeliveryScheduleTransactionAsync(CancellationToken ct);
    Task LockDeliveryScheduleAsync(Guid companyId, Guid detailId, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
