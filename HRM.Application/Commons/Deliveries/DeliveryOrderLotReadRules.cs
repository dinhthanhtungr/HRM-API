namespace HRM.Application.Commons.Deliveries;

/// <summary>
/// Quy tắc đọc lot: consumption active là nguồn chuẩn; LotNoList chỉ dùng khi dòng lịch sử chưa có consumption.
/// </summary>
internal static class DeliveryOrderLotReadRules
{
    private static readonly char[] LegacySeparators = [',', ';', '|', '\r', '\n'];

    public static string? ResolveDisplay(
        IEnumerable<string> activeLotNumbers,
        string? legacyLotNoList)
    {
        var normalizedLots = activeLotNumbers
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedLots.Length > 0
            ? string.Join(", ", normalizedLots)
            : NormalizeLegacyDisplay(legacyLotNoList);
    }

    public static IReadOnlyList<string> SplitLegacy(string? legacyLotNoList)
        => string.IsNullOrWhiteSpace(legacyLotNoList)
            ? []
            : legacyLotNoList
                .Split(LegacySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? NormalizeLegacyDisplay(string? legacyLotNoList)
        => string.IsNullOrWhiteSpace(legacyLotNoList) ? null : legacyLotNoList.Trim();
}
