using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class IncidentLineMROConfiguration : IEntityTypeConfiguration<IncidentLineMRO>
{
    public void Configure(EntityTypeBuilder<IncidentLineMRO> entity)
    {
        entity.ToTable("incidenthistory", "mro");
        entity.HasKey(x => x.IncidentLineId).HasName("pk_incidenthistory");

        entity.Property(x => x.IncidentLineId)
              .HasColumnName("incidenthistory_id")
              .UseIdentityByDefaultColumn();

        entity.Property(x => x.IncidentId).HasColumnName("incident_id").IsRequired();
        entity.Property(x => x.IncidentCode).HasColumnName("incident_code").HasColumnType("citext").IsRequired();
        entity.Property(x => x.Action).HasColumnName("action");
        entity.Property(x => x.Summary).HasColumnName("summary");
        entity.Property(x => x.StockOutId).HasColumnName("stock_out_id");
        entity.Property(x => x.WoRef).HasColumnName("wo_ref").HasColumnType("citext");
        entity.Property(x => x.PerformedBy).HasColumnName("performed_by");
        entity.Property(x => x.PerformedAt).HasColumnName("performed_at");
        entity.Property(x => x.RootCause).HasColumnName("root_cause");
        entity.Property(x => x.CorrectiveAction).HasColumnName("corrective_action");
        entity.Property(x => x.PreventiveAction).HasColumnName("preventive_action");
        entity.Property(x => x.DowntimeMinutes).HasColumnName("downtime_minutes");
        entity.Property(x => x.CostEstimate).HasColumnName("cost_estimate").HasPrecision(18, 2);

        entity.HasIndex(x => x.IncidentId).HasDatabaseName("ix_incidenthistory_incident");
        entity.HasIndex(x => new { x.IncidentId, x.PerformedAt, x.IncidentLineId })
              .IsDescending(false, true, true)
              .HasDatabaseName("ix_incidenthistory_incident_performed_desc");
        entity.HasIndex(x => x.StockOutId).HasDatabaseName("ix_incidenthistory_stockout");
        entity.HasIndex(x => x.PerformedBy).HasDatabaseName("ix_incidenthistory_performed_by");

        entity.HasOne(x => x.Incident)
              .WithMany()
              .HasForeignKey(x => x.IncidentId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_incidenthistory_incident");

        entity.HasOne(x => x.StockOut)
              .WithMany()
              .HasForeignKey(x => x.StockOutId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incidenthistory_stockout");

        entity.HasOne(x => x.PerformedByEmployee)
              .WithMany()
              .HasForeignKey(x => x.PerformedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incidenthistory_performed_by");
    }
}
