using System.Security.Claims;
using System.Text.Json;
using HRM.Api.Security.Authorization;
using HRM.Application.Abstractions.Authentication;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Api.Security;

/// <summary>Developer acts through the target's real principal; never unions Developer permissions.</summary>
public sealed class DeveloperSampleWorkMiddleware(RequestDelegate next)
{
    public const string Header = "X-Sample-Work-Employee";

    public static bool IsSupportedPath(PathString path)
        => path.StartsWithSegments("/api/v1/plm") || path.StartsWithSegments("/api/v1/crm") ||
            path.StartsWithSegments("/api/v1/attachments") || path.StartsWithSegments("/api/v1/message-recipients");

    public async Task InvokeAsync(HttpContext context, IIdentityAuthenticationService identities, IServiceScopeFactory scopes)
    {
        if (!context.Request.Headers.ContainsKey(Header)) { await next(context); return; }
        var original = context.User;
        if (original.Identity?.IsAuthenticated != true || !original.IsInRole(ApplicationRoles.Developer) ||
            ImsSessionAuthorizationMiddleware.HasImsRole(original) ||
            !IsSupportedPath(context.Request.Path) ||
            !Guid.TryParse(original.FindFirstValue(ClaimTypes.NameIdentifier) ?? original.FindFirstValue("sub"), out var actorId) ||
            !Guid.TryParse(original.FindFirstValue("companyId"), out var company) ||
            !Guid.TryParse(original.FindFirstValue("employeeId"), out var actorEmployee) ||
            !Guid.TryParse(context.Request.Headers[Header].ToString(), out var employee))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        var target = await identities.ResolveDeveloperSampleWorkUserAsync(actorId, company, employee, context.RequestAborted);
        if (target is null || target.Roles.Contains(ApplicationRoles.InternalSystems.IMSUser, StringComparer.OrdinalIgnoreCase))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, target.UserId.ToString()), new("employeeId", employee.ToString()),
            new("companyId", company.ToString()), new(ClaimTypes.Name, target.UserName ?? string.Empty)
        };
        claims.AddRange(target.Roles.Select(x => new Claim(ClaimTypes.Role, x)));
        if (target.UsesDatabasePermissions)
        {
            claims.Add(new(ApplicationPermissionClaimTypes.PermissionModelVersion, ApplicationPermissionClaimTypes.CurrentModelVersion));
            claims.AddRange(target.Permissions.Select(x => new Claim(ApplicationPermissionClaimTypes.Permission, x)));
        }
        // Persist attribution before allowing a real write. Do not log input, notes, tokens or prices.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
        {
            using var auditScope = scopes.CreateScope();
            var db = auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AuditLogs.Add(new AuditLog
            {
                CompanyId = company, SchemaName = "dev", TableName = "SampleWorkActingRequests",
                RecordId = Guid.CreateVersion7(), ActionType = AuditActionType.Create,
                ChangedBy = actorEmployee, ChangedAt = DateTime.Now, Reason = "DeveloperSampleWorkWriteAttempt",
                NewValues = JsonSerializer.SerializeToDocument(new
                {
                    ActorUserId = actorId, TargetUserId = target.UserId, TargetEmployeeId = employee,
                    Method = context.Request.Method, Path = context.Request.Path.Value,
                    TraceId = context.TraceIdentifier
                })
            });
            await db.SaveChangesAsync(context.RequestAborted);
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DeveloperSampleWork"));
        try { await next(context); }
        finally { context.User = original; }
    }
}
