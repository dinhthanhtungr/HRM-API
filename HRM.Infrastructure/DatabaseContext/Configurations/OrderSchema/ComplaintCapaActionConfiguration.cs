using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class ComplaintCapaActionConfiguration : IEntityTypeConfiguration<ComplaintCapaAction>
{
    public void Configure(EntityTypeBuilder<ComplaintCapaAction> entity)
    {
        entity.ToTable("ComplaintCapaActions", "Orders", table =>
        {
            table.HasCheckConstraint("CK_ComplaintCapaActions_SortOrder", "\"SortOrder\" >= 0");
        });

        entity.HasKey(x => x.ComplaintCapaActionId);

        entity.Property(x => x.ComplaintCapaActionId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.ActionType)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.Content)
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.PersonInChargeNameSnapshot)
            .HasColumnType("citext");

        entity.Property(x => x.Result)
            .HasColumnType("text");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => new { x.ComplaintReportId, x.ActionType, x.SortOrder })
            .HasDatabaseName("IX_ComplaintCapaActions_Report_Type_SortOrder");

        entity.HasOne(x => x.ComplaintReport)
            .WithMany(x => x.CapaActions)
            .HasForeignKey(x => x.ComplaintReportId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintCapaActions_ComplaintReport");

        entity.HasOne(x => x.PersonInCharge)
            .WithMany()
            .HasForeignKey(x => x.PersonInChargeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintCapaActions_PersonInCharge");
    }
}
