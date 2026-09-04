using HRM.Domain.Entities.InventorySchema;
using HRM.Domain.Entities.WeightMixingSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InventorySchema;

public sealed class WeighCostAllocationConfiguration : IEntityTypeConfiguration<WeighCostAllocation>
{
    public void Configure(EntityTypeBuilder<WeighCostAllocation> entity)
    {
        entity.ToTable("weigh_cost_allocations", "inv");
        entity.HasKey(x => x.AllocId);
        entity.Property(x => x.AllocId).HasColumnName("alloc_id");
        entity.Property(x => x.CompanyId).HasColumnName("company_id");
        entity.Property(x => x.EventId).HasColumnName("event_id");
        entity.Property(x => x.AmountCost).HasColumnName("amount_cost");
        entity.Property(x => x.CostStatus).HasColumnName("cost_status");
    }
}

public sealed class InventoryStockLedgerConfiguration : IEntityTypeConfiguration<InventoryStockLedger>
{
    public void Configure(EntityTypeBuilder<InventoryStockLedger> entity)
    {
        entity.ToTable("stock_ledger", "inv");
        entity.HasKey(x => x.LedgerId);
        entity.Property(x => x.LedgerId).HasColumnName("ledger_id");
        entity.Property(x => x.CompanyId).HasColumnName("company_id");
        entity.Property(x => x.TxnDate).HasColumnName("txn_date");
        entity.Property(x => x.ProductCode).HasColumnName("product_code");
        entity.Property(x => x.AvgCostAfter).HasColumnName("avg_cost_after");
    }
}

public sealed class WeighEventConfiguration : IEntityTypeConfiguration<WeighEvent>
{
    public void Configure(EntityTypeBuilder<WeighEvent> entity)
    {
        entity.ToTable("WeighEvent", "weightmixing");
        entity.HasKey(x => x.EventId);
        entity.Property(x => x.EventId).HasColumnName("EventId");
        entity.Property(x => x.VAMaterialId).HasColumnName("VAMaterialId");
    }
}

public sealed class WeighVAMaterialConfiguration : IEntityTypeConfiguration<WeighVAMaterial>
{
    public void Configure(EntityTypeBuilder<WeighVAMaterial> entity)
    {
        entity.ToTable("WeighVAMaterial", "weightmixing");
        entity.HasKey(x => x.VAMaterialId);
        entity.Property(x => x.VAMaterialId).HasColumnName("VAMaterialId");
        entity.Property(x => x.VAId).HasColumnName("VAId");
    }
}

public sealed class WeighVAConfiguration : IEntityTypeConfiguration<WeighVA>
{
    public void Configure(EntityTypeBuilder<WeighVA> entity)
    {
        entity.ToTable("WeighVA", "weightmixing");
        entity.HasKey(x => x.VAId);
        entity.Property(x => x.VAId).HasColumnName("VAId");
        entity.Property(x => x.OrderCode).HasColumnName("OrderCode");
    }
}
