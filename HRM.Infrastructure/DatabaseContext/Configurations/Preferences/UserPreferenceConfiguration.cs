using HRM.Domain.Entities.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.Preferences;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> entity)
    {
        entity.ToTable("UserPreferences", "public", table =>
        {
            table.HasCheckConstraint("CK_UserPreferences_SchemaVersion", "\"SchemaVersion\" >= 1");
            table.HasCheckConstraint("CK_UserPreferences_Revision", "\"Revision\" >= 1");
            table.HasCheckConstraint("CK_UserPreferences_DataObject", "jsonb_typeof(\"Data\") = 'object'");
        });
        entity.HasKey(x => new { x.UserId, x.Category });
        entity.Property(x => x.Category).HasConversion<int>();
        entity.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.Revision).IsConcurrencyToken();
        entity.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
