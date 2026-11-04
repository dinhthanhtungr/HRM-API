namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Bật sau khi người vận hành áp dụng migration; khi tắt không truy vấn bảng cảm xúc.</summary>
public sealed class InternalMailReactionOptions
{
    public const string SectionName = "InternalMail:Reactions";
    public bool Enabled { get; set; }
}
