namespace HRM.Application.Features.Notifications.Dtos;

public sealed class WebPushPublicKeyDto
{
    public string PublicKey { get; set; } = string.Empty;
}

public sealed class WebPushSubscriptionDto
{
    public Guid SubscriptionId { get; set; }
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WebPushSubscriptionKeysDto
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

public sealed class UnsubscribeWebPushRequestDto
{
    public string Endpoint { get; set; } = string.Empty;
}
