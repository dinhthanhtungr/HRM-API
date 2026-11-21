namespace HRM.Api.Security.Authorization;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class AllowImsSessionAttribute : Attribute
{
    public bool AllowsSessionPost { get; init; }
}
