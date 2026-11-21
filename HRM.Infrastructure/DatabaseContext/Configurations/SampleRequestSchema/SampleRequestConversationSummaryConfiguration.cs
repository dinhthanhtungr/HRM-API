using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class SampleRequestConversationSummaryConfiguration : IEntityTypeConfiguration<SampleRequestConversationSummary>
{
    public void Configure(EntityTypeBuilder<SampleRequestConversationSummary> entity)
    {
        entity.ToTable("SampleRequestConversationSummaries", "SampleRequests");
        entity.HasKey(x => new { x.CompanyId, x.SampleRequestId });
        entity.Property(x => x.ContentJson).HasColumnType("jsonb");
        entity.Property(x => x.AppliedSourceFingerprint).HasMaxLength(64);
        entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
        entity.Property(x => x.LastErrorCode).HasMaxLength(80);
        entity.Property(x => x.SyncedAt).HasColumnType("timestamp without time zone");
        entity.HasOne<SampleRequest>().WithMany().HasForeignKey(x => x.SampleRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
