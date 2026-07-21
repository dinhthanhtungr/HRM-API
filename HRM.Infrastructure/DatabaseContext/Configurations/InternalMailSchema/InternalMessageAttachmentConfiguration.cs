using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalMessageAttachmentConfiguration : IEntityTypeConfiguration<InternalMessageAttachment>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalMessageAttachment> entity)
    {
        entity.ToTable("InternalMessageAttachments", Schema);
        entity.HasKey(x => x.InternalMessageAttachmentId);

        entity.Property(x => x.InternalMessageAttachmentId)
            .HasColumnName("InternalMessageAttachmentId")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.InternalMessageId).HasColumnName("InternalMessageId").IsRequired();
        entity.Property(x => x.AttachmentId).HasColumnName("AttachmentId").IsRequired();
        entity.Property(x => x.AttachedAt).HasColumnName("AttachedAt");

        entity.HasIndex(x => x.InternalMessageId)
            .HasDatabaseName("IX_InternalMessageAttachments_InternalMessageId");
        entity.HasIndex(x => x.AttachmentId)
            .HasDatabaseName("IX_InternalMessageAttachments_AttachmentId");

        entity.HasOne(x => x.Message)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.InternalMessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_InternalMessageAttachments_InternalMessage");

        entity.HasOne(x => x.Attachment)
            .WithMany(x => x.InternalMessageAttachments)
            .HasForeignKey(x => x.AttachmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessageAttachments_Attachment");
    }
}
