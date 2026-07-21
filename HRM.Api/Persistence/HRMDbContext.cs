using System;
using System.Collections.Generic;
using HRM.Infrastructure.DatabaseContext.Views.Energy;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Persistence;

public partial class HRMDbContext : DbContext
{
    public HRMDbContext(DbContextOptions<HRMDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<VGroupDayCost> VGroupDayCosts { get; set; }

    public virtual DbSet<VGroupMonthBill> VGroupMonthBills { get; set; }

    public virtual DbSet<VGroupMonthCostRaw> VGroupMonthCostRaws { get; set; }

    public virtual DbSet<VHourlySource> VHourlySources { get; set; }

    public virtual DbSet<VMeterGroupActiveHourly> VMeterGroupActiveHourlies { get; set; }

    public virtual DbSet<VMetersRuntime> VMetersRuntimes { get; set; }

    public virtual DbSet<VMissingLast24h> VMissingLast24hs { get; set; }

    public virtual DbSet<VReadingsGrouped> VReadingsGroupeds { get; set; }

    public virtual DbSet<VReadingsPriced> VReadingsPriceds { get; set; }

    public virtual DbSet<VReadingsPricedCompat> VReadingsPricedCompats { get; set; }

    public virtual DbSet<VReadingsWithTou> VReadingsWithTous { get; set; }

    public virtual DbSet<VReadingsWithTouCompat> VReadingsWithTouCompats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("btree_gist")
            .HasPostgresExtension("citext")
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("pgcrypto")
            .HasPostgresExtension("unaccent");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
