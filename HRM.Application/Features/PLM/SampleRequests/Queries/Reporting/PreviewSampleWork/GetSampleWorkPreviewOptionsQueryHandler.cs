using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;

internal sealed class GetSampleWorkPreviewOptionsQueryHandler(IPLMReadDbContext db, ICurrentUser user)
    : IRequestHandler<GetSampleWorkPreviewOptionsQuery, SampleWorkPreviewOptionsDto?>
{
    public async Task<SampleWorkPreviewOptionsDto?> Handle(GetSampleWorkPreviewOptionsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !user.IsInRole(ApplicationRoles.Developer) || user.CompanyId is not { } company ||
            user.EmployeeId is not { } employee || !await db.Employees.AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct)) return null;
        var employees = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive)
            .OrderBy(x => x.FullName).Select(x => new SampleWorkPreviewEmployeeDto(x.EmployeeId, x.FullName, x.ExternalId)).ToListAsync(ct);
        return new(employees, [ApplicationRoles.Lab.LabUser, ApplicationRoles.Sales.SaleUser, ApplicationRoles.Lab.LabAdmin]);
    }
}
