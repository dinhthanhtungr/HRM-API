using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class StockOutLineMROConfiguration : IEntityTypeConfiguration<StockOutLineMRO>
{
    public void Configure(EntityTypeBuilder<StockOutLineMRO> entity)
    {
        entity.ToTable("stock_out_line", "mro");
        entity.HasKey(x => x.LineId).HasName("pk_stock_out_line");

        entity.Property(x => x.LineId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("line_id");

        entity.Property(x => x.StockOutId).HasColumnName("stock_out_id").IsRequired();
        entity.Property(x => x.MaterialId).HasColumnName("material_id").IsRequired();
        entity.Property(x => x.MaterialExternalId).HasColumnName("material_externalid").HasColumnType("text");
        entity.Property(x => x.Qty).HasColumnName("qty").IsRequired();
        entity.Property(x => x.Uom).HasColumnName("uom").HasColumnType("text").IsRequired();
        entity.Property(x => x.SlotCode).HasColumnName("slot_code").HasColumnType("text");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => x.StockOutId).HasDatabaseName("ix_stock_out_line_stock_out_id");
        entity.HasIndex(x => x.MaterialId).HasDatabaseName("ix_stock_out_line_material_id");

        entity.HasOne(x => x.StockOut)
              .WithMany()
              .HasForeignKey(x => x.StockOutId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_stock_out_line_stock_out");

        entity.HasOne(x => x.Material)
              .WithMany()
              .HasForeignKey(x => x.MaterialId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_stock_out_line_material");
    }
}
