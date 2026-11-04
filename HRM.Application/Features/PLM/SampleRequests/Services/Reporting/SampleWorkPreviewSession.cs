using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

/// <summary>Scoped read-only preview. Never changes the authenticated principal, cookies or tokens.</summary>
public sealed class SampleWorkPreviewSession
{
    public ICurrentUser? User { get; internal set; }
    public ViewerScope? ActorReadScope { get; internal set; }
}

internal sealed record SampleWorkPreviewUser(Guid UserId, Guid? EmployeeId, Guid? CompanyId, string Persona) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? UserName => null;
    public string? Email => null;
    public IReadOnlyCollection<string> Roles => [Persona];
    public bool IsInRole(string role) => string.Equals(role, Persona, StringComparison.OrdinalIgnoreCase);
}
