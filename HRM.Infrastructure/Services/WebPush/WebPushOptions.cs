namespace HRM.Infrastructure.Services.WebPush;

public sealed class WebPushOptions
{
    public bool Enabled { get; set; }
    public string VapidSubject { get; set; } = string.Empty;
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
}
