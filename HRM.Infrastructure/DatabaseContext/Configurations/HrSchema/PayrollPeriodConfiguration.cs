using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> entity)
    {
        entity.ToTable("PayrollPeriods", "hr");
        entity.HasKey(x => x.PayrollPeriodId);

        entity.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Note)
            .HasColumnType("text");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.ApprovedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => new { x.Year, x.Month, x.PayrollType })
            .HasDatabaseName("IX_PayrollPeriods_Year_Month_Type");
    }
}
