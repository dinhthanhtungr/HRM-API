using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.ShiftReportSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.AuditSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.PLM.Manufacturing;

/// <summary>Persistence cho module MFG; caller sở hữu transaction, không phụ thuộc Infrastructure.</summary>
public interface IManufacturingDbContext : IProductionOrderDbContext
{
    /// <summary>Khóa transaction theo công ty/lệnh trước khi đọc trạng thái để xử lý mutation.</summary>
    Task LockProductionOrderAsync(Guid productionOrderId, Guid companyId, CancellationToken ct = default);
    Task LockManufacturingFormulaAsync(Guid formulaId, Guid companyId, CancellationToken ct = default);
    Task LockManufacturingProductAsync(Guid productId, Guid companyId, CancellationToken ct = default);
    Task LockManufacturingColorChipAsync(Guid recordId, Guid companyId, CancellationToken ct = default);
    DbSet<ManufacturingFormulaAdjustment> ManufacturingFormulaAdjustments { get; }
    DbSet<ManufacturingFormulaAdjustmentBatch> ManufacturingFormulaAdjustmentBatches { get; }
    DbSet<ManufacturingFormulaAdjustmentItem> ManufacturingFormulaAdjustmentItems { get; }
    DbSet<EndOfShiftReportForAll> EndOfShiftReportForAlls { get; }
    DbSet<EndOfShiftReportDetailForAll> EndOfShiftReportDetailForAlls { get; }
    DbSet<OperationHistoryMd> OperationHistoryMds { get; }
    DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; }
    DbSet<ProductionOutputReceiptSource> ProductionOutputReceiptSources { get; }
    DbSet<OperationMaterialRequestLink> OperationMaterialRequestLinks { get; }
    DbSet<WarehouseRequest> WarehouseRequests { get; }
    DbSet<TempStockIssueLedger> TempStockIssueLedgers { get; }
    DbSet<EventLog> EventLogs { get; }
    DbSet<global::HRM.Domain.Entities.CompanySchema.MemberInGroup> MemberInGroups { get; }
}
