using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Commons.Concurrency;

public static class OptimisticConcurrencyHelper
{
    public const string ConflictMessageMarker = "Reload before saving again.";

    public static string? ValidateExpectedUpdatedDate(
        DateTime? expectedUpdatedDate,
        DateTime? currentUpdatedDate,
        string resourceName)
    {
        if (!expectedUpdatedDate.HasValue)
        {
            return null;
        }

        if (currentUpdatedDate.HasValue &&
            expectedUpdatedDate.Value.Ticks == currentUpdatedDate.Value.Ticks)
        {
            return null;
        }

        return CreateConflictMessage(resourceName);
    }

    public static string? ValidateExpectedUpdatedDateWithDatabasePrecision(
        DateTime? expectedUpdatedDate,
        DateTime? currentUpdatedDate,
        string resourceName)
    {
        if (!expectedUpdatedDate.HasValue)
        {
            return null;
        }

        if (currentUpdatedDate.HasValue &&
            NormalizeToDatabasePrecision(expectedUpdatedDate.Value).Ticks ==
            NormalizeToDatabasePrecision(currentUpdatedDate.Value).Ticks)
        {
            return null;
        }

        return CreateConflictMessage(resourceName);
    }

    public static string CreateConflictMessage(string resourceName)
        => $"{resourceName} was changed by another request. {ConflictMessageMarker}";

    public static string CreateConflictMessage(
        string resourceName,
        DbUpdateConcurrencyException exception)
    {
        var affectedEntities = exception.Entries
            .Select(entry => entry.Entity.GetType().Name)
            .Distinct()
            .ToArray();
        var suffix = affectedEntities.Length == 0
            ? string.Empty
            : $" Affected entities: {string.Join(", ", affectedEntities)}.";

        return $"{CreateConflictMessage(resourceName)}{suffix}";
    }

    public static bool IsConflictMessage(string? message)
        => message?.Contains(ConflictMessageMarker, StringComparison.Ordinal) == true;

    private static DateTime NormalizeToDatabasePrecision(DateTime value)
    {
        var ticks = value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond);
        return new DateTime(ticks, value.Kind);
    }
}
