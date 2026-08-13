using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class SampleRequestSampleTrialConfiguration : IEntityTypeConfiguration<SampleRequestSampleTrial>
{
    public void Configure(EntityTypeBuilder<SampleRequestSampleTrial> entity)
    {
        entity.ToTable("SampleRequestSampleTrials", "SampleRequests");

        entity.HasKey(x => x.SampleRequestSampleTrialId);

        entity.Property(x => x.SampleRequestSampleTrialId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(SampleTrialStatus.Draft)
            .IsRequired();

        entity.Property(x => x.CustomerReplyStatus)
            .HasMaxLength(50);

        entity.Property(x => x.BatchNo)
            .HasMaxLength(100);

        entity.Property(x => x.DeliveryMethod)
            .HasMaxLength(100);

        entity.Property(x => x.CustomerNameSnapshot)
            .HasMaxLength(500);

        entity.Property(x => x.SampleRequestExternalIdSnapshot)
            .HasMaxLength(100);

        entity.Property(x => x.ProductNameSnapshot)
            .HasMaxLength(1000);

        entity.Property(x => x.ColourCodeSnapshot)
            .HasMaxLength(100);

        entity.Property(x => x.CategoryNameSnapshot)
            .HasMaxLength(255);

        entity.Property(x => x.DeliveredSampleQuantityKg)
            .HasPrecision(18, 4);

        entity.Property(x => x.AdditiveRate)
            .HasPrecision(18, 4);

        entity.Property(x => x.LabNote)
            .HasColumnType("text");

        entity.Property(x => x.CustomerReplyNote)
            .HasColumnType("text");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasIndex(x => new { x.SampleRequestId, x.TrialNo })
            .IsUnique()
            .HasDatabaseName("UX_SampleRequestSampleTrials_Request_TrialNo");

        entity.HasIndex(x => new { x.SampleRequestId, x.IsActive })
            .HasDatabaseName("IX_SampleRequestSampleTrials_Request_Active");

        entity.HasIndex(x => new { x.Status, x.CustomerReplyStatus })
            .HasDatabaseName("IX_SampleRequestSampleTrials_Status_ReplyStatus");

        entity.HasIndex(x => x.SentDate)
            .HasDatabaseName("IX_SampleRequestSampleTrials_SentDate");

        entity.HasIndex(x => x.SampleReceivedDate)
            .HasDatabaseName("IX_SampleRequestSampleTrials_SampleReceivedDate");

        entity.HasIndex(x => x.CustomerReplyDate)
            .HasDatabaseName("IX_SampleRequestSampleTrials_CustomerReplyDate");

        entity.HasOne(x => x.SampleRequest)
            .WithMany(x => x.SampleRequestSampleTrials)
            .HasForeignKey(x => x.SampleRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SampleRequestSampleTrials_SampleRequest");

        entity.HasOne(x => x.Formula)
            .WithMany(x => x.SampleRequestSampleTrials)
            .HasForeignKey(x => x.FormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SampleRequestSampleTrials_Formula");
    }
}
