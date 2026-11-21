using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.Commands.SyncConversationSummaries;
internal sealed class SyncConversationSummariesCommandHandler(ICurrentUser user, ICurrentUserPermissionService permissions,
    ISampleRequestConversationSummaryStore store) : IRequestHandler<SyncConversationSummariesCommand, SummaryBackfillPage?>
{
    public Task<SummaryBackfillPage?> Handle(SyncConversationSummariesCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty || user.EmployeeId is null || user.EmployeeId == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs) ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs) || request.Take is < 1 or > 100 ||
            request.SampleRequestId == Guid.Empty || request.Cursor == Guid.Empty) return Task.FromResult<SummaryBackfillPage?>(null);
        return QueueAsync(company);
        async Task<SummaryBackfillPage?> QueueAsync(Guid companyId) =>
            await store.EnqueuePageAsync(companyId, request.Cursor, request.Take, request.SampleRequestId, ct);
    }
}
