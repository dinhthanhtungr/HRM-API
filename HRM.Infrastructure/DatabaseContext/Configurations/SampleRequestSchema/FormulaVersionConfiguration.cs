using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class FormulaVersionConfiguration : IEntityTypeConfiguration<FormulaVersion>
{
    public void Configure(EntityTypeBuilder<FormulaVersion> entity)
    {
        entity.ToTable("FormulaVersions", "SampleRequests");

        entity.HasKey(x => x.FormulaVersionId).HasName("PK_FormulaVersions");
        entity.Property(x => x.FormulaVersionId).ValueGeneratedNever();
        entity.Property(x => x.FormulaId).IsRequired();
        entity.Property(x => x.VersionNo).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(32).HasDefaultValue("Draft").IsRequired();
        entity.Property(x => x.Note).HasColumnType("text");
        entity.Property(x => x.TotalPrice).HasPrecision(22, 6).IsRequired();
        entity.Property(x => x.ProductionPrice).HasPrecision(22, 6);
        entity.Property(x => x.PresidentPrice).HasPrecision(22, 6);
        entity.Property(x => x.ProfitMarginPrice).HasPrecision(22, 6);
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.ChangeReason).HasMaxLength(500);

        entity.HasIndex(x => new { x.FormulaId, x.VersionNo })
            .IsUnique()
            .HasDatabaseName("UX_FormulaVersions_FormulaId_VersionNo");
        entity.HasIndex(x => new { x.FormulaId, x.EffectiveFrom, x.EffectiveTo })
            .HasDatabaseName("IX_FormulaVersions_FormulaId_Period");
        entity.HasIndex(x => x.CreatedBy)
            .HasDatabaseName("IX_FormulaVersions_CreatedBy");
        entity.HasOne(x => x.Formula)
            .WithMany(x => x.FormulaVersions)
            .HasForeignKey(x => x.FormulaId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FormulaVersions_Formulas");

        entity.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_FormulaVersions_Employees_CreatedBy");

        entity.HasMany(x => x.Items)
            .WithOne(x => x.Version)
            .HasForeignKey(x => x.FormulaVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FormulaVersionItems_FormulaVersions");
    }
}
