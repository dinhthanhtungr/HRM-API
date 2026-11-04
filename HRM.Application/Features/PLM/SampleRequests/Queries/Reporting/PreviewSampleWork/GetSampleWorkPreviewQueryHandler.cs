using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;

internal sealed class GetSampleWorkPreviewQueryHandler(IPLMReadDbContext db, ICurrentUser actor,
    ICustomerVisibilityService visibility, IServiceScopeFactory scopes)
    : IRequestHandler<GetSampleWorkPreviewQuery, SampleWorkPreviewDto?>
{
    public async Task<SampleWorkPreviewDto?> Handle(GetSampleWorkPreviewQuery request, CancellationToken ct)
    {
        if (!actor.IsAuthenticated || !actor.IsInRole(ApplicationRoles.Developer) || actor.CompanyId is not { } company ||
            actor.EmployeeId is not { } employee || !await db.Employees.AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct)) return null;
        var audience = request.Persona switch
        {
            ApplicationRoles.Lab.LabUser => SampleRequestWorkAudience.Lab,
            ApplicationRoles.Lab.LabAdmin => SampleRequestWorkAudience.LabAdmin,
            ApplicationRoles.Sales.SaleUser => SampleRequestWorkAudience.Sale,
            _ => throw new ArgumentException("Invalid preview persona.")
        };
        if (!Enum.IsDefined(request.Bucket) || request.LabTeam.HasValue && (!Enum.IsDefined(request.LabTeam.Value) || audience == SampleRequestWorkAudience.Sale) || request.Keyword?.Length > 80)
            throw new ArgumentException("Invalid preview filter.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == request.EmployeeId && x.IsActive, ct)) return null;
        var actorScope = await visibility.BuildReadScopeAsync(ct);
        using var scope = scopes.CreateScope();
        var preview = scope.ServiceProvider.GetRequiredService<SampleWorkPreviewSession>();
        preview.User = new SampleWorkPreviewUser(actor.UserId, request.EmployeeId, company, request.Persona);
        preview.ActorReadScope = actorScope;
        var report = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetSampleRequestDailyWorkQuery
        {
            Audience = audience, Bucket = request.Bucket, LabTeam = request.LabTeam, Keyword = request.Keyword,
            PageNumber = request.PageNumber, PageSize = request.PageSize
        }, ct);
        return new(request.Persona, request.EmployeeId, true, report);
    }
}
