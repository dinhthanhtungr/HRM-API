using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class SampleRequestUserPreferenceConfiguration : IEntityTypeConfiguration<SampleRequestUserPreference>
{
    public void Configure(EntityTypeBuilder<SampleRequestUserPreference> entity)
    {
        entity.ToTable("SampleRequestUserPreferences", "SampleRequests");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.IsPinned).HasDefaultValue(false);
        entity.Property(x => x.NotifyOnDue).HasDefaultValue(false);
        entity.Property(x => x.ReminderTaskId);
        entity.Property(x => x.PinnedAt).HasColumnType("timestamp without time zone");
        entity.Property(x => x.CreatedDate).HasColumnType("timestamp without time zone");
        entity.Property(x => x.UpdatedDate).HasColumnType("timestamp without time zone");

        entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.SampleRequestId })
            .IsUnique().HasDatabaseName("UX_SampleRequestUserPreferences_Company_Employee_Request");

        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.SampleRequest).WithMany().HasForeignKey(x => x.SampleRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
