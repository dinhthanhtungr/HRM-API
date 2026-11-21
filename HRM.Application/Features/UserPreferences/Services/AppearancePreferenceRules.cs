using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Domain.Entities.Preferences;
using HRM.Domain.Enums.Preferences;

namespace HRM.Application.Features.UserPreferences.Services;

public static partial class AppearancePreferenceRules
{
    public const int SchemaVersion = 1;
    public const int MaxBytes = 16 * 1024;
    public const int MaxCategories = 20;
    public const string CategoryName = "appearance";
    private static readonly HashSet<string> Presets =
        ["default", "ocean", "forest", "violet", "rose", "slate", "custom"];
    private static readonly HashSet<string> Fields = ["preset", "color", "background", "emphasis"];

    public static bool TryCategory(string? name, out UserPreferenceCategory category)
    {
        category = name switch
        {
            CategoryName => UserPreferenceCategory.Appearance,
            "display" => UserPreferenceCategory.Display,
            "custom-theme-library" => UserPreferenceCategory.CustomThemeLibrary,
            _ => 0
        };
        return category != 0;
    }

    public static JsonElement DefaultData() => JsonSerializer.SerializeToElement(new
    {
        preset = "default", background = "neutral", emphasis = "light"
    });

    public static JsonElement? Normalize(SaveUserPreferenceRequest request,
        UserPreferenceCategory category = UserPreferenceCategory.Appearance)
    {
        if (category != UserPreferenceCategory.Appearance)
            return AdvancedPreferenceRules.Normalize(category, request);
        if (request.SchemaVersion != SchemaVersion || request.ExpectedRevision < 0 ||
            request.ExpectedRevision == long.MaxValue || request.Data.ValueKind != JsonValueKind.Object ||
            Encoding.UTF8.GetByteCount(request.Data.GetRawText()) > MaxBytes) return null;
        var seen = new HashSet<string>();
        foreach (var property in request.Data.EnumerateObject())
            if (!Fields.Contains(property.Name) || !seen.Add(property.Name)) return null;
        string? Read(string key) => request.Data.TryGetProperty(key, out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var preset = Read("preset");
        var background = Read("background");
        var emphasis = Read("emphasis");
        var color = Read("color");
        if (preset is null || !Presets.Contains(preset) ||
            background is not ("neutral" or "tinted") ||
            emphasis is not ("light" or "medium" or "strong")) return null;
        if (preset == "custom")
        {
            if (color is null || !HexColor().IsMatch(color)) return null;
            return JsonSerializer.SerializeToElement(new
            {
                preset, color = color.ToLowerInvariant(), background, emphasis
            });
        }
        if (request.Data.TryGetProperty("color", out _)) return null;
        return JsonSerializer.SerializeToElement(new { preset, background, emphasis });
    }

    public static UserPreferenceDto ToDto(UserPreference? preference,
        UserPreferenceCategory category = UserPreferenceCategory.Appearance) =>
        preference is null
            ? new(category, SchemaVersion, category == UserPreferenceCategory.Appearance
                ? DefaultData() : AdvancedPreferenceRules.DefaultData(category), 0, null)
            : new(preference.Category, preference.SchemaVersion, preference.Data.RootElement.Clone(),
                preference.Revision, preference.UpdatedAtUtc);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
