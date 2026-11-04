using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public sealed class InternalConversationGroupConfiguration : IEntityTypeConfiguration<InternalConversationGroup>
{
    public void Configure(EntityTypeBuilder<InternalConversationGroup> entity)
    {
        entity.ToTable("InternalConversationGroups", "InternalMail", table =>
        {
            // Schema is deployed separately; the synchronization API and atomic live writer need no DB trigger.
            table.ExcludeFromMigrations();
            table.HasCheckConstraint("CK_InternalConversationGroups_Type", "\"GroupType\" IN (0,1,2,3)");
            table.HasCheckConstraint("CK_InternalConversationGroups_Root",
                "(\"GroupType\" = 0 AND \"RootConversationId\" = \"ConversationId\") OR (\"GroupType\" <> 0 AND \"RootConversationId\" <> \"ConversationId\")");
            table.HasCheckConstraint("CK_InternalConversationGroups_Name",
                "(\"DisplayName\" IS NULL OR btrim(\"DisplayName\") <> '') AND (\"GroupType\" <> 3 OR \"DisplayName\" IS NOT NULL)");
        });
        entity.HasKey(x => x.ConversationId).HasName("PK_InternalConversationGroups");
        entity.HasAlternateKey(x => new { x.CompanyId, x.ConversationId }).HasName("UX_InternalConversationGroups_Company_Id");
        entity.Property(x => x.ConversationId).HasColumnName("ConversationId").ValueGeneratedNever();
        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.RootConversationId).HasColumnName("RootConversationId").IsRequired();
        entity.Property(x => x.GroupType).HasColumnName("GroupType").HasConversion<short>().HasColumnType("smallint").IsRequired();
        entity.Property(x => x.DisplayName).HasColumnName("DisplayName").HasColumnType("character varying(100)").HasMaxLength(100);
        entity.Property(x => x.IsActive).HasColumnName("IsActive").IsRequired();
        entity.HasOne(x => x.Conversation).WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.ConversationId })
            .HasPrincipalKey(x => new { x.CompanyId, x.InternalConversationId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_InternalConversationGroups_Conversation")
            .Metadata.PrincipalKey.SetName("UX_GroupStorage_Conversations_Company_Id");
        entity.HasOne(x => x.RootGroup).WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.RootConversationId })
            .HasPrincipalKey(x => new { x.CompanyId, x.ConversationId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_InternalConversationGroups_Root");
        entity.HasIndex(x => new { x.CompanyId, x.RootConversationId, x.GroupType })
            .HasDatabaseName("IX_InternalConversationGroups_Root");
        entity.HasIndex(x => new { x.CompanyId, x.RootConversationId, x.GroupType }, "ActiveDefaultGroups")
            .IsUnique().HasFilter("\"IsActive\" AND \"GroupType\" IN (1,2)")
            .HasDatabaseName("UX_InternalConversationGroups_ActiveDefault");
    }
}
