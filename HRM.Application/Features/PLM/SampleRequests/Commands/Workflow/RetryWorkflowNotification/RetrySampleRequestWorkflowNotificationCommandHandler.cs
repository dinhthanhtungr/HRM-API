using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotification;

internal sealed class RetrySampleRequestWorkflowNotificationCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICustomerVisibilityService visibility) : IRequestHandler<RetrySampleRequestWorkflowNotificationCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(RetrySampleRequestWorkflowNotificationCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } actor)
            return OperationResult<Guid>.Fail("Current employee/company context is required.");
        var scope = await visibility.BuildScopeAsync(ct);
        if (!await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .AnyAsync(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive, ct))
            return OperationResult<Guid>.Fail("Workflow event is outside your scope.");
        var evt = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId &&
            x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.RecordedByEmployeeId == actor, ct);
        if (evt is null) return OperationResult<Guid>.Fail("Only the event recorder can retry this notification.");
        if (evt.NotificationId.HasValue) return OperationResult<Guid>.Ok(evt.Id);
        var idText = evt.Id.ToString();
        var companyText = company.ToString();
        var jobIds = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM notification.outbox_messages WHERE type={SampleRequestWorkflowNotificationQueue.OutboxType}
            AND payload_json->>'EventId'={idText} AND payload_json->>'CompanyId'={companyText}
            """).Where(x => x.ProcessedAt == null && x.Attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts)
            .Select(x => x.Id).ToListAsync(ct);
        var count = await db.OutboxMessages.Where(x => jobIds.Contains(x.Id) && x.ProcessedAt == null &&
            x.Attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, 0).SetProperty(x => x.Error, (string?)null), ct);
        return count > 0 ? OperationResult<Guid>.Ok(evt.Id) : OperationResult<Guid>.Fail("There is no failed notification to retry.");
    }
}
