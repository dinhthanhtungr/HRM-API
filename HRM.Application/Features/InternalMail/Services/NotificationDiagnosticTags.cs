namespace HRM.Application.Features.InternalMail.Services;

public static class NotificationDiagnosticTags
{
    public const string ServerTiming = "internal-mail.server-timing";
    public const string ThreadTiming = "internal-mail.thread-timing";
    internal static readonly HashSet<string> HeaderStages = ["unread-keys", "unread-filter", "unread-latest", "total-count", "page", "icons", "badges",
        "group-info", "parents", "sample-info", "quotation-info", "quick-groups", "masking", "quotation-threads",
        "summary-legacy", "summary-reactions", "summary-topics", "personal-message-keys", "personal-event-keys", "personal-conversation-keys", "personal-latest", "feed-scope", "feed-page", "reactions", "titles", "conversation-metadata",
        "hub-feed", "hub-conversations", "hub-badges", "hub-groups", "hub-parents",
        "hub-samples", "hub-quotations", "hub-quick-groups", "hub-masking",
        "urgent-conversation-keys", "urgent-event-keys", "hub-candidate-events", "hub-candidate-conversations",
        "thread-access", "thread-page", "thread-badges", "thread-areas", "thread-roles",
        "thread-visible-members", "thread-eligible-members", "thread-reader-groups",
        "read-access", "read-inbox-scope", "summary-scope", "read-event-snapshot", "read-membership", "read-boundary", "read-legacy-check",
        "read-message-snapshot", "read-begin", "read-messages", "read-notifications", "read-participant",
        "read-commit", "read-inbox-update", "read-inbox-legacy", "read-write-total",
        "read-state-access", "read-state-groups", "read-state-counts", "read-state-latest", "read-state-total"];
    private static readonly HashSet<string> Metrics = HeaderStages.Select(stage => "nm_" + stage.Replace('-', '_'))
        .Append("nm_handler").ToHashSet(StringComparer.Ordinal);

    public static string? ValidateServerTiming(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 2048 || value.Any(char.IsControl)) return null;
        var entries = value.Split(',');
        if (entries.Length > Metrics.Count) return null;
        var normalized = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Trim().Split(";dur=", StringSplitOptions.None);
            if (fields.Length != 2 || !Metrics.Contains(fields[0]) ||
                !long.TryParse(fields[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration) || duration < 0) return null;
            normalized.Add(fields[0] + ";dur=" + duration.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return string.Join(", ", normalized);
    }
}
