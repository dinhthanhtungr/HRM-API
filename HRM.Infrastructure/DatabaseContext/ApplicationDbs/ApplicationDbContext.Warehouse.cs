using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.InventorySchema;
using HRM.Domain.Entities.WeightMixingSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<DepartmentTempStock> DepartmentTempStocks { get; set; } = default!;
        public virtual DbSet<DepartmentTempStockLog> DepartmentTempStockLogs { get; set; } = default!;
        public virtual DbSet<ImportRequestFlowLog> ImportRequestFlowLogs { get; set; } = default!;
        public virtual DbSet<OperationMaterialBuffer> OperationMaterialBuffers { get; set; } = default!;
        public virtual DbSet<OperationMaterialRequestLink> OperationMaterialRequestLinks { get; set; } = default!;
        public virtual DbSet<PicklistDraft> PicklistDrafts { get; set; } = default!;
        public virtual DbSet<PicklistDraftLine> PicklistDraftLines { get; set; } = default!;
        public virtual DbSet<ProductionOutputReceiptLedger> ProductionOutputReceiptLedgers { get; set; } = default!;
        public virtual DbSet<ProductionOutputReceiptSource> ProductionOutputReceiptSources { get; set; } = default!;
        public virtual DbSet<TempStockIssueLedger> TempStockIssueLedgers { get; set; } = default!;
        public virtual DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; set; } = default!;
        public virtual DbSet<WarehouseStockFifoLayer> WarehouseStockFifoLayers { get; set; } = default!;
        public virtual DbSet<WarehouseTempStock> WarehouseTempStocks { get; set; } = default!;
        public virtual DbSet<WarehouseRequest> WarehouseRequests { get; set; } = default!;
        public virtual DbSet<WarehouseRequestDetail> WarehouseRequestDetails { get; set; } = default!;
        public virtual DbSet<WarehouseShelves> WarehouseShelves { get; set; } = default!;
        public virtual DbSet<WarehouseShelfLedger> WarehouseShelfLedgers { get; set; } = default!;
        public virtual DbSet<UsagePurpose> UsagePurposes { get; set; } = default!;
        public virtual DbSet<WarehouseVoucher> WarehouseVouchers { get; set; } = default!;
        public virtual DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; set; } = default!;
        public virtual DbSet<WeighCostAllocation> WeighCostAllocations { get; set; } = default!;
        public virtual DbSet<InventoryStockLedger> InventoryStockLedgers { get; set; } = default!;
        public virtual DbSet<WeighEvent> WeighEvents { get; set; } = default!;
        public virtual DbSet<WeighVAMaterial> WeighVAMaterials { get; set; } = default!;
        public virtual DbSet<WeighVA> WeighVAs { get; set; } = default!;

    }
}
