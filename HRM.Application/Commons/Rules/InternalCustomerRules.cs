namespace HRM.Application.Commons.Rules;

/// <summary>
/// Quy tắc nhận diện khách nội bộ VietAus dùng chung cho report và các module nghiệp vụ.
/// </summary>
internal static class InternalCustomerRules
{
    public const string InternalCustomerExternalId = "KH_VIETAUS";

    public static bool IsInternalCustomerExternalId(string? externalId)
    {
        return string.Equals(
            externalId?.Trim(),
            InternalCustomerExternalId,
            StringComparison.OrdinalIgnoreCase);
    }
}
