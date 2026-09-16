using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Warehouse;

public interface IWarehouseReadDbContext
{
    DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; }

    DbSet<WarehouseTempStock> WarehouseTempStocks { get; }

    DbSet<Material> Materials { get; }

    DbSet<Product> Products { get; }

    DbSet<SampleRequest> SampleRequests { get; }

    DbSet<Customer> Customers { get; }

    DbSet<FormulaMaterial> FormulaMaterials { get; }

    DbSet<WarehouseVoucher> WarehouseVouchers { get; }

    DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; }

    DbSet<WarehouseRequest> WarehouseRequests { get; }

    DbSet<WarehouseShelfLedger> WarehouseShelfLedgers { get; }

    DbSet<Company> Companies { get; }

    DbSet<Employee> Employees { get; }

    DbSet<PurchaseOrder> PurchaseOrders { get; }

    DbSet<PurchaseOrderSnapshot> PurchaseOrderSnapshots { get; }
}
