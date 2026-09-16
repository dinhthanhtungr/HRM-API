using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HRM.Application.Commons.Pricing.Rules;

/// <summary>
/// Quy tắc giá vốn BTP nội bộ. Các giá trị được tập trung tại đây để thay đổi
/// theo quyết định nghiệp vụ mà không phải rải điều kiện tên/mã ở các calculator.
/// </summary>
public static partial class InternalMaterialCostingRules
{
    public const decimal GrindingCostPerKg = 5_000m;
    public const decimal DilutedPigmentRate = 0.70m;
    public const decimal ColorMasterbatchCostPerKg = 15_000m;
    public const decimal CompoundCostPerKg = 10_000m;

    public const string ColorMasterbatchCategoryCode = "CMB";
    public const string CompoundCategoryCode = "CMP";

    public static bool TryResolveMaterialRule(
        string? materialName,
        out InternalMaterialCostingRule rule,
        out string sourceNameKey)
    {
        var normalizedName = Normalize(materialName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            rule = InternalMaterialCostingRule.None;
            sourceNameKey = string.Empty;
            return false;
        }

        if (normalizedName.Contains("PHA LOANG", StringComparison.Ordinal))
        {
            rule = InternalMaterialCostingRule.DilutedPigment;
            sourceNameKey = NormalizeSourceName(normalizedName, InternalMaterialCostingRule.DilutedPigment);
            return !string.IsNullOrEmpty(sourceNameKey);
        }

        if (normalizedName.Contains("NGHIEN", StringComparison.Ordinal))
        {
            rule = InternalMaterialCostingRule.GroundResin;
            sourceNameKey = NormalizeSourceName(normalizedName, InternalMaterialCostingRule.GroundResin);
            return !string.IsNullOrEmpty(sourceNameKey);
        }

        rule = InternalMaterialCostingRule.None;
        sourceNameKey = string.Empty;
        return false;
    }

    public static decimal ApplyMaterialRule(InternalMaterialCostingRule rule, decimal sourcePrice) => rule switch
    {
        InternalMaterialCostingRule.GroundResin => sourcePrice + GrindingCostPerKg,
        InternalMaterialCostingRule.DilutedPigment => sourcePrice * DilutedPigmentRate,
        _ => sourcePrice
    };

    public static decimal GetProductManufacturingSurcharge(string? categoryExternalId) =>
        string.Equals(categoryExternalId, ColorMasterbatchCategoryCode, StringComparison.OrdinalIgnoreCase)
            ? ColorMasterbatchCostPerKg
            : string.Equals(categoryExternalId, CompoundCategoryCode, StringComparison.OrdinalIgnoreCase)
                ? CompoundCostPerKg
                : 0m;

    /// <summary>
    /// Tên sản phẩm được ưu tiên trước category: Compound có chữ "nghiền"
    /// vẫn phải áp chi phí nghiền, không áp phụ phí Compound.
    /// </summary>
    public static InternalProductCostAdjustment ResolveProductCostAdjustment(
        string? productName,
        string? categoryExternalId)
    {
        if (TryResolveMaterialRule(productName, out var materialRule, out _))
        {
            return new InternalProductCostAdjustment(materialRule, 0m);
        }

        return new InternalProductCostAdjustment(
            InternalMaterialCostingRule.None,
            GetProductManufacturingSurcharge(categoryExternalId));
    }

    public static decimal ApplyProductCostAdjustment(
        decimal formulaMaterialCost,
        InternalProductCostAdjustment adjustment) =>
        adjustment.MaterialRule != InternalMaterialCostingRule.None
            ? ApplyMaterialRule(adjustment.MaterialRule, formulaMaterialCost)
            : formulaMaterialCost + adjustment.SurchargePerKg;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return WhitespaceRegex().Replace(builder.ToString().ToUpperInvariant(), " ").Trim();
    }

    private static string NormalizeSourceName(string normalizedName, InternalMaterialCostingRule rule)
    {
        var value = rule == InternalMaterialCostingRule.GroundResin
            ? normalizedName.Replace("NGHIEN", string.Empty, StringComparison.Ordinal)
            : PercentageRegex().Replace(
                normalizedName.Replace("PHA LOANG", string.Empty, StringComparison.Ordinal),
                string.Empty);

        return NormalizeComparableName(value);
    }

    /// <summary>
    /// Bỏ phần mô tả dạng vật liệu để ghép bột/hạt với cùng tên nền.
    /// Ví dụ: "Bột PP trắng nghiền" và "Hạt PP trắng" cùng thành "PP TRANG".
    /// </summary>
    public static string NormalizeComparableName(string? value)
    {
        var normalized = Normalize(value)
            .Replace("NGUYEN CHAT", string.Empty, StringComparison.Ordinal);
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x is not "HAT" and not "BOT");
        return string.Join(' ', tokens);
    }

    [GeneratedRegex(@"\b\d+(?:[.,]\d+)?\s*%")]
    private static partial Regex PercentageRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

public enum InternalMaterialCostingRule
{
    None = 0,
    GroundResin = 1,
    DilutedPigment = 2
}

public readonly record struct InternalProductCostAdjustment(
    InternalMaterialCostingRule MaterialRule,
    decimal SurchargePerKg)
{
    public bool IsInternalCostRule => MaterialRule != InternalMaterialCostingRule.None || SurchargePerKg > 0m;
}
