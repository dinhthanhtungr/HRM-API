using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HRM.Api.Hubs;

/// <summary>
/// Endpoint SignalR dùng để báo realtime có notification mới.
/// Client tự join group user/role từ JWT claim và chỉ nhận notification id.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var companyId = ReadClaim("companyId");
        var employeeId = ReadClaim("employeeId");
        var companyKey = Guid.TryParse(companyId, out var parsedCompanyId)
            ? parsedCompanyId.ToString("N")
            : null;

        // Tên group phải khớp tuyệt đối với OutboxProcessor: company dùng Guid:N, role viết hoa.
        if (!string.IsNullOrWhiteSpace(companyKey))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"company:{companyKey}");
        }

        if (Guid.TryParse(employeeId, out var parsedEmployeeId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{parsedEmployeeId}");
        }

        if (!string.IsNullOrWhiteSpace(companyKey))
        {
            foreach (var role in ReadRoles())
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{companyKey}:{role}");
            }
        }

        await base.OnConnectedAsync();
    }

    private string? ReadClaim(string type)
    {
        return Context.User?.Claims
            .FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private IEnumerable<string> ReadRoles()
    {
        if (Context.User is null)
        {
            return Array.Empty<string>();
        }

        return Context.User.Claims
            .Where(x =>
                x.Type == ClaimTypes.Role ||
                string.Equals(x.Type, "role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "roles", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
