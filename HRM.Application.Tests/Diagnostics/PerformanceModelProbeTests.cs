using System.Data;
using System.Diagnostics;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HRM.Application.Tests.Diagnostics;

/// <summary>Manual metadata-only probe. Does not connect to any database.</summary>
public sealed class PerformanceModelProbeTests(ITestOutputHelper output)
{
    [ModelProbeFact]
    public void Measures_first_and_reused_model_without_opening_a_database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_metadata_only")
            .Options;
        var watch = Stopwatch.StartNew();
        using var first = new ApplicationDbContext(options);
        var count = first.Model.GetEntityTypes().Count();
        var firstMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        using var second = new ApplicationDbContext(options);
        Assert.Equal(count, second.Model.GetEntityTypes().Count());
        var reusedMs = watch.Elapsed.TotalMilliseconds;
        Assert.True(count > 0);
        Assert.Equal(ConnectionState.Closed, first.Database.GetDbConnection().State);
        Assert.Equal(ConnectionState.Closed, second.Database.GetDbConnection().State);
        output.WriteLine("MODEL_PROBE first_ms={0:F2} reused_ms={1:F2}", firstMs, reusedMs);
    }

    private sealed class ModelProbeFactAttribute : FactAttribute
    {
        public ModelProbeFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("HRM_RUN_MODEL_PROBE") != "1")
                Skip = "Manual model-only measurement; set HRM_RUN_MODEL_PROBE=1.";
        }
    }
}
