using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class ComplaintReportLineLotConfiguration : IEntityTypeConfiguration<ComplaintReportLineLot>
{
    public void Configure(EntityTypeBuilder<ComplaintReportLineLot> entity)
    {
        entity.ToTable("ComplaintReportLineLots", "Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_ComplaintReportLineLots_DeliveredQuantity_NonNegative",
                "\"DeliveredQuantitySnapshot\" >= 0");
            table.HasCheckConstraint(
                "CK_ComplaintReportLineLots_ComplaintQuantity",
                "\"ComplaintQuantity\" > 0 AND \"ComplaintQuantity\" <= \"DeliveredQuantitySnapshot\"");
        });

        entity.HasKey(x => x.ComplaintReportLineLotId);

        entity.Property(x => x.ComplaintReportLineLotId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.LotNoSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.DeliveredQuantitySnapshot)
            .HasPrecision(18, 3);

        entity.Property(x => x.ComplaintQuantity)
            .HasPrecision(18, 3);

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => x.ComplaintReportLineId)
            .HasDatabaseName("IX_ComplaintReportLineLots_ComplaintReportLineId");

        entity.HasIndex(x => x.SourceDeliveryOrderDetailId)
            .HasDatabaseName("IX_ComplaintReportLineLots_SourceDeliveryOrderDetailId");

        entity.HasIndex(x => x.SourceLotConsumptionId)
            .HasDatabaseName("IX_ComplaintReportLineLots_SourceLotConsumptionId");

        entity.HasIndex(x => new { x.ComplaintReportLineId, x.SourceDeliveryOrderDetailId, x.LotNoSnapshot })
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE")
            .HasDatabaseName("UX_ComplaintReportLineLots_Line_DeliveryDetail_Lot_Active");

        entity.HasOne(x => x.ComplaintReportLine)
            .WithMany(x => x.Lots)
            .HasForeignKey(x => x.ComplaintReportLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLineLots_ComplaintReportLine");

        entity.HasOne(x => x.SourceDeliveryOrderDetail)
            .WithMany()
            .HasForeignKey(x => x.SourceDeliveryOrderDetailId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLineLots_SourceDeliveryOrderDetail");

        entity.HasOne(x => x.SourceLotConsumption)
            .WithMany()
            .HasForeignKey(x => x.SourceLotConsumptionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportLineLots_SourceLotConsumption");
    }
}
