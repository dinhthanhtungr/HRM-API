namespace HRM.Application.Features.PLM.Formulas.Services;

internal static class FormulaConcurrencyRules
{
    public static bool HasExpectedUpdatedDateConflict(
        DateTime? expectedUpdatedDate,
        DateTime? currentUpdatedDate,
        DateTime? createdDate = null)
    {
        var currentTimestamp = currentUpdatedDate ?? createdDate;

        return expectedUpdatedDate.HasValue &&
               currentTimestamp.HasValue &&
               currentTimestamp.Value.Ticks != expectedUpdatedDate.Value.Ticks;
    }

    public static bool HasExpectedUpdatedDateConflictWithDatabasePrecision(
        DateTime? expectedUpdatedDate,
        DateTime? currentUpdatedDate)
    {
        return expectedUpdatedDate.HasValue &&
               currentUpdatedDate.HasValue &&
               NormalizeDatabaseTimestamp(currentUpdatedDate.Value).Ticks !=
               NormalizeDatabaseTimestamp(expectedUpdatedDate.Value).Ticks;
    }

    public static DateTime NormalizeDatabaseTimestamp(DateTime value)
    {
        var normalizedTicks = value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond);
        return new DateTime(normalizedTicks, value.Kind);
    }
}
