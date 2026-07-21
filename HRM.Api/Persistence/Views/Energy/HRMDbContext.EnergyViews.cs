using HRM.Infrastructure.DatabaseContext.Views.Energy;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Persistence;

public partial class HRMDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VGroupDayCost>(entity =>
        {
            entity.HasNoKey().ToView("v_group_day_cost", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.D).HasColumnName("d");
            entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.Kwh).HasColumnName("kwh");
        });

        modelBuilder.Entity<VGroupMonthBill>(entity =>
        {
            entity.HasNoKey().ToView("v_group_month_bill", "energy");

            entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
            entity.Property(e => e.FixedFeeVnd).HasColumnName("fixed_fee_vnd");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.Kwh).HasColumnName("kwh");
            entity.Property(e => e.SubtotalVnd).HasColumnName("subtotal_vnd");
            entity.Property(e => e.TotalVnd).HasColumnName("total_vnd");
            entity.Property(e => e.VatVnd).HasColumnName("vat_vnd");
            entity.Property(e => e.Ym).HasColumnName("ym");
        });

        modelBuilder.Entity<VGroupMonthCostRaw>(entity =>
        {
            entity.HasNoKey().ToView("v_group_month_cost_raw", "energy");

            entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
            entity.Property(e => e.FuelAdjAny).HasColumnName("fuel_adj_any");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.Kwh).HasColumnName("kwh");
            entity.Property(e => e.VatRateAny).HasColumnName("vat_rate_any");
            entity.Property(e => e.Ym).HasColumnName("ym");
        });

        modelBuilder.Entity<VHourlySource>(entity =>
        {
            entity.HasNoKey().ToView("v_hourly_source", "energy");

            entity.Property(e => e.KwhImport).HasColumnName("kwh_import");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsHourVn)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_hour_vn");
        });

        modelBuilder.Entity<VMeterGroupActiveHourly>(entity =>
        {
            entity.HasNoKey().ToView("v_meter_group_active_hourly", "energy");

            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsHourVn)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_hour_vn");
        });

        modelBuilder.Entity<VMetersRuntime>(entity =>
        {
            entity.HasNoKey().ToView("v_meters_runtime", "energy");

            entity.Property(e => e.KwhLast1h).HasColumnName("kwh_last_1h");
            entity.Property(e => e.KwhToday).HasColumnName("kwh_today");
            entity.Property(e => e.LastAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_at");
            entity.Property(e => e.MeterCode)
                .HasColumnType("citext")
                .HasColumnName("meter_code");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.MeterName)
                .HasColumnType("citext")
                .HasColumnName("meter_name");
            entity.Property(e => e.MinutesSinceLast).HasColumnName("minutes_since_last");
            entity.Property(e => e.Multiplier)
                .HasPrecision(10, 4)
                .HasColumnName("multiplier");
            entity.Property(e => e.PowerKwEst).HasColumnName("power_kw_est");
            entity.Property(e => e.Status).HasColumnName("status");
        });

        modelBuilder.Entity<VMissingLast24h>(entity =>
        {
            entity.HasNoKey().ToView("v_missing_last24h", "energy");

            entity.Property(e => e.HourUtcExpected)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("hour_utc_expected");
            entity.Property(e => e.MeterCode)
                .HasColumnType("citext")
                .HasColumnName("meter_code");
        });

        modelBuilder.Entity<VReadingsGrouped>(entity =>
        {
            entity.HasNoKey().ToView("v_readings_grouped", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.CalCode).HasColumnName("cal_code");
            entity.Property(e => e.CalendarId).HasColumnName("calendar_id");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.KwhImport)
                .HasPrecision(14, 5)
                .HasColumnName("kwh_import");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.Quality).HasColumnName("quality");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.TsLocal).HasColumnName("ts_local");
            entity.Property(e => e.TsUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_utc");
            entity.Property(e => e.Tz).HasColumnName("tz");
        });

        modelBuilder.Entity<VReadingsPriced>(entity =>
        {
            entity.HasNoKey().ToView("v_readings_priced", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
            entity.Property(e => e.FuelAdjVndPerKwh)
                .HasPrecision(12, 4)
                .HasColumnName("fuel_adj_vnd_per_kwh");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.KwhImport)
                .HasPrecision(14, 5)
                .HasColumnName("kwh_import");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.PriceVndPerKwh)
                .HasPrecision(12, 4)
                .HasColumnName("price_vnd_per_kwh");
            entity.Property(e => e.Quality).HasColumnName("quality");
            entity.Property(e => e.TsLocal).HasColumnName("ts_local");
            entity.Property(e => e.TsUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_utc");
            entity.Property(e => e.VatRate)
                .HasPrecision(6, 4)
                .HasColumnName("vat_rate");
            entity.Property(e => e.VersionId).HasColumnName("version_id");
        });

        modelBuilder.Entity<VReadingsPricedCompat>(entity =>
        {
            entity.HasNoKey().ToView("v_readings_priced_compat", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.EnergyCostVnd).HasColumnName("energy_cost_vnd");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.KwhImport).HasColumnName("kwh_import");
            entity.Property(e => e.TsLocal)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_local");
        });

        modelBuilder.Entity<VReadingsWithTou>(entity =>
        {
            entity.HasNoKey().ToView("v_readings_with_tou", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.CalCode).HasColumnName("cal_code");
            entity.Property(e => e.CalendarId).HasColumnName("calendar_id");
            entity.Property(e => e.KwhImport)
                .HasPrecision(14, 5)
                .HasColumnName("kwh_import");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.Quality).HasColumnName("quality");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.TsLocal).HasColumnName("ts_local");
            entity.Property(e => e.TsUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_utc");
            entity.Property(e => e.Tz).HasColumnName("tz");
        });

        modelBuilder.Entity<VReadingsWithTouCompat>(entity =>
        {
            entity.HasNoKey().ToView("v_readings_with_tou_compat", "energy");

            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.GroupCode)
                .HasColumnType("citext")
                .HasColumnName("group_code");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.KwhImport).HasColumnName("kwh_import");
            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsHourVn)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_hour_vn");
        });
    }
}
