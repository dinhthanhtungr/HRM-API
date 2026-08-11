using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Timeline;

public interface ITimelineDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<ComplaintReport> ComplaintReports { get; }
    DbSet<ComplaintReportLine> ComplaintReportLines { get; }
    DbSet<Customer> Customers { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; }
    DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
    DbSet<Employee> Employees { get; }
    DbSet<EventLog> EventLogs { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<MfgOrderPO> MfgOrderPOs { get; }
    DbSet<MfgProductionOrder> MfgProductionOrders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
