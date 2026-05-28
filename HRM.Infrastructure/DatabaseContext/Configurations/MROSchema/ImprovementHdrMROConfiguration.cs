using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class ImprovementHdrMROConfiguration : IEntityTypeConfiguration<ImprovementHdrMRO>
{
    public void Configure(EntityTypeBuilder<ImprovementHdrMRO> entity)
    {
        entity.ToTable("improvement_hdr", "mro");
        entity.HasKey(x => x.ProposalId).HasName("pk_improvement_hdr");

        entity.Property(x => x.ProposalId).UseIdentityByDefaultColumn().HasColumnName("proposal_id");

        entity.Property(x => x.ProposalExternal).HasColumnName("proposal_external").HasColumnType("text").IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").HasColumnType("text").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.AreaExternalId).HasColumnName("area_externalid").HasColumnType("text");
        entity.Property(x => x.EquipmentExternalId).HasColumnName("equipment_externalid").HasColumnType("text");
        entity.Property(x => x.FactoryExternalId).HasColumnName("factory_externalid").HasColumnType("text");
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("timezone('utc', now())");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by");
        entity.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        entity.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        entity.Property(x => x.StartedAt).HasColumnName("started_at");
        entity.Property(x => x.StartedBy).HasColumnName("started_by");
        entity.Property(x => x.DoneAt).HasColumnName("done_at");
        entity.Property(x => x.DoneBy).HasColumnName("done_by");
        entity.Property(x => x.ClosedAt).HasColumnName("closed_at");
        entity.Property(x => x.ClosedBy).HasColumnName("closed_by");

        entity.HasIndex(x => new { x.FactoryExternalId, x.ProposalExternal })
              .IsUnique()
              .HasDatabaseName("ux_improvement_hdr_factory_proposal_external");

        entity.HasIndex(x => new { x.FactoryExternalId, x.Status, x.CreatedAt, x.ProposalId })
              .IsDescending(false, false, true, true)
              .HasDatabaseName("ix_improvement_hdr_factory_status_created_desc");

        entity.HasIndex(x => x.AreaExternalId).HasDatabaseName("ix_improvement_hdr_area_externalid");
        entity.HasIndex(x => x.EquipmentExternalId).HasDatabaseName("ix_improvement_hdr_equipment_externalid");

        entity.HasOne(x => x.CreatedByNav).WithMany()
              .HasForeignKey(x => x.CreatedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_improvement_hdr_created_by");

        entity.HasOne(x => x.ApprovedByNav).WithMany()
              .HasForeignKey(x => x.ApprovedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_improvement_hdr_approved_by");

        entity.HasOne(x => x.StartedByNav).WithMany()
              .HasForeignKey(x => x.StartedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_improvement_hdr_started_by");

        entity.HasOne(x => x.DoneByNav).WithMany()
              .HasForeignKey(x => x.DoneBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_improvement_hdr_done_by");

        entity.HasOne(x => x.ClosedByNav).WithMany()
              .HasForeignKey(x => x.ClosedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_improvement_hdr_closed_by");
    }
}
