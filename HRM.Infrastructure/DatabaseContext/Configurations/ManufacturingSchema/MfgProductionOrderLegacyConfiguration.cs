using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema;

public sealed class MfgProductionOrderLegacyConfiguration : IEntityTypeConfiguration<MfgProductionOrder>
{
    public void Configure(EntityTypeBuilder<MfgProductionOrder> entity)
    {
        entity.Property(x => x.IsPrintedStock).HasColumnName("is_printed_stock").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.Checker).HasColumnName("checker");
        entity.Property(x => x.CheckedDate).HasColumnName("checked_date");
        entity.HasOne(x => x.CheckerNavigation).WithMany().HasForeignKey(x => x.Checker).OnDelete(DeleteBehavior.Restrict);
    }
}
