using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema;

public sealed class MaterialProcessingFormulaConfiguration : IEntityTypeConfiguration<MaterialProcessingFormula>
{
    public void Configure(EntityTypeBuilder<MaterialProcessingFormula> entity)
    {
        entity.ToTable("MaterialProcessingFormulas", "Material");
        entity.HasKey(x => x.MaterialProcessingFormulaId);
        entity.HasIndex(x => new { x.CompanyId, x.ExternalId }).IsUnique();
        entity.Property(x => x.ExternalId).HasMaxLength(64);
        entity.Property(x => x.ProcessingType).HasMaxLength(1);
        entity.Property(x => x.Status).HasMaxLength(32);
        entity.Property(x => x.TotalQuantityRequest).HasPrecision(18, 6);
        entity.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.Note).HasMaxLength(2000);
        entity.Property(x => x.QcCheck).HasMaxLength(2000);
        entity.Property(x => x.Revision).IsConcurrencyToken();
    }
}

public sealed class MaterialProcessingFormulaDetailConfiguration : IEntityTypeConfiguration<MaterialProcessingFormulaDetail>
{
    public void Configure(EntityTypeBuilder<MaterialProcessingFormulaDetail> entity)
    {
        entity.ToTable("MaterialProcessingFormulaDetails", "Material");
        entity.HasKey(x => x.MaterialProcessingFormulaDetailId);
        entity.HasIndex(x => new { x.MaterialProcessingFormulaId, x.LineNo })
            .HasDatabaseName("IX_MaterialProcessingFormulaDetails_Formula_LineNo").IsUnique();
        entity.Property(x => x.QuantityKg).HasPrecision(18, 6);
        entity.Property(x => x.LotNo).HasMaxLength(128);
        entity.Property(x => x.itemType).HasDefaultValue(HRM.Domain.Enums.Formulas.ItemType.Material);
        entity.Property(x => x.MaterialCodeSnapshot).HasMaxLength(256);
        entity.Property(x => x.MaterialNameSnapshot).HasMaxLength(1000);
        entity.HasOne(x => x.Formula).WithMany(x => x.Details).HasForeignKey(x => x.MaterialProcessingFormulaId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
