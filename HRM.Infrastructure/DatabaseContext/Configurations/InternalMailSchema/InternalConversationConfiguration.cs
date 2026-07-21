using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalConversationConfiguration : IEntityTypeConfiguration<InternalConversation>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalConversation> entity)
    {
        entity.ToTable("InternalConversations", Schema);
        entity.HasKey(x => x.InternalConversationId);

        entity.Property(x => x.InternalConversationId)
            .HasColumnName("InternalConversationId")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.Subject).HasColumnName("Subject").HasColumnType("citext").IsRequired();
        entity.Property(x => x.RelatedType).HasColumnName("RelatedType").HasConversion<int>();
        entity.Property(x => x.RelatedId).HasColumnName("RelatedId");
        entity.Property(x => x.RelatedExternalId).HasColumnName("RelatedExternalId").HasColumnType("citext");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        entity.Property(x => x.LastMessageAt).HasColumnName("LastMessageAt");
        entity.Property(x => x.LastMessageId).HasColumnName("LastMessageId");
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        entity.Property(x => x.DeletedByEmployeeId).HasColumnName("DeletedByEmployeeId");

        entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.LastMessageAt })
            .HasDatabaseName("IX_InternalConversations_CompanyId_IsActive_LastMessageAt");
        entity.HasIndex(x => new { x.CompanyId, x.RelatedType, x.RelatedId })
            .HasDatabaseName("IX_InternalConversations_CompanyId_RelatedType_RelatedId");
        entity.HasIndex(x => x.LastMessageId)
            .HasDatabaseName("IX_InternalConversations_LastMessageId");

        entity.HasOne(x => x.Company)
            .WithMany(x => x.InternalConversations)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalConversations_Company");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany(x => x.InternalConversationCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalConversations_CreatedBy");

        entity.HasOne(x => x.DeletedByEmployeeNavigation)
            .WithMany(x => x.InternalConversationDeletedByNavigations)
            .HasForeignKey(x => x.DeletedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalConversations_DeletedByEmployee");

        entity.HasOne(x => x.LastMessage)
            .WithMany()
            .HasForeignKey(x => x.LastMessageId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_InternalConversations_LastMessage");
    }
}
