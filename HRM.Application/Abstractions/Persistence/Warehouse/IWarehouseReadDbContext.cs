using HRM.Domain.Entities.MaterialSchema;
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
}
