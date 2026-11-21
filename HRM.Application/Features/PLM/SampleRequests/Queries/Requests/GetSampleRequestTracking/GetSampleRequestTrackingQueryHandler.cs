using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;

internal sealed class GetSampleRequestTrackingQueryHandler(
    IPLMReadDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<GetSampleRequestTrackingQuery, SampleRequestTrackingDto?>
{
    public async Task<SampleRequestTrackingDto?> Handle(GetSampleRequestTrackingQuery request, CancellationToken cancellationToken)
    {
        if (SampleRequestTrackingReader.ResolveCompany(user, permissions) is not { } companyId ||
            request.SampleRequestId == Guid.Empty)
            return null;

        var query = SampleRequestTrackingReader.VisibleRequests(db, companyId)
            .Where(sample => sample.SampleRequestId == request.SampleRequestId);
        var header = await SampleRequestTrackingReader.Project(query).SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var milestones = await SampleRequestTrackingReader.ProjectMilestones(db, query, companyId)
            .ToListAsync(cancellationToken);
        return header with { Milestones = milestones };
    }
}
