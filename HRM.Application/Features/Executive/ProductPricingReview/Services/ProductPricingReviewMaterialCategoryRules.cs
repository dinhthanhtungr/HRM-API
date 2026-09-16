using System.Globalization;
using System.Text;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal static class ProductPricingReviewMaterialCategoryRules
{
    public static PricingReviewMaterialCategoryGroup Resolve(string? categoryCode, string? categoryName)
    {
        var normalizedCode = Normalize(categoryCode);
        var normalizedName = Normalize(categoryName);

        if (normalizedCode is "PIG" or "BM" ||
            ContainsAny(normalizedName, "PIGMENT", "BOT MAU"))
            return PricingReviewMaterialCategoryGroup.Pigment;

        if (normalizedCode is "ADD" or "PG" ||
            ContainsAny(normalizedName, "ADDITIVE", "PHU GIA"))
            return PricingReviewMaterialCategoryGroup.Additive;

        if (normalizedCode is "VRG" or "NH" ||
            ContainsAny(normalizedName, "POLYMER", "RESIN", "NHUA"))
            return PricingReviewMaterialCategoryGroup.Resin;

        return PricingReviewMaterialCategoryGroup.Other;
    }

    public static string GetDisplayName(PricingReviewMaterialCategoryGroup group)
        => group switch
        {
            PricingReviewMaterialCategoryGroup.Pigment => "Bột màu",
            PricingReviewMaterialCategoryGroup.Additive => "Phụ gia",
            PricingReviewMaterialCategoryGroup.Resin => "Nhựa",
            _ => "Khác"
        };

    private static bool ContainsAny(string value, params string[] keywords)
        => keywords.Any(value.Contains);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'Đ' or 'đ' ? 'D' : character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}

internal sealed class PricingReviewFormulaItemCategory
{
    public Guid CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
}
