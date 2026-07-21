using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalMessageConfiguration : IEntityTypeConfiguration<InternalMessage>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalMessage> entity)
    {
        entity.ToTable("InternalMessages", Schema);
        entity.HasKey(x => x.InternalMessageId);

        entity.Property(x => x.InternalMessageId)
            .HasColumnName("InternalMessageId")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.InternalConversationId).HasColumnName("InternalConversationId").IsRequired();
        entity.Property(x => x.SenderEmployeeId).HasColumnName("SenderEmployeeId").IsRequired();
        entity.Property(x => x.MessageType).HasColumnName("MessageType").HasConversion<int>().HasDefaultValue(InternalMessageType.Text);
        entity.Property(x => x.Body).HasColumnName("Body").HasColumnType("text").IsRequired();
        entity.Property(x => x.PayloadJson).HasColumnName("PayloadJson").HasColumnType("jsonb");
        entity.Property(x => x.ReplyToMessageId).HasColumnName("ReplyToMessageId");
        entity.Property(x => x.IsUrgent).HasColumnName("IsUrgent").HasDefaultValue(false);
        entity.Property(x => x.SentAt).HasColumnName("SentAt");
        entity.Property(x => x.IsEdited).HasColumnName("IsEdited").HasDefaultValue(false);
        entity.Property(x => x.EditedAt).HasColumnName("EditedAt");
        entity.Property(x => x.EditedByEmployeeId).HasColumnName("EditedByEmployeeId");
        entity.Property(x => x.IsDeleted).HasColumnName("IsDeleted").HasDefaultValue(false);
        entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        entity.Property(x => x.DeletedByEmployeeId).HasColumnName("DeletedByEmployeeId");

        entity.HasIndex(x => new { x.InternalConversationId, x.SentAt })
            .HasDatabaseName("IX_InternalMessages_InternalConversationId_SentAt");
        entity.HasIndex(x => new { x.SenderEmployeeId, x.SentAt })
            .HasDatabaseName("IX_InternalMessages_SenderEmployeeId_SentAt");
        entity.HasIndex(x => x.ReplyToMessageId)
            .HasDatabaseName("IX_InternalMessages_ReplyToMessageId");
        entity.HasIndex(x => x.IsDeleted)
            .HasDatabaseName("IX_InternalMessages_IsDeleted");

        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.InternalConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_InternalMessages_InternalConversation");

        entity.HasOne(x => x.SenderEmployee)
            .WithMany(x => x.InternalMessageSenderNavigations)
            .HasForeignKey(x => x.SenderEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessages_SenderEmployee");

        entity.HasOne(x => x.EditedByEmployeeNavigation)
            .WithMany(x => x.InternalMessageEditedByNavigations)
            .HasForeignKey(x => x.EditedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessages_EditedByEmployee");

        entity.HasOne(x => x.DeletedByEmployeeNavigation)
            .WithMany(x => x.InternalMessageDeletedByNavigations)
            .HasForeignKey(x => x.DeletedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessages_DeletedByEmployee");

        entity.HasOne(x => x.ReplyToMessage)
            .WithMany(x => x.Replies)
            .HasForeignKey(x => x.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessages_ReplyToMessage");
    }
}
