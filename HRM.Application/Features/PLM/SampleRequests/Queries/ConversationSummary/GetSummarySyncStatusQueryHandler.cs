using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.Queries.ConversationSummary;
internal sealed class GetSummarySyncStatusQueryHandler(ICurrentUser user, ICurrentUserPermissionService permissions,
    ISampleRequestConversationSummaryStore store) : IRequestHandler<GetSummarySyncStatusQuery, SummarySyncStatus?>
{
    public async Task<SummarySyncStatus?> Handle(GetSummarySyncStatusQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty || user.EmployeeId is null || user.EmployeeId == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs)) return null;
        return await store.StatusAsync(company, ct);
    }
}
