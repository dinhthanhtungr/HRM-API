using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.PLM.SaleOrders;

public interface ISaleOrderDbContext
{
    DbSet<AttachmentCollection> AttachmentCollections { get; }
    DbSet<AttachmentModel> AttachmentModels { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerAssignment> CustomerAssignments { get; }
    DbSet<CustomerClaim> CustomerClaims { get; }
    DbSet<CustomerTransferLog> CustomerTransferLogs { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; }
    DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; }
    DbSet<Employee> Employees { get; }
    DbSet<EventLog> EventLogs { get; }
    DbSet<Formula> Formulas { get; }
    DbSet<FormulaMaterial> FormulaMaterials { get; }
    DbSet<ManufacturingFormula> ManufacturingFormulas { get; }
    DbSet<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; }
    DbSet<MaterialsSupplier> MaterialsSuppliers { get; }
    DbSet<MemberInGroup> MemberInGroups { get; }
    DbSet<ComplaintReport> ComplaintReports { get; }
    DbSet<ComplaintReportLine> ComplaintReportLines { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<MfgOrderPO> MfgOrderPOs { get; }
    DbSet<MfgProductionOrder> MfgProductionOrders { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductStandardFormula> ProductStandardFormulas { get; }
    DbSet<SampleRequest> SampleRequests { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
