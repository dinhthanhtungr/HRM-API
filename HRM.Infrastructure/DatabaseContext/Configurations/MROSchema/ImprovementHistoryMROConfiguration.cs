using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;


public class ImprovementHistoryMROConfiguration : IEntityTypeConfiguration<ImprovementHistoryMRO>
{
    public void Configure(EntityTypeBuilder<ImprovementHistoryMRO> entity)
    {
        entity.ToTable("improvementhistory", "mro");
        entity.HasKey(x => x.HistoryId).HasName("pk_improvementhistory");

        entity.Property(x => x.HistoryId).UseIdentityByDefaultColumn().HasColumnName("history_id");

        entity.Property(x => x.ProposalId).HasColumnName("proposal_id").IsRequired();
        entity.Property(x => x.ProposalExternal).HasColumnName("proposal_external").HasColumnType("text").IsRequired();
        entity.Property(x => x.Action).HasColumnName("action").HasColumnType("text");
        entity.Property(x => x.Summary).HasColumnName("summary").HasColumnType("text");
        entity.Property(x => x.Minutes).HasColumnName("minutes").HasPrecision(10, 2);
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.WoRef).HasColumnName("wo_ref").HasColumnType("text");
        entity.Property(x => x.PerformedAt).HasColumnName("performed_at");
        entity.Property(x => x.PerformedBy).HasColumnName("performed_by");

        entity.HasIndex(x => new { x.ProposalId, x.PerformedAt, x.HistoryId })
              .IsDescending(false, true, true)
              .HasDatabaseName("ix_imprhist_proposal_performed_desc");

        entity.HasIndex(x => x.PerformedBy).HasDatabaseName("ix_imprhist_performed_by");

        entity.HasOne(x => x.PerformedByNav)
              .WithMany()
              .HasForeignKey(x => x.PerformedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_imprhist_performed_by");
    }
}
