using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;

internal sealed class RecoverReadyWorkflowNotificationsCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ISender sender)
    : IRequestHandler<RecoverReadyWorkflowNotificationsCommand, RecoverReadyWorkflowNotificationsResult?>
{
    public async Task<RecoverReadyWorkflowNotificationsResult?> Handle(RecoverReadyWorkflowNotificationsCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is null ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs) ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs)) return null;
        var code = request.SampleRequestCode?.Trim();
        if (code?.Length > 80) throw new ArgumentException("Invalid sample request code.");
        var ids = await WorkflowNotificationJobQueries.ReadyConfirmations(db.OutboxMessages, company, code).AsNoTracking()
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(50).Select(x => x.Id).ToListAsync(ct);
        var queued = 0;
        foreach (var id in ids)
        {
            var result = await sender.Send(new RetryWorkflowNotificationJobCommand(id, RequireReady: true), ct);
            if (result.Outcome == "Queued") queued++;
        }
        return new(ids.Count, queued, ids.Count - queued);
    }
}
