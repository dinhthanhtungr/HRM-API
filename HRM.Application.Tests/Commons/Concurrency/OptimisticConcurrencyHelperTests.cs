using HRM.Application.Commons.Concurrency;

namespace HRM.Application.Tests.Commons.Concurrency;

public sealed class OptimisticConcurrencyHelperTests
{
    [Fact]
    public void ValidateWithDatabasePrecision_IgnoresSubMicrosecondDifference()
    {
        var databaseTimestamp = new DateTime(638925120001234560, DateTimeKind.Unspecified);
        var responseTimestamp = databaseTimestamp.AddTicks(7);

        var error = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
            responseTimestamp,
            databaseTimestamp,
            "Sample trial");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateWithDatabasePrecision_DetectsOneMicrosecondDifference()
    {
        var databaseTimestamp = new DateTime(638925120001234560, DateTimeKind.Unspecified);
        var staleTimestamp = databaseTimestamp.AddTicks(-TimeSpan.TicksPerMicrosecond);

        var error = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
            staleTimestamp,
            databaseTimestamp,
            "Sample trial");

        Assert.Equal(
            "Sample trial was changed by another request. Reload before saving again.",
            error);
    }
}
