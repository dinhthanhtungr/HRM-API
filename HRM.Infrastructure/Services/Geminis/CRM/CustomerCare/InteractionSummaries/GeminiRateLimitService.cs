using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HRM.Infrastructure.Services.Geminis.CRM.CustomerCare.InteractionSummaries;

internal sealed class GeminiRateLimitService : IGeminiRateLimitService
{
    private static readonly ConcurrentDictionary<string, GeminiRateLimitBucket> Buckets = new();
    private readonly GeminiOptions _options;

    public GeminiRateLimitService(IOptions<GeminiOptions> options)
    {
        _options = options.Value;
    }

    public AiRateLimitInfoDto GetCurrent(string model)
    {
        var now = DateTime.Now;
        var bucket = GetBucket(model, now);

        lock (bucket.SyncRoot)
        {
            ResetExpiredWindows(bucket, now);
            return BuildInfo(bucket, model, now);
        }
    }

    public AiRateLimitInfoDto TryConsume(string model)
    {
        var now = DateTime.Now;
        var bucket = GetBucket(model, now);

        lock (bucket.SyncRoot)
        {
            ResetExpiredWindows(bucket, now);
            var info = BuildInfo(bucket, model, now);
            if (!info.CanRequest)
            {
                return info;
            }

            bucket.MinuteUsed++;
            bucket.DayUsed++;
            return BuildInfo(bucket, model, now);
        }
    }

    private GeminiRateLimitBucket GetBucket(string model, DateTime now)
    {
        var key = NormalizeModel(model);
        return Buckets.GetOrAdd(key, _ => new GeminiRateLimitBucket
        {
            MinuteWindowStartsAt = TruncateToMinute(now),
            DayWindowStartsAt = now.Date
        });
    }

    private static void ResetExpiredWindows(GeminiRateLimitBucket bucket, DateTime now)
    {
        var minuteStart = TruncateToMinute(now);
        if (bucket.MinuteWindowStartsAt != minuteStart)
        {
            bucket.MinuteWindowStartsAt = minuteStart;
            bucket.MinuteUsed = 0;
        }

        if (bucket.DayWindowStartsAt.Date != now.Date)
        {
            bucket.DayWindowStartsAt = now.Date;
            bucket.DayUsed = 0;
        }
    }

    private AiRateLimitInfoDto BuildInfo(GeminiRateLimitBucket bucket, string model, DateTime now)
    {
        var rpmLimit = Math.Max(1, _options.RateLimit.RequestsPerMinute);
        var rpdLimit = Math.Max(1, _options.RateLimit.RequestsPerDay);
        var rpmRemaining = Math.Max(0, rpmLimit - bucket.MinuteUsed);
        var rpdRemaining = Math.Max(0, rpdLimit - bucket.DayUsed);
        var minuteEndsAt = bucket.MinuteWindowStartsAt.AddMinutes(1);
        var dayEndsAt = bucket.DayWindowStartsAt.AddDays(1);
        var canRequest = rpmRemaining > 0 && rpdRemaining > 0;
        var retryAt = canRequest ? (DateTime?)null : rpmRemaining <= 0 ? minuteEndsAt : dayEndsAt;
        var retryAfterSeconds = retryAt.HasValue
            ? Math.Max(1, (int)Math.Ceiling((retryAt.Value - now).TotalSeconds))
            : 0;

        return new AiRateLimitInfoDto
        {
            CanRequest = canRequest,
            Model = string.IsNullOrWhiteSpace(model) ? _options.Model : model,
            RetryAfterSeconds = retryAfterSeconds,
            RetryAt = retryAt,
            RpmLimit = rpmLimit,
            RpmUsed = bucket.MinuteUsed,
            RpmRemaining = rpmRemaining,
            RpdLimit = rpdLimit,
            RpdUsed = bucket.DayUsed,
            RpdRemaining = rpdRemaining,
            MinuteWindowStartsAt = bucket.MinuteWindowStartsAt,
            MinuteWindowEndsAt = minuteEndsAt,
            DayWindowStartsAt = bucket.DayWindowStartsAt,
            DayWindowEndsAt = dayEndsAt,
            Message = canRequest
                ? $"Con {rpmRemaining}/{rpmLimit} request trong phut nay va {rpdRemaining}/{rpdLimit} request trong ngay."
                : rpmRemaining <= 0
                    ? $"Ban da dung het {rpmLimit} request/phut. Vui long thu lai sau {retryAfterSeconds} giay."
                    : $"Ban da dung het {rpdLimit} request/ngay. Vui long thu lai sau {retryAfterSeconds} giay."
        };
    }

    private string NormalizeModel(string model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? _options.Model.Trim().ToUpperInvariant()
            : model.Trim().ToUpperInvariant();
    }

    private static DateTime TruncateToMinute(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
    }

    private sealed class GeminiRateLimitBucket
    {
        public object SyncRoot { get; } = new();
        public DateTime MinuteWindowStartsAt { get; set; }
        public int MinuteUsed { get; set; }
        public DateTime DayWindowStartsAt { get; set; }
        public int DayUsed { get; set; }
    }
}
