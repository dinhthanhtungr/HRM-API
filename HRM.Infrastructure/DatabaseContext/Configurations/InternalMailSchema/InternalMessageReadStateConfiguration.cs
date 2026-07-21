using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public class InternalMessageReadStateConfiguration : IEntityTypeConfiguration<InternalMessageReadState>
{
    private const string Schema = "InternalMail";

    public void Configure(EntityTypeBuilder<InternalMessageReadState> entity)
    {
        entity.ToTable("InternalMessageReadStates", Schema);
        entity.HasKey(x => new { x.InternalMessageId, x.EmployeeId });

        entity.Property(x => x.InternalMessageId).HasColumnName("InternalMessageId");
        entity.Property(x => x.EmployeeId).HasColumnName("EmployeeId");
        entity.Property(x => x.IsRead).HasColumnName("IsRead").HasDefaultValue(false);
        entity.Property(x => x.ReadAt).HasColumnName("ReadAt");

        entity.HasIndex(x => new { x.EmployeeId, x.IsRead })
            .HasDatabaseName("IX_InternalMessageReadStates_EmployeeId_IsRead");

        entity.HasOne(x => x.Message)
            .WithMany(x => x.ReadStates)
            .HasForeignKey(x => x.InternalMessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_InternalMessageReadStates_InternalMessage");

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.InternalMessageReadStates)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InternalMessageReadStates_Employee");
    }
}
