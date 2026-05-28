using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class IncidentHeaderMROConfiguration : IEntityTypeConfiguration<IncidentHeaderMRO>
{
    public void Configure(EntityTypeBuilder<IncidentHeaderMRO> entity)
    {
        entity.ToTable("incident_hdr", "mro");

        entity.HasKey(x => x.IncidentId).HasName("pk_incident_hdr");

        entity.Property(x => x.IncidentId)
              .HasColumnName("incident_id")
              .UseIdentityByDefaultColumn();

        entity.Property(x => x.IncidentCode).HasColumnName("incident_code").HasColumnType("text").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").HasColumnType("text").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");

        entity.Property(x => x.EquipmentId).HasColumnName("equipment_id");
        entity.Property(x => x.EquipmentCode).HasColumnName("equipment_code").HasColumnType("text");
        entity.Property(x => x.AreaId).HasColumnName("area_id");
        entity.Property(x => x.AreaCode).HasColumnName("area_code").HasColumnType("text");
        entity.Property(x => x.CompanyId).HasColumnName("company_id");
        entity.Property(x => x.RolePrefix).HasColumnName("role_prefix").HasColumnType("text");

        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by");
        entity.Property(x => x.ExecAt).HasColumnName("exec_at");
        entity.Property(x => x.ExecBy).HasColumnName("exec_by");
        entity.Property(x => x.DoneAt).HasColumnName("done_at");
        entity.Property(x => x.DoneBy).HasColumnName("done_by");
        entity.Property(x => x.ClosedAt).HasColumnName("closed_at");
        entity.Property(x => x.ClosedBy).HasColumnName("closed_by");

        entity.Property(x => x.WaitMin).HasColumnName("wait_min");
        entity.Property(x => x.RepairMin).HasColumnName("repair_min");
        entity.Property(x => x.TotalMin).HasColumnName("total_min");

        entity.HasIndex(x => new { x.CompanyId, x.IncidentCode })
              .HasDatabaseName("ux_incident_hdr_company_code")
              .IsUnique();

        entity.HasIndex(x => x.Status).HasDatabaseName("ix_incident_hdr_status");
        entity.HasIndex(x => new { x.EquipmentId, x.EquipmentCode }).HasDatabaseName("ix_incident_hdr_equipment");
        entity.HasIndex(x => x.AreaId).HasDatabaseName("ix_incident_hdr_area");
        entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_incident_hdr_created_at");

        entity.HasOne(x => x.Company)
              .WithMany()
              .HasForeignKey(x => x.CompanyId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_company_id");

        entity.HasOne(x => x.Area)
              .WithMany()
              .HasForeignKey(x => x.AreaId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_area_id");

        entity.HasOne(x => x.Equipment)
              .WithMany()
              .HasForeignKey(x => x.EquipmentId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_equipment_id");

        entity.HasOne(x => x.CreatedByEmployee)
              .WithMany()
              .HasForeignKey(x => x.CreatedBy)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_created_by");

        entity.HasOne(x => x.ExecByEmployee)
              .WithMany()
              .HasForeignKey(x => x.ExecBy)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_exec_by");

        entity.HasOne(x => x.DoneByEmployee)
              .WithMany()
              .HasForeignKey(x => x.DoneBy)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_done_by");

        entity.HasOne(x => x.ClosedByEmployee)
              .WithMany()
              .HasForeignKey(x => x.ClosedBy)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_incident_hdr_closed_by");
    }
}
