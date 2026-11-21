using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderPauseTrackingPostgresTests
{
    [ReadOnlyPostgresFact]
    public async Task ScopedReadSubquery_DetachesOrder_UnlessMutationExplicitlyTracks()
    {
        var connection = Environment.GetEnvironmentVariable("HRM_PAUSE_READONLY_TEST_CONNECTION");
        await using var db = new ProbeDb(new DbContextOptionsBuilder<ProbeDb>().UseNpgsql(connection).Options);
        // Read only: reproduce the no-tracking annotation introduced by the visibility subquery.
        var visibleIds = db.Orders.AsNoTracking().Select(x => x.Id);
        var query = db.Orders.Where(x => visibleIds.Contains(x.Id));
        var detached = await query.FirstAsync();
        Assert.Equal(EntityState.Detached, db.Entry(detached).State);
        var tracked = await query.AsTracking().FirstAsync();
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
        tracked.Paused = !tracked.Paused;
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, db.Entry(tracked).State);
        // No SaveChanges: never alter the source database.
    }

    private sealed class ProbeDb(DbContextOptions<ProbeDb> options) : DbContext(options)
    {
        public DbSet<ProbeOrder> Orders => Set<ProbeOrder>();
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ProbeOrder>(b =>
            {
                b.ToTable("MerchandiseOrders", "Orders");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("MerchandiseOrderId");
                b.Property(x => x.Paused).HasColumnName("IsDeliveryPaused");
            });
        }
    }

    private sealed class ProbeOrder
    {
        public Guid Id { get; set; }
        public bool Paused { get; set; }
    }

    public sealed class ReadOnlyPostgresFactAttribute : FactAttribute
    {
        public ReadOnlyPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRM_PAUSE_READONLY_TEST_CONNECTION")))
                Skip = "Set HRM_PAUSE_READONLY_TEST_CONNECTION to run the read-only PostgreSQL tracking regression.";
        }
    }
}
