using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

public static class ComplaintReportPdfRules
{
    public static bool ShouldShowDraftWatermark(ComplaintReportStatus status)
        => status != ComplaintReportStatus.Closed;

    public static string BuildFileName(string externalId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string((externalId ?? string.Empty)
            .Select(x => invalid.Contains(x) ? '-' : x)
            .ToArray()).Trim();
        return $"CAPA-{(safe.Length == 0 ? "report" : safe)}.pdf";
    }

    public static bool IsSupportedImage(ReadOnlySpan<byte> bytes)
        => IsPng(bytes) || IsJpeg(bytes);

    private static bool IsPng(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 8 && bytes[..8].SequenceEqual(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    private static bool IsJpeg(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 &&
            bytes[^2] == 0xFF && bytes[^1] == 0xD9;
}
