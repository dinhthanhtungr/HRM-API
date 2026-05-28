using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class StockOutHeaderMROConfiguration : IEntityTypeConfiguration<StockOutHeaderMRO>
{
    public void Configure(EntityTypeBuilder<StockOutHeaderMRO> entity)
    {
        entity.ToTable("stock_out_hdr", "mro");
        entity.HasKey(x => x.StockOutId).HasName("pk_stock_out_hdr");

        entity.Property(x => x.StockOutId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("stock_out_id");

        entity.Property(x => x.StockOutCode).HasColumnName("stock_out_code").HasColumnType("text").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft").IsRequired();
        entity.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.FactoryId).HasColumnName("factory_id").IsRequired();
        entity.Property(x => x.FactoryCode).HasColumnName("factory_code").HasColumnType("text");
        entity.Property(x => x.SourceType).HasColumnName("source_type").HasColumnType("text");
        entity.Property(x => x.SourceId).HasColumnName("source_id");
        entity.Property(x => x.SourceCode).HasColumnName("source_code").HasColumnType("text");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.PostedAt).HasColumnName("posted_at");
        entity.Property(x => x.PostedBy).HasColumnName("posted_by");

        entity.HasIndex(x => new { x.FactoryId, x.StockOutCode })
              .IsUnique()
              .HasDatabaseName("ux_stock_out_code_per_factory");

        entity.HasIndex(x => new { x.FactoryId, x.Status, x.CreatedAt, x.StockOutId })
              .IsDescending(false, false, true, true)
              .HasDatabaseName("ix_stock_out_hdr_factory_status_created_desc");

        entity.HasOne(x => x.Factory).WithMany()
              .HasForeignKey(x => x.FactoryId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_stock_out_hdr_factory");

        entity.HasOne(x => x.CreatedByNav).WithMany()
              .HasForeignKey(x => x.CreatedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_stock_out_hdr_created_by");

        entity.HasOne(x => x.PostedByNav).WithMany()
              .HasForeignKey(x => x.PostedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_stock_out_hdr_posted_by");

        entity.HasOne(x => x.IncidentHeaderMRO).WithMany(a => a.StockOutHeaderMRO)
              .HasForeignKey(x => x.SourceId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_stock_out_hdr_incident ");
    }
}
