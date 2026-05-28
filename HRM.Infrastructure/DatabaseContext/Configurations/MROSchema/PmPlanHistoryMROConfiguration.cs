using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class PmPlanHistoryMROConfiguration : IEntityTypeConfiguration<PmPlanHistoryMRO>
{
    public void Configure(EntityTypeBuilder<PmPlanHistoryMRO> entity)
    {
        entity.ToTable("pmplanhistory", "mro");
        entity.HasKey(x => x.HistId).HasName("pk_pmplanhistory");

        entity.Property(x => x.HistId).UseIdentityByDefaultColumn().HasColumnName("hist_id");
        entity.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        entity.Property(x => x.Action).HasColumnName("action").HasColumnType("text").IsRequired();
        entity.Property(x => x.Details).HasColumnName("details").HasColumnType("jsonb");
        entity.Property(x => x.PerformedBy).HasColumnName("performed_by");
        entity.Property(x => x.PerformedAt).HasColumnName("performed_at");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.WoRef).HasColumnName("wo_ref").HasColumnType("text");
        entity.Property(x => x.PlanExternalId).HasColumnName("plan_externalid").HasColumnType("text").IsRequired();
        entity.Property(x => x.MinutesSpent).HasColumnName("minutesspent").HasPrecision(10, 2);

        entity.HasIndex(x => new { x.PlanId, x.PerformedAt, x.HistId })
              .IsDescending(false, true, true)
              .HasDatabaseName("ix_pmplanhist_plan_performed_desc");

        entity.HasIndex(x => x.PerformedBy).HasDatabaseName("ix_pmplanhist_performed_by");

        entity.HasOne(x => x.Plan).WithMany()
              .HasForeignKey(x => x.PlanId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_pmplanhist_plan");
    }
}
