using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class ComplaintReportLineConfiguration : IEntityTypeConfiguration<ComplaintReportLine>
{
    public void Configure(EntityTypeBuilder<ComplaintReportLine> entity)
    {
        entity.ToTable("ComplaintReportLines", "Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_ComplaintReportLines_ComplaintQuantity_Positive",
                "\"ComplaintQuantity\" > 0");
            table.HasCheckConstraint(
                "CK_ComplaintReportLines_ApprovedReplacementQuantity",
                "\"ApprovedReplacementQuantity\" IS NULL OR (\"ApprovedReplacementQuantity\" > 0 AND \"ApprovedReplacementQuantity\" <= \"ComplaintQuantity\")");
        });

        entity.HasKey(x => x.ComplaintReportLineId);

        entity.Property(x => x.ComplaintReportLineId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.ProductExternalIdSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.ProductNameSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.FormulaExternalIdSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.ManufacturingFormulaExternalIdSnapshot)
            .HasColumnType("citext");

        entity.Property(x => x.ComplaintQuantity)
            .HasPrecision(18, 3);

        entity.Property(x => x.ApprovedReplacementQuantity)
            .HasPrecision(18, 3);

        entity.Property(x => x.IssueType)
            .HasMaxLength(100);

        entity.Property(x => x.Severity)
            .HasMaxLength(50);

        entity.Property(x => x.Description)
            .HasColumnType("text");

        entity.Property(x => x.ResolutionNote)
            .HasColumnType("text");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => x.ComplaintReportId)
            .HasDatabaseName("IX_ComplaintReportLines_ComplaintReportId");

        entity.HasIndex(x => x.SourceMerchandiseOrderDetailId)
            .HasDatabaseName("IX_ComplaintReportLines_SourceMerchandiseOrderDetailId");

        entity.HasIndex(x => x.SourceMfgProductionOrderId)
            .HasDatabaseName("IX_ComplaintReportLines_SourceMfgProductionOrderId");

        entity.HasIndex(x => x.ManufacturingFormulaId)
            .HasDatabaseName("IX_ComplaintReportLines_ManufacturingFormulaId");

        entity.HasOne(x => x.ComplaintReport)
            .WithMany(x => x.ComplaintReportLines)
            .HasForeignKey(x => x.ComplaintReportId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_ComplaintReport");

        entity.HasOne(x => x.SourceMerchandiseOrderDetail)
            .WithMany(x => x.ComplaintReportLines)
            .HasForeignKey(x => x.SourceMerchandiseOrderDetailId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_SourceMerchandiseOrderDetail");

        entity.HasOne(x => x.SourceMfgProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.SourceMfgProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_SourceMfgProductionOrder");

        entity.HasOne(x => x.Product)
            .WithMany(x => x.ComplaintReportLines)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_Product");

        entity.HasOne(x => x.Formula)
            .WithMany(x => x.ComplaintReportLines)
            .HasForeignKey(x => x.FormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_Formula");

        entity.HasOne(x => x.ManufacturingFormula)
            .WithMany()
            .HasForeignKey(x => x.ManufacturingFormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLines_ManufacturingFormula");
    }
}
