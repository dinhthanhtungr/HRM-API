using System.Globalization;

namespace HRM.Application.Features.Warehouse.Services;

public static class WarehouseMovementDiagnosticTags
{
    public const string Enabled = "warehouse.movements.timing-enabled";
    public const string ServerTiming = "warehouse.movements.server-timing";
    internal static readonly HashSet<string> Stages = ["scope", "suggestions", "count", "page", "rows", "context", "actors", "vouchers", "requests", "mapping", "handler"];
    internal static readonly HashSet<string> JourneyStages = ["scope", "key", "seed", "statistics", "page", "rows", "context", "actors", "vouchers", "requests", "verification", "business", "locations", "allocations", "mapping", "handler"];

    internal static bool IsStage(string operation, string stage) => (operation == "wj" ? JourneyStages : Stages).Contains(stage);

    public static string? ValidateServerTiming(string? value, string operation = "wm")
    {
        if (operation is not ("wm" or "wj")) return null;
        if (string.IsNullOrEmpty(value) || value.Length > 2048 || value.Any(char.IsControl)) return null;
        var entries = value.Split(',');
        if (entries.Length > (operation == "wj" ? JourneyStages.Count : Stages.Count)) return null;
        foreach (var entry in entries)
        {
            var parts = entry.Trim().Split(";dur=", StringSplitOptions.None);
            if (parts.Length != 2 || !parts[0].StartsWith(operation + "_", StringComparison.Ordinal) || !IsStage(operation, parts[0][3..]) ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var elapsed) || elapsed < 0) return null;
        }
        return string.Join(", ", entries.Select(x => x.Trim()));
    }
}
