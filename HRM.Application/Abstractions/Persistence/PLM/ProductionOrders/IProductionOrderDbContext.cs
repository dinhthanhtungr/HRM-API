using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;

/// <summary>MFG và reservation dùng chung DbContext/transaction, không ghi nhận xuất kho.</summary>
public interface IProductionOrderDbContext : IPLMWriteDbContext
{
    DbSet<WarehouseTempStock> WarehouseTempStocks { get; }
}
