using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalConversationParticipantConfiguration : IEntityTypeConfiguration<InternalConversationParticipant>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalConversationParticipant> entity)
    {
        entity.ToTable("InternalConversationParticipants", Schema);
        entity.HasKey(x => new { x.InternalConversationId, x.EmployeeId });

        entity.Property(x => x.InternalConversationId).HasColumnName("InternalConversationId");
        entity.Property(x => x.EmployeeId).HasColumnName("EmployeeId");
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        entity.Property(x => x.DeletedByEmployeeId).HasColumnName("DeletedByEmployeeId");
        entity.Property(x => x.Role).HasColumnName("Role").HasConversion<int>().HasDefaultValue(HRM.Domain.Enums.InternalMailEnums.InternalConversationParticipantRole.Member);
        entity.Property(x => x.IsArchived).HasColumnName("IsArchived").HasDefaultValue(false);
        entity.Property(x => x.ArchivedAt).HasColumnName("ArchivedAt");
        entity.Property(x => x.LastReadAt).HasColumnName("LastReadAt");
        entity.Property(x => x.JoinedAt).HasColumnName("JoinedAt");
        entity.Property(x => x.IsMuted).HasColumnName("IsMuted").HasDefaultValue(false);

        entity.HasIndex(x => new { x.EmployeeId, x.IsArchived, x.IsMuted })
            .HasDatabaseName("IX_InternalConversationParticipants_EmployeeId_IsArchived_IsMuted");
        entity.HasIndex(x => new { x.EmployeeId, x.LastReadAt })
            .HasDatabaseName("IX_InternalConversationParticipants_EmployeeId_LastReadAt");
        entity.HasIndex(x => new { x.EmployeeId, x.IsActive })
            .HasDatabaseName("IX_InternalConversationParticipants_Employee_IsActive");

        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.InternalConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_InternalConversationParticipants_InternalConversation");

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.InternalConversationParticipants)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalConversationParticipants_Employee");
    }
}
