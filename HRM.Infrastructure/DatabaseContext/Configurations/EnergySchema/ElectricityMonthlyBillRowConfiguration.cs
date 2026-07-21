using HRM.Application.Abstractions.Persistence.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.EnergySchema;

public sealed class ElectricityMonthlyBillRowConfiguration : IEntityTypeConfiguration<ElectricityMonthlyBillRow>
{
    public void Configure(EntityTypeBuilder<ElectricityMonthlyBillRow> entity)
    {
        entity.HasNoKey().ToView("v_group_month_bill", "energy");

        entity.Property(e => e.GroupCode)
            .HasColumnType("citext")
            .HasColumnName("group_code");
        entity.Property(e => e.Ym).HasColumnName("ym");
        entity.Property(e => e.Kwh).HasColumnName("kwh");
        entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
        entity.Property(e => e.FixedFeeVnd).HasColumnName("fixed_fee_vnd");
        entity.Property(e => e.SubtotalVnd).HasColumnName("subtotal_vnd");
        entity.Property(e => e.VatVnd).HasColumnName("vat_vnd");
        entity.Property(e => e.TotalVnd).HasColumnName("total_vnd");
    }
}
