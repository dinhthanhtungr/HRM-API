using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class FormulaVersionItemConfiguration : IEntityTypeConfiguration<FormulaVersionItem>
{
    public void Configure(EntityTypeBuilder<FormulaVersionItem> entity)
    {
        entity.ToTable("FormulaVersionItems", "SampleRequests");

        entity.HasKey(x => x.FormulaVersionItemId).HasName("PK_FormulaVersionItems");
        entity.Property(x => x.FormulaVersionItemId).ValueGeneratedNever();
        entity.Property(x => x.FormulaVersionId).IsRequired();
        entity.Property(x => x.LineNo).IsRequired();
        entity.Property(x => x.ItemType).HasConversion<int>().IsRequired();
        entity.Property(x => x.CategoryId).IsRequired();
        entity.Property(x => x.Quantity).HasPrecision(12, 10).IsRequired();
        entity.Property(x => x.UnitPrice).HasPrecision(22, 6).IsRequired();
        entity.Property(x => x.TotalPrice).HasPrecision(22, 6).IsRequired();
        entity.Property(x => x.Unit).HasMaxLength(50);
        entity.Property(x => x.MaterialExternalIdSnapshot).HasMaxLength(100);
        entity.Property(x => x.MaterialNameSnapshot).HasMaxLength(500);

        entity.HasIndex(x => new { x.FormulaVersionId, x.LineNo })
            .IsUnique()
            .HasDatabaseName("UX_FormulaVersionItems_VersionId_LineNo");
        entity.HasIndex(x => x.MaterialId)
            .HasDatabaseName("IX_FormulaVersionItems_MaterialId");
        entity.HasIndex(x => x.ProductId)
            .HasDatabaseName("IX_FormulaVersionItems_ProductId");

        entity.HasOne(x => x.Version)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.FormulaVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FormulaVersionItems_FormulaVersions");
        entity.HasOne(x => x.Material)
            .WithMany()
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormulaVersionItems_Materials");
        entity.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormulaVersionItems_Products");
        entity.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormulaVersionItems_Categories");
    }
}
