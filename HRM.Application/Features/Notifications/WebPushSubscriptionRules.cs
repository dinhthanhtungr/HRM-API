namespace HRM.Application.Features.Notifications;

internal static class WebPushSubscriptionRules
{
    internal const int MaxEndpointLength = 2048;
    internal const int MaxP256dhLength = 512;
    internal const int MaxAuthLength = 256;
    internal const int MaxDeviceNameLength = 128;
    internal const int MaxUserAgentLength = 1024;

    internal static bool IsValidEndpoint(string? endpoint)
    {
        return !string.IsNullOrWhiteSpace(endpoint) &&
               endpoint.Length <= MaxEndpointLength &&
               Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TrimToMaxLength(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is { Length: > 0 }
            ? normalized[..Math.Min(normalized.Length, maxLength)]
            : null;
    }
}
