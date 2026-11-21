using System.Text;
using System.Text.Json;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Domain.Enums.Preferences;

namespace HRM.Application.Features.UserPreferences.Services;

public static class AdvancedPreferenceRules
{
    public const int LibraryLimit = 12;
    private static readonly Dictionary<string, string[]> DisplayFields = new()
    {
        ["colorScheme"] = ["light", "dark", "system"],
        ["density"] = ["compact", "standard", "comfortable"],
        ["textSize"] = ["extra-small", "small", "standard", "large"],
        ["fontFamily"] = ["open-sans", "system", "arial"],
        ["contrast"] = ["standard", "high"],
        ["motion"] = ["system", "reduced", "off"]
    };

    public static JsonElement DefaultData(UserPreferenceCategory category) => category switch
    {
        UserPreferenceCategory.Display => JsonSerializer.SerializeToElement(new
        {
            colorScheme = "light", density = "standard", textSize = "standard",
            contrast = "standard", motion = "system", fontFamily = "open-sans"
        }),
        UserPreferenceCategory.CustomThemeLibrary => JsonSerializer.SerializeToElement(new { samples = Array.Empty<object>() }),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static JsonElement? Normalize(UserPreferenceCategory category, SaveUserPreferenceRequest request)
    {
        if (request.SchemaVersion != 1 || request.ExpectedRevision < 0 ||
            request.ExpectedRevision == long.MaxValue || request.Data.ValueKind != JsonValueKind.Object ||
            Encoding.UTF8.GetByteCount(request.Data.GetRawText()) > AppearancePreferenceRules.MaxBytes) return null;
        if (category == UserPreferenceCategory.Display)
        {
            // Dữ liệu schema 1 cũ chưa có fontFamily vẫn giữ nguyên các lựa chọn khác.
            var hasFont = request.Data.TryGetProperty("fontFamily", out _);
            if (!ExactFields(request.Data, DisplayFields.Keys.Where(name => hasFont || name != "fontFamily"))) return null;
            var result = new Dictionary<string, string>();
            foreach (var (name, allowed) in DisplayFields)
            {
                if (name == "fontFamily" && !hasFont)
                {
                    result[name] = "open-sans";
                    continue;
                }
                var value = request.Data.GetProperty(name);
                if (value.ValueKind != JsonValueKind.String || !allowed.Contains(value.GetString())) return null;
                result[name] = value.GetString()!;
            }
            return JsonSerializer.SerializeToElement(result);
        }
        if (category != UserPreferenceCategory.CustomThemeLibrary ||
            !ExactFields(request.Data, ["samples"])) return null;
        var samples = request.Data.GetProperty("samples");
        if (samples.ValueKind != JsonValueKind.Array || samples.GetArrayLength() > LibraryLimit) return null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<object>();
        foreach (var sample in samples.EnumerateArray())
        {
            if (!ExactFields(sample, ["id", "name", "preference"])) return null;
            var id = sample.GetProperty("id");
            var name = sample.GetProperty("name");
            if (id.ValueKind != JsonValueKind.String || name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 80 ||
                string.IsNullOrWhiteSpace(name.GetString()) || name.GetString()!.Trim().Length > 40 ||
                !ids.Add(id.GetString()!)) return null;
            var color = AppearancePreferenceRules.Normalize(new(1, 0, sample.GetProperty("preference")));
            if (color is null) return null;
            normalized.Add(new { id = id.GetString(), name = name.GetString()!.Trim(), preference = color.Value });
        }
        return JsonSerializer.SerializeToElement(new { samples = normalized });
    }

    private static bool ExactFields(JsonElement value, IEnumerable<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var expected = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!expected.Remove(property.Name)) return false;
        return expected.Count == 0;
    }
}
