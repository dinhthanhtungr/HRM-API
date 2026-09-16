namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Claim contract used to transport database-backed role permissions in access tokens.
/// </summary>
public static class ApplicationPermissionClaimTypes
{
    public const string Permission = "permission";
    public const string PermissionModelVersion = "permission-model";
    public const string CurrentModelVersion = "1";
}
