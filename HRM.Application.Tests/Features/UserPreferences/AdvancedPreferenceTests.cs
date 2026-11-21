using System.Text.Json;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Services;
using HRM.Domain.Enums.Preferences;

namespace HRM.Application.Tests.Features.UserPreferences;

public sealed class AdvancedPreferenceTests
{
    [Theory]
    [InlineData("open-sans")]
    [InlineData("system")]
    [InlineData("arial")]
    public void DisplayFontRoundTripsWithExistingSchema(string fontFamily)
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            colorScheme = "dark", density = "compact", textSize = "small",
            contrast = "high", motion = "off", fontFamily
        });
        var result = AppearancePreferenceRules.Normalize(new(1, 0, data), UserPreferenceCategory.Display);
        Assert.Equal(fontFamily, result!.Value.GetProperty("fontFamily").GetString());
        Assert.Equal("compact", result.Value.GetProperty("density").GetString());
    }

    [Fact]
    public void LegacyDisplayGetsDefaultFontWithoutLosingOtherOptions()
    {
        var result = AppearancePreferenceRules.Normalize(Request("{\"colorScheme\":\"dark\",\"density\":\"compact\",\"textSize\":\"small\",\"contrast\":\"high\",\"motion\":\"off\"}"), UserPreferenceCategory.Display);
        Assert.Equal("open-sans", result!.Value.GetProperty("fontFamily").GetString());
        Assert.Equal("dark", result.Value.GetProperty("colorScheme").GetString());
    }

    [Theory]
    [InlineData("url(https://example.invalid/font)")]
    [InlineData("unknown")]
    public void RejectsUnlistedFonts(string fontFamily)
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            colorScheme = "light", density = "standard", textSize = "standard",
            contrast = "standard", motion = "system", fontFamily
        });
        Assert.Null(AppearancePreferenceRules.Normalize(new(1, 0, data), UserPreferenceCategory.Display));
    }

    private static SaveUserPreferenceRequest Request(string json, int version = 1) =>
        new(version, 0, JsonSerializer.Deserialize<JsonElement>(json));

    [Theory]
    [InlineData("extra-small")]
    [InlineData("small")]
    [InlineData("standard")]
    [InlineData("large")]
    public void TextSizesRoundTripWithoutChangingSchema(string textSize)
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            colorScheme = "dark", density = "standard", textSize,
            contrast = "standard", motion = "system"
        });
        var result = AppearancePreferenceRules.Normalize(new(1, 0, data), UserPreferenceCategory.Display);
        Assert.Equal(textSize, result!.Value.GetProperty("textSize").GetString());
        Assert.Equal("standard", AdvancedPreferenceRules.DefaultData(UserPreferenceCategory.Display).GetProperty("textSize").GetString());
    }

    [Theory]
    [InlineData("display", UserPreferenceCategory.Display)]
    [InlineData("custom-theme-library", UserPreferenceCategory.CustomThemeLibrary)]
    public void CategoriesHaveIndependentDefaultRecords(string route, UserPreferenceCategory category)
    {
        Assert.True(AppearancePreferenceRules.TryCategory(route, out var parsed));
        Assert.Equal(category, parsed);
        var result = AppearancePreferenceRules.ToDto(null, category);
        Assert.Equal(category, result.Category);
        Assert.Equal(0, result.Revision);
        Assert.Null(result.UpdatedAtUtc);
        Assert.NotNull(AppearancePreferenceRules.Normalize(new(1, 0, result.Data), category));
    }

    [Theory]
    [InlineData("{\"colorScheme\":\"dark\",\"density\":\"compact\",\"textSize\":\"large\",\"contrast\":\"high\",\"motion\":\"off\"}", true)]
    [InlineData("{\"colorScheme\":\"dark\"}", false)]
    [InlineData("{\"colorScheme\":\"dark\",\"density\":\"wrong\",\"textSize\":\"large\",\"contrast\":\"high\",\"motion\":\"off\"}", false)]
    [InlineData("{\"colorScheme\":\"dark\",\"density\":\"compact\",\"textSize\":\"large\",\"contrast\":\"high\",\"motion\":\"off\",\"userId\":\"other\"}", false)]
    [InlineData("{\"colorScheme\":\"dark\",\"colorScheme\":\"light\",\"density\":\"compact\",\"textSize\":\"large\",\"contrast\":\"high\",\"motion\":\"off\"}", false)]
    public void DisplayUsesStrictWhitelist(string json, bool valid) =>
        Assert.Equal(valid, AppearancePreferenceRules.Normalize(Request(json), UserPreferenceCategory.Display).HasValue);

    [Fact]
    public void LibraryRejectsDuplicatesOversizeAndInvalidColors()
    {
        var sample = new { id = "existing-browser-id", name = "My color", preference = new {preset="custom",color="#AABBCC",background="neutral",emphasis="light"} };
        var request = new SaveUserPreferenceRequest(1, 0, JsonSerializer.SerializeToElement(new {samples=new[]{sample}}));
        var result = AppearancePreferenceRules.Normalize(request, UserPreferenceCategory.CustomThemeLibrary);
        Assert.Equal("#aabbcc", result!.Value.GetProperty("samples")[0].GetProperty("preference").GetProperty("color").GetString());
        Assert.Null(AppearancePreferenceRules.Normalize(request with {SchemaVersion=2}, UserPreferenceCategory.CustomThemeLibrary));
        Assert.Null(AppearancePreferenceRules.Normalize(request with {Data=JsonSerializer.SerializeToElement(new {samples=new[]{sample,sample}})}, UserPreferenceCategory.CustomThemeLibrary));
        Assert.Null(AppearancePreferenceRules.Normalize(request with {Data=JsonSerializer.SerializeToElement(new {samples=Enumerable.Range(0,13).Select(i=>new {id=i.ToString(),sample.name,sample.preference}).ToArray()})}, UserPreferenceCategory.CustomThemeLibrary));
        Assert.Null(AppearancePreferenceRules.Normalize(Request("{\"samples\":[{\"id\":\"one\",\"name\":\"Bad\",\"preference\":{\"preset\":\"custom\",\"color\":\"url(bad)\",\"background\":\"neutral\",\"emphasis\":\"light\"}}]}"), UserPreferenceCategory.CustomThemeLibrary));
    }
}
