using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class ComplaintReportApprovalConfiguration : IEntityTypeConfiguration<ComplaintReportApproval>
{
    public void Configure(EntityTypeBuilder<ComplaintReportApproval> entity)
    {
        entity.ToTable("ComplaintReportApprovals", "Orders");

        entity.HasKey(x => x.ComplaintReportApprovalId);

        entity.Property(x => x.ComplaintReportApprovalId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.Stage)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.Decision)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.ActorNameSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.Comment)
            .HasColumnType("text");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => new { x.ComplaintReportId, x.Stage, x.DecidedAt })
            .HasDatabaseName("IX_ComplaintReportApprovals_Report_Stage_DecidedAt");

        entity.HasOne(x => x.ComplaintReport)
            .WithMany(x => x.Approvals)
            .HasForeignKey(x => x.ComplaintReportId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportApprovals_ComplaintReport");

        entity.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReportApprovals_Actor");
    }
}
