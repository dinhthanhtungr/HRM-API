namespace HRM.Application.Features.Groups;

internal static class GroupManagementRules
{
    public const int NameMaxLength = 200;
    public const int GroupTypeMaxLength = 100;

    public static bool CanRemoveLeader(int activeLeaderCount)
        => activeLeaderCount > 1;

    public static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
