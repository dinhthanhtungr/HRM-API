using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;

public interface IComplaintReportDbContext
{
    DbSet<AttachmentCollection> AttachmentCollections { get; }
    DbSet<AttachmentModel> AttachmentModels { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerAssignment> CustomerAssignments { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Part> Parts { get; }
    DbSet<ComplaintReport> ComplaintReports { get; }
    DbSet<ComplaintReportLine> ComplaintReportLines { get; }
    DbSet<ComplaintReportLineLot> ComplaintReportLineLots { get; }
    DbSet<ComplaintCapaAction> ComplaintCapaActions { get; }
    DbSet<ComplaintReportApproval> ComplaintReportApprovals { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; }
    DbSet<MfgOrderPO> MfgOrderPOs { get; }
    DbSet<MfgProductionOrder> MfgProductionOrders { get; }
    DbSet<ProductionSelectVersion> ProductionSelectVersions { get; }
    DbSet<ManufacturingFormula> ManufacturingFormulas { get; }
    DbSet<CustomerInteraction> CustomerInteractions { get; }
    DbSet<CustomerInteractionReference> CustomerInteractionReferences { get; }
    DbSet<EventLog> EventLogs { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
