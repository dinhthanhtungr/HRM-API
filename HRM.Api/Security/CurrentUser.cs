using System.Security.Claims;
using HRM.Application.Abstractions.Security;

namespace HRM.Api.Security;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

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
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

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

    private Guid? TryReadGuid(string claimType)
    {
        var value = ReadClaim(claimType);

        return Guid.TryParse(value, out var guid)
            ? guid
            : null;
    }
}
