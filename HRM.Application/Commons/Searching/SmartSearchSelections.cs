using System.Text.Json;

namespace HRM.Application.Commons.Searching;

internal static class SmartSearchSelections
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    internal static bool TryParse(string? raw, IEnumerable<string> allowedFields, out SmartSearchSelection[] selections)
    {
        selections = [];
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (raw.Length > 6000) return false;
        var allowed = allowedFields.ToHashSet(StringComparer.Ordinal);
        try
        {
            selections = JsonSerializer.Deserialize<SmartSearchSelection[]>(raw, Options) ?? [];
            return selections.Length <= 20 && selections.All(x => x is not null && allowed.Contains(x.Field) &&
                !string.IsNullOrWhiteSpace(x.Value) && x.Value.Length <= 200);
        }
        catch (JsonException) { return false; }
    }
}
