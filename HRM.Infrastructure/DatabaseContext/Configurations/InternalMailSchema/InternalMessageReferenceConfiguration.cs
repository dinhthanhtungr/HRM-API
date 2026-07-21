using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalMessageReferenceConfiguration : IEntityTypeConfiguration<InternalMessageReference>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalMessageReference> entity)
    {
        entity.ToTable("InternalMessageReferences", Schema);
        entity.HasKey(x => x.InternalMessageReferenceId);

        entity.Property(x => x.InternalMessageReferenceId)
            .HasColumnName("InternalMessageReferenceId")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.InternalMessageId).HasColumnName("InternalMessageId").IsRequired();
        entity.Property(x => x.RelatedType).HasColumnName("RelatedType").HasConversion<int>().IsRequired();
        entity.Property(x => x.RelatedId).HasColumnName("RelatedId").IsRequired();
        entity.Property(x => x.RelatedExternalId).HasColumnName("RelatedExternalId").HasColumnType("citext");
        entity.Property(x => x.RelatedNameSnapshot).HasColumnName("RelatedNameSnapshot").HasColumnType("citext");
        entity.Property(x => x.SnapshotJson).HasColumnName("SnapshotJson").HasColumnType("jsonb");
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);

        entity.HasIndex(x => new { x.RelatedType, x.RelatedId })
            .HasDatabaseName("IX_InternalMessageReferences_RelatedType_RelatedId");
        entity.HasIndex(x => new { x.InternalMessageId, x.RelatedType, x.RelatedId })
            .IsUnique()
            .HasDatabaseName("UX_InternalMessageReferences_InternalMessageId_RelatedType_RelatedId");
        entity.HasIndex(x => x.InternalMessageId)
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true")
            .HasDatabaseName("UX_InternalMessageReferences_InternalMessageId_IsPrimary");

        entity.HasOne(x => x.Message)
            .WithMany(x => x.References)
            .HasForeignKey(x => x.InternalMessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_InternalMessageReferences_InternalMessage");
    }
}
