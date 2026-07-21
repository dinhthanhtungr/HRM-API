using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WarehouseSchema
{
    public class DepartmentTempStockConfiguration : IEntityTypeConfiguration<DepartmentTempStock>
    {
        public void Configure(EntityTypeBuilder<DepartmentTempStock> entity)
        {
            entity.HasKey(e => e.TempStockId).HasName("DepartmentTempStock_pkey");
            entity.ToTable("DepartmentTempStock", "Warehouse");

            entity.HasIndex(e => e.VoucherDetailId, "UQ_DepartmentTempStock_VoucherDetail").IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.PartId, e.ProductCode, e.LotNumber }, "ix_department_temp_stock_request");
            entity.HasIndex(e => new { e.CompanyId, e.PartId, e.Status, e.CostStatus }, "ix_department_temp_stock_status");

            entity.Property(e => e.TempStockId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.PartId).HasColumnName("PartID");
            entity.Property(e => e.AmountCost).HasPrecision(18, 2);
            entity.Property(e => e.AvgUnitCost).HasPrecision(18, 6);
            entity.Property(e => e.CostStatus).HasDefaultValueSql("'PENDING'::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.IssuedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.RemainingQtyKg).HasPrecision(18, 3).HasComputedColumnSql("((\"IssuedQtyKg\" - \"UsedQtyKg\") - \"ReturnedQtyKg\")", true);
            entity.Property(e => e.ReturnedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.Status).HasDefaultValueSql("'OPEN'::text");
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.UsedQtyKg).HasPrecision(18, 3);
        }
    }

    public class DepartmentTempStockLogConfiguration : IEntityTypeConfiguration<DepartmentTempStockLog>
    {
        public void Configure(EntityTypeBuilder<DepartmentTempStockLog> entity)
        {
            entity.HasKey(e => e.LogId).HasName("DepartmentTempStockLog_pkey");
            entity.ToTable("DepartmentTempStockLog", "Warehouse");

            entity.HasIndex(e => new { e.CompanyId, e.PartId, e.CreatedAt }, "ix_department_temp_stock_log_part_date")
                .IsDescending(false, false, true);
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.ProductCode, e.LotNumber, e.CreatedAt }, "ix_department_temp_stock_log_request");

            entity.Property(e => e.LogId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.PartId).HasColumnName("PartID");
            entity.Property(e => e.AmountCost).HasPrecision(18, 2);
            entity.Property(e => e.AvgUnitCost).HasPrecision(18, 6);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.DeltaKg).HasPrecision(18, 3);
            entity.Property(e => e.QtyAfter).HasPrecision(18, 3);
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
        }
    }

    public class ImportRequestFlowLogConfiguration : IEntityTypeConfiguration<ImportRequestFlowLog>
    {
        public void Configure(EntityTypeBuilder<ImportRequestFlowLog> entity)
        {
            entity.HasKey(e => e.FlowLogId).HasName("ImportRequestFlowLog_pkey");
            entity.ToTable("ImportRequestFlowLog", "Warehouse");

            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.RequestDetailId, e.LogStatus }, "IX_ImportRequestFlowLog_ApplyLookup");
            entity.HasIndex(e => new { e.CompanyId, e.LogStatus, e.CreatedAt }, "IX_ImportRequestFlowLog_Pending")
                .HasFilter("(\"LogStatus\" = ANY (ARRAY['PENDING'::text, 'PARTIAL'::text]))");
            entity.HasIndex(e => new { e.CompanyId, e.ProductCode, e.LotNumber }, "IX_ImportRequestFlowLog_Product");
            entity.HasIndex(e => new { e.CompanyId, e.SourceKind, e.SourceRowId }, "IX_ImportRequestFlowLog_Source");
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.RequestDetailId }, "UX_ImportRequestFlowLog_RequestDetail")
                .IsUnique()
                .HasFilter("(\"RequestDetailId\" IS NOT NULL)");
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.LineNo }, "UX_ImportRequestFlowLog_Request_Line").IsUnique();

            entity.Property(e => e.FlowLogId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.AppliedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.AppliedBagNumber).HasDefaultValue(0);
            entity.Property(e => e.AppliedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.BagNumber).HasDefaultValue(0);
            entity.Property(e => e.CancelledAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ConfirmedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.LogStatus).HasDefaultValueSql("'PENDING'::text");
            entity.Property(e => e.QtyKg).HasPrecision(18, 3);
            entity.Property(e => e.SourceAvailableQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
        }
    }

    public class OperationMaterialBufferConfiguration : IEntityTypeConfiguration<OperationMaterialBuffer>
    {
        public void Configure(EntityTypeBuilder<OperationMaterialBuffer> entity)
        {
            entity.HasKey(e => e.BufferId).HasName("OperationMaterialBuffer_pkey");
            entity.ToTable("OperationMaterialBuffer", "Warehouse");

            entity.HasIndex(e => e.VoucherDetailId, "UQ_OperationMaterialBuffer_VoucherDetail").IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.ProductCode, e.LotNumber }, "ix_operation_material_buffer_request");
            entity.HasIndex(e => new { e.CompanyId, e.Status, e.CostStatus }, "ix_operation_material_buffer_status");

            entity.Property(e => e.BufferId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.AmountCost).HasPrecision(18, 2);
            entity.Property(e => e.AvgUnitCost).HasPrecision(18, 6);
            entity.Property(e => e.CostStatus).HasDefaultValueSql("'PENDING'::text");
            entity.Property(e => e.CostedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.IssuedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.RemainingQtyKg).HasPrecision(18, 3).HasComputedColumnSql("((\"IssuedQtyKg\" - \"UsedQtyKg\") - \"ReturnedQtyKg\")", true);
            entity.Property(e => e.ReturnedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.Status).HasDefaultValueSql("'OPEN'::text");
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.UsedQtyKg).HasPrecision(18, 3);
        }
    }

    public class OperationMaterialRequestLinkConfiguration : IEntityTypeConfiguration<OperationMaterialRequestLink>
    {
        public void Configure(EntityTypeBuilder<OperationMaterialRequestLink> entity)
        {
            entity.HasKey(e => e.LinkId).HasName("OperationMaterialRequestLink_pkey");
            entity.ToTable("OperationMaterialRequestLink", "Warehouse");

            entity.HasIndex(e => e.RequestId, "IX_OperationMaterialRequestLink_RequestId");

            entity.Property(e => e.LinkId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.RequestedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
        }
    }

    public class PicklistDraftConfiguration : IEntityTypeConfiguration<PicklistDraft>
    {
        public void Configure(EntityTypeBuilder<PicklistDraft> entity)
        {
            entity.HasKey(e => e.DraftId).HasName("PicklistDraft_pkey");
            entity.ToTable("PicklistDraft", "Warehouse");

            entity.HasIndex(e => e.Status, "ix_picklistdraft_status");
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.Kind }, "ux_picklistdraft_company_req_kind_legacy_null_round")
                .IsUnique()
                .HasFilter("(\"roundNo\" IS NULL)");
            entity.HasIndex(e => new { e.CompanyId, e.RequestCode, e.Kind, e.RoundNo }, "ux_picklistdraft_company_req_kind_round")
                .IsUnique()
                .HasFilter("(\"roundNo\" IS NOT NULL)");

            entity.Property(e => e.DraftId).HasColumnName("draftId");
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.RequestCode).HasColumnName("requestCode");
            entity.Property(e => e.Kind).HasColumnName("kind");
            entity.Property(e => e.ReqType).HasColumnName("reqType");
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValueSql("'DRAFT'::text");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt").HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");
            entity.Property(e => e.FrozenAt).HasColumnName("frozenAt").HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.ReceivedAt).HasColumnName("receivedAt").HasColumnType("timestamp without time zone");
            entity.Property(e => e.ReceivedBy).HasColumnName("receivedBy");
            entity.Property(e => e.RoundNo).HasColumnName("roundNo").HasComment("Số đợt xuất cho cùng một phiếu CT. NULL để tương thích draft cũ.");
            entity.Property(e => e.ExportedAt).HasColumnName("exportedAt").HasComment("Thời điểm kho xác nhận xuất đợt này. Nullable để không ảnh hưởng dữ liệu cũ.").HasColumnType("timestamp without time zone");
            entity.Property(e => e.ExportedBy).HasColumnName("exportedBy").HasComment("Người kho xác nhận xuất đợt này. Nullable để không ảnh hưởng dữ liệu cũ.");
        }
    }

    public class PicklistDraftLineConfiguration : IEntityTypeConfiguration<PicklistDraftLine>
    {
        public void Configure(EntityTypeBuilder<PicklistDraftLine> entity)
        {
            entity.HasKey(e => e.LineId).HasName("PicklistDraftLine_pkey");
            entity.ToTable("PicklistDraftLine", "Warehouse");

            entity.HasIndex(e => new { e.DraftId, e.ShelfStockId }, "ix_picklistdraftline_draft_shelf");
            entity.HasIndex(e => new { e.DraftId, e.LineNo }, "ux_picklistdraftline_draft_lineno").IsUnique();

            entity.Property(e => e.LineId).HasColumnName("lineId");
            entity.Property(e => e.DraftId).HasColumnName("draftId");
            entity.Property(e => e.LineNo).HasColumnName("lineNo");
            entity.Property(e => e.ShelfStockId).HasColumnName("shelfStockId");
            entity.Property(e => e.SlotCode).HasColumnName("slotCode");
            entity.Property(e => e.ProductCode).HasColumnName("productCode");
            entity.Property(e => e.ProductName).HasColumnName("productName");
            entity.Property(e => e.LotNumber).HasColumnName("lotNumber");
            entity.Property(e => e.QtyKg).HasColumnName("qtyKg").HasPrecision(18, 3);
            entity.Property(e => e.Bags).HasColumnName("bags");
            entity.Property(e => e.StdBagKg).HasColumnName("stdBagKg").HasPrecision(18, 3);
            entity.Property(e => e.LotKey).HasColumnName("lotKey").HasColumnType("citext");
            entity.Property(e => e.RawLineNo).HasColumnName("rawLineNo");
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");

            entity.HasOne(d => d.Draft)
                .WithMany(p => p.PicklistDraftLines)
                .HasForeignKey(d => d.DraftId)
                .HasConstraintName("PicklistDraftLine_draftId_fkey");
        }
    }

    public class ProductionOutputReceiptLedgerConfiguration : IEntityTypeConfiguration<ProductionOutputReceiptLedger>
    {
        public void Configure(EntityTypeBuilder<ProductionOutputReceiptLedger> entity)
        {
            entity.HasKey(e => e.LedgerId).HasName("ProductionOutputReceiptLedger_pkey");
            entity.ToTable("ProductionOutputReceiptLedger", "Warehouse");

            entity.HasIndex(e => new { e.CompanyId, e.VoucherDetailId }, "UX_ProductionOutputReceiptLedger_VoucherDetail").IsUnique();

            entity.Property(e => e.LedgerId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.QtyKg).HasPrecision(18, 3);
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");

            entity.HasOne(d => d.ProductionOutput)
                .WithMany(p => p.ProductionOutputReceiptLedgers)
                .HasForeignKey(d => d.ProductionOutputId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOutputReceiptLedger_Source");
        }
    }

    public class ProductionOutputReceiptSourceConfiguration : IEntityTypeConfiguration<ProductionOutputReceiptSource>
    {
        public void Configure(EntityTypeBuilder<ProductionOutputReceiptSource> entity)
        {
            entity.HasKey(e => e.ProductionOutputId).HasName("ProductionOutputReceiptSource_pkey");
            entity.ToTable("ProductionOutputReceiptSource", "Warehouse");

            entity.HasIndex(e => new { e.CompanyId, e.LinkStatus, e.CostStatus, e.Status }, "IX_ProductionOutputReceiptSource_LinkCost");
            entity.HasIndex(e => new { e.CompanyId, e.MfgExternalId, e.VaExternalId, e.Status }, "IX_ProductionOutputReceiptSource_MfgVa");
            entity.HasIndex(e => new { e.CompanyId, e.ProductCode, e.ItemStockType, e.Status }, "IX_ProductionOutputReceiptSource_Open_Product");
            entity.HasIndex(e => new { e.CompanyId, e.ShiftReportForAllId, e.ShiftReportDetailForAllId }, "IX_ProductionOutputReceiptSource_ShiftReport");
            entity.HasIndex(e => new { e.CompanyId, e.SourceKey }, "UX_ProductionOutputReceiptSource_SourceKey").IsUnique();

            entity.Property(e => e.ProductionOutputId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CostStatus).HasDefaultValueSql("'PENDING'::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.DefectQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.GoodQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.ImportedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.IsOpenEnded).HasDefaultValue(true);
            entity.Property(e => e.ItemStockType).HasDefaultValue(1);
            entity.Property(e => e.LineNo).HasDefaultValue(1);
            entity.Property(e => e.LinkStatus).HasDefaultValueSql("'UNLINKED'::text");
            entity.Property(e => e.MaterialIssueFoundAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.OverrideAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.PlannedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.ProducedQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.ReceiptCount).HasDefaultValue(0);
            entity.Property(e => e.RemainingQtyKg)
                .HasPrecision(18, 3)
                .HasComputedColumnSql("\nCASE\n    WHEN (\"Status\" <> 'OPEN'::text) THEN (0)::numeric\n    WHEN (\"IsOpenEnded\" = true) THEN 999999999.999\n    ELSE GREATEST((\"ProducedQtyKg\" - \"ImportedQtyKg\"), (0)::numeric)\nEND", true);
            entity.Property(e => e.ScrapQtyKg).HasPrecision(18, 3);
            entity.Property(e => e.SourceKind).HasDefaultValueSql("'SHIFT_REPORT'::text");
            entity.Property(e => e.Status).HasDefaultValueSql("'OPEN'::text");
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
        }
    }

    public class TempStockIssueLedgerConfiguration : IEntityTypeConfiguration<TempStockIssueLedger>
    {
        public void Configure(EntityTypeBuilder<TempStockIssueLedger> entity)
        {
            entity.HasKey(e => e.LedgerId).HasName("TempStockIssueLedger_pkey");
            entity.ToTable("TempStockIssueLedger", "Warehouse");

            entity.HasIndex(e => new { e.CompanyId, e.SourceKind, e.SourceKey }, "UX_TempStockIssueLedger_Source").IsUnique();

            entity.Property(e => e.LedgerId).HasColumnName("ledgerId").UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.SourceKind).HasColumnName("sourceKind");
            entity.Property(e => e.SourceKey).HasColumnName("sourceKey");
            entity.Property(e => e.RequestCode).HasColumnName("requestCode");
            entity.Property(e => e.RequestDetailId).HasColumnName("requestDetailId");
            entity.Property(e => e.VaCode).HasColumnName("vaCode");
            entity.Property(e => e.ProductCode).HasColumnName("productCode");
            entity.Property(e => e.LotNo).HasColumnName("lotNo");
            entity.Property(e => e.QtyKg).HasColumnName("qtyKg").HasPrecision(18, 3);
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt").HasDefaultValueSql("now()").HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
        }
    }

    public class WarehouseStockFifoLayerConfiguration : IEntityTypeConfiguration<WarehouseStockFifoLayer>
    {
        public void Configure(EntityTypeBuilder<WarehouseStockFifoLayer> entity)
        {
            entity.HasKey(e => e.LayerId).HasName("WarehouseStockFifoLayer_pkey");
            entity.ToTable("WarehouseStockFifoLayer", "Warehouse");

            entity.HasIndex(e => e.InLedgerId, "ux_wh_fifo_layer_inledger").IsUnique();

            entity.Property(e => e.LayerId).HasColumnName("layerId").UseIdentityAlwaysColumn();
            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.ShelfStockId).HasColumnName("shelfStockId");
            entity.Property(e => e.SlotId).HasColumnName("slotId");
            entity.Property(e => e.ProductCode).HasColumnName("productCode");
            entity.Property(e => e.LotNumber).HasColumnName("lotNumber").HasDefaultValueSql("''::text");
            entity.Property(e => e.ExpiryDate).HasColumnName("expiryDate");
            entity.Property(e => e.StockType).HasColumnName("stockType");
            entity.Property(e => e.InLedgerId).HasColumnName("inLedgerId");
            entity.Property(e => e.InAt).HasColumnName("inAt").HasColumnType("timestamp without time zone");
            entity.Property(e => e.QtyInKg).HasColumnName("qtyInKg").HasPrecision(18, 3);
            entity.Property(e => e.QtyUsedKg).HasColumnName("qtyUsedKg").HasPrecision(18, 3);
            entity.Property(e => e.RemainingKg).HasColumnName("remainingKg").HasPrecision(18, 3).HasComputedColumnSql("round((\"qtyInKg\" - \"qtyUsedKg\"), 3)", true);
            entity.Property(e => e.UnitName).HasDefaultValueSql("'Kg'::text");
        }
    }
}
