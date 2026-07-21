namespace HRM.Infrastructure.Services.Geminis;

internal sealed class GeminiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.5-flash-lite";
    public int TimeoutSeconds { get; set; } = 120;
    public GeminiRateLimitOptions RateLimit { get; set; } = new();
}

internal sealed class GeminiRateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 15;
    public int RequestsPerDay { get; set; } = 500;
}
