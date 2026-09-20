namespace HRM.Infrastructure.Authentication;

public sealed class RefreshTokenSessionOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Reuses one active refresh token across logins and browser tabs for the same account.
    /// Set to false to restore atomic refresh-token rotation.
    /// </summary>
    public bool ReuseActiveToken { get; set; }
}
