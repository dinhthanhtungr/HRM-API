namespace HRM.Domain.Identity;

public static class IdentityAccountAccessRules
{
    public static bool IsLocked(bool lockoutEnabled, DateTimeOffset? lockoutEnd, DateTimeOffset now)
        => lockoutEnabled && lockoutEnd.HasValue && lockoutEnd.Value > now;
}
