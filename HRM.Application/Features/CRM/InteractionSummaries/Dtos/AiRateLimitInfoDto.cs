namespace HRM.Application.Features.CRM.InteractionSummaries.Dtos;

/// <summary>
/// Snapshot quota theo phút/ngày của model AI đang dùng cho CRM summary.
/// </summary>
public sealed class AiRateLimitInfoDto
{
    public bool CanRequest { get; set; }
    public string Model { get; set; } = string.Empty;
    public int RetryAfterSeconds { get; set; }
    public DateTime? RetryAt { get; set; }
    public int RpmLimit { get; set; }
    public int RpmUsed { get; set; }
    public int RpmRemaining { get; set; }
    public int RpdLimit { get; set; }
    public int RpdUsed { get; set; }
    public int RpdRemaining { get; set; }
    public DateTime MinuteWindowStartsAt { get; set; }
    public DateTime MinuteWindowEndsAt { get; set; }
    public DateTime DayWindowStartsAt { get; set; }
    public DateTime DayWindowEndsAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
