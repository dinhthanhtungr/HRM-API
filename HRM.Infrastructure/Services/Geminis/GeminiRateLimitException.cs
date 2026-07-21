namespace HRM.Infrastructure.Services.Geminis;

internal sealed class GeminiRateLimitException : Exception
{
    public GeminiRateLimitException(string message, int? retryAfterSeconds = null)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int? RetryAfterSeconds { get; }
}
