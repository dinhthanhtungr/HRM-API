using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.Purchasing;

public interface IPurchaseOrderDbContext
{
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; }
    DbSet<PurchaseOrderSnapshot> PurchaseOrderSnapshots { get; }
    DbSet<PurchaseOrderLink> PurchaseOrderLinks { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Material> Materials { get; }
    DbSet<MaterialsSupplier> MaterialsSuppliers { get; }
    DbSet<PriceHistory> PriceHistories { get; }
    DbSet<Employee> Employees { get; }
    DbSet<WarehouseRequest> WarehouseRequests { get; }
    DbSet<WarehouseRequestDetail> WarehouseRequestDetails { get; }
    DbSet<WarehouseVoucher> WarehouseVouchers { get; }
    DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; }
    DbSet<WarehouseShelfLedger> WarehouseShelfLedgers { get; }
    DbSet<QCInputByQC> QCInputByQCs { get; }
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
