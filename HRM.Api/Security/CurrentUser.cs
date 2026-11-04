using System.Security.Claims;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Domain.Entities.Security;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkPreviewSession? _preview;

    public CurrentUser(IHttpContextAccessor httpContextAccessor,
        HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkPreviewSession? preview = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _preview = preview;
    }

    private ClaimsPrincipal? Principal => _preview?.User is { } user
        ? new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("companyId", user.CompanyId!.Value.ToString()),
            new Claim("employeeId", user.EmployeeId!.Value.ToString())
        }.Concat(user.Roles.Select(role => new Claim(ClaimTypes.Role, role))), "SampleWorkReadOnlyPreview"))
        : _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        TryReadGuid(ClaimTypes.NameIdentifier)
        ?? TryReadGuid(ClaimTypes.NameIdentifier.ToLowerInvariant())
        ?? TryReadGuid("sub")
        ?? Guid.Empty;

    public Guid? EmployeeId => TryReadGuid("employeeId");

    public Guid? CompanyId => TryReadGuid("companyId");

    public string? UserName =>
        ReadClaim(ClaimTypes.Name)
        ?? ReadClaim("unique_name")
        ?? ReadClaim("name");

    public string? Email =>
        ReadClaim(ClaimTypes.Email)
        ?? ReadClaim("email");

    public IReadOnlyCollection<string> Roles =>
        Principal is null
            ? Array.Empty<string>()
            : Principal.FindAll(ClaimTypes.Role)
                .Select(x => x.Value)
                .Concat(Principal.FindAll("role").Select(x => x.Value))
                .Concat(ReadCsvClaims("roles"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public IReadOnlyCollection<string> Permissions =>
        Principal is null
            ? Array.Empty<string>()
            : Principal.FindAll(ApplicationPermissionClaimTypes.Permission)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public bool HasExplicitPermissionSet =>
        Principal?.FindFirst(ApplicationPermissionClaimTypes.PermissionModelVersion)?.Value ==
        ApplicationPermissionClaimTypes.CurrentModelVersion;

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private string? ReadClaim(string claimType)
    {
        if (Principal is null)
        {
            return null;
        }

        return Principal.Claims
            .FirstOrDefault(x => string.Equals(x.Type, claimType, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private IEnumerable<string> ReadCsvClaims(string claimType)
    {
        if (Principal is null)
        {
            return Array.Empty<string>();
        }

        return Principal.FindAll(claimType)
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private Guid? TryReadGuid(string claimType)
    {
        var value = ReadClaim(claimType);

        return Guid.TryParse(value, out var guid)
            ? guid
            : null;
    }
}
