using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class ComplaintReportConfiguration : IEntityTypeConfiguration<ComplaintReport>
{
    public void Configure(EntityTypeBuilder<ComplaintReport> entity)
    {
        entity.ToTable("ComplaintReports", "Orders");

        entity.HasKey(x => x.ComplaintReportId);

        entity.Property(x => x.ComplaintReportId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.ExternalId)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.CustomerExternalIdSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.CustomerNameSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.CreatedByNameSnapshot)
            .HasColumnType("citext")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(ComplaintReportStatus.Draft)
            .IsRequired();

        entity.Property(x => x.RequestedResolutionType)
            .HasConversion<int>();

        entity.Property(x => x.ResolutionType)
            .HasConversion<int>();

        entity.Property(x => x.RelatedStandards)
            .HasConversion<int>()
            .HasDefaultValue(ComplaintRelatedStandard.None)
            .IsRequired();

        entity.Property(x => x.RelatedScopes)
            .HasConversion<int>()
            .HasDefaultValue(ComplaintRelatedScope.CustomerClaim)
            .IsRequired();

        entity.Property(x => x.IssuePartNameSnapshot)
            .HasColumnType("citext");

        entity.Property(x => x.OtherRelatedStandard)
            .HasMaxLength(250);

        entity.Property(x => x.Summary)
            .HasColumnType("text");

        entity.Property(x => x.DocumentRequirement)
            .HasColumnType("text");

        entity.Property(x => x.NonConformityDescription)
            .HasColumnType("text");

        entity.Property(x => x.RootCause)
            .HasColumnType("text");

        entity.Property(x => x.InterestedPartyComment)
            .HasColumnType("text");

        entity.Property(x => x.CausingPartySnapshot)
            .HasColumnType("citext");

        entity.Property(x => x.ResolutionNote)
            .HasColumnType("text");

        entity.Property(x => x.RiskReviewComment)
            .HasColumnType("text");

        entity.Property(x => x.EffectivenessPersonInChargeNameSnapshot)
            .HasColumnType("citext");

        entity.Property(x => x.EffectivenessConclusion)
            .HasConversion<int>();

        entity.Property(x => x.EffectivenessComment)
            .HasColumnType("text");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => new { x.CompanyId, x.ExternalId })
            .IsUnique()
            .HasDatabaseName("UX_ComplaintReports_Company_ExternalId");

        entity.HasIndex(x => new { x.CompanyId, x.CustomerId, x.Status, x.CreatedDate })
            .HasDatabaseName("IX_ComplaintReports_Company_Customer_Status_CreatedDate");

        entity.HasIndex(x => new { x.CompanyId, x.Status, x.ProposedCompletionAt })
            .HasDatabaseName("IX_ComplaintReports_Company_Status_ProposedCompletionAt");

        entity.HasOne(x => x.Company)
            .WithMany(x => x.ComplaintReports)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_Company");

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.ComplaintReports)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_Customer");

        entity.HasOne(x => x.AttachmentCollection)
            .WithMany()
            .HasForeignKey(x => x.AttachmentCollectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_AttachmentCollection");

        entity.HasOne(x => x.IssuePart)
            .WithMany()
            .HasForeignKey(x => x.IssuePartId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_IssuePart");

        entity.HasOne(x => x.CausingPart)
            .WithMany()
            .HasForeignKey(x => x.CausingPartId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_CausingPart");

        entity.HasOne(x => x.EffectivenessPersonInCharge)
            .WithMany()
            .HasForeignKey(x => x.EffectivenessPersonInChargeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_EffectivenessPersonInCharge");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany(x => x.ComplaintReportCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_CreatedBy");

        entity.HasOne(x => x.UpdatedByNavigation)
            .WithMany(x => x.ComplaintReportUpdatedByNavigations)
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_UpdatedBy");

        entity.HasOne(x => x.CompletedByNavigation)
            .WithMany(x => x.ComplaintReportCompletedByNavigations)
            .HasForeignKey(x => x.CompletedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ComplaintReports_CompletedBy");
    }
}
