namespace HRM.Application.Features.PLM.CustomerLabels.Services;

internal static class CustomerLabelRules
{
    public const int MaxShortTextLength = 200;
    public const int MaxFieldValueLength = 4_000;

    public static string? ValidateOptionalText(string? value, string name, int maxLength, bool rejectBlank)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return rejectBlank ? $"{name} must not be blank." : null;
        }

        return value.Trim().Length > maxLength ? $"{name} must not exceed {maxLength} characters." : null;
    }

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
