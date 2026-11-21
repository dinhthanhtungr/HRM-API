using System.Globalization;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

internal static class SampleBatchFeedbackMessage
{
    // Keep a preview of every non-empty note within the existing 2,000-character message limit.
    internal static string? Build(string requestCode, DateTime sentAt, IEnumerable<(string? Code, bool Approved, string? Note)> feedback)
    {
        static string Short(string text, int limit) => text.Length <= limit ? text : text[..(limit - 1)] + "…";
        var rows = feedback.Where(row => !string.IsNullOrWhiteSpace(row.Note)).ToArray();
        if (rows.Length == 0) return null;
        var header = $"Phản hồi mẫu {Short(requestCode, 48)} · Đợt {sentAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        var prefixes = rows.Select((row, i) => $"{Short(string.IsNullOrWhiteSpace(row.Code) ? $"Mẫu {i + 1}" : row.Code, 24)} · {(row.Approved ? "Đạt" : "Không đạt")}: ").ToArray();
        var budget = (2000 - header.Length - rows.Length - prefixes.Sum(prefix => prefix.Length)) / rows.Length;
        return header + "\n" + string.Join("\n", rows.Select((row, i) => prefixes[i] +
            Short(row.Note!.Trim().Replace("\r", " ").Replace("\n", " "), budget)));
    }
}
