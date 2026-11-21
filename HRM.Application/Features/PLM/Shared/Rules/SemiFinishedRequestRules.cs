namespace HRM.Application.Features.PLM.Shared.Rules;

/// <summary>Hồ sơ NVL gia công nội bộ; không phải yêu cầu phối mẫu của Sale/Lab.</summary>
public static class SemiFinishedRequestRules
{
    public const string RequestType = "SemiFinished";
    public static bool IsSemiFinished(string? value)
        => string.Equals(value?.Trim(), RequestType, StringComparison.OrdinalIgnoreCase);
}
