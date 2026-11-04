using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public sealed class InternalMessageReactionConfiguration : IEntityTypeConfiguration<InternalMessageReaction>
{
    public void Configure(EntityTypeBuilder<InternalMessageReaction> entity)
    {
        entity.ToTable("InternalMessageReactions", "InternalMail", table =>
            table.HasCheckConstraint("CK_InternalMessageReactions_Type", "\"ReactionType\" BETWEEN 1 AND 6"));
        entity.HasKey(x => new { x.MessageId, x.EmployeeId });
        entity.Property(x => x.ReactionType).HasConversion<int>().IsRequired();
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.UpdatedAt).IsRequired();
        entity.HasIndex(x => new { x.MessageId, x.ReactionType });
        entity.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
