using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordCustomerFeedbackNote;

/// <summary>Appends explicitly shared customer feedback without changing price acceptance, request status or VU selection.</summary>
internal sealed class RecordCustomerFeedbackNoteCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility)
    : IRequestHandler<RecordCustomerFeedbackNoteCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(RecordCustomerFeedbackNoteCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow)) return Fail("You cannot record customer feedback.");
        if (command.ExpectedPriceEventId == Guid.Empty || string.IsNullOrWhiteSpace(command.CustomerFeedbackNote) ||
            command.CustomerFeedbackNote.Length > 2000) return Fail("Customer feedback is required and must not exceed 2000 characters.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct))
            return Fail("Employee is not active.");
        var scope = await visibility.BuildScopeAsync(ct);
        var allowedOwners = SampleSaleWorkflowAccess.GetAllowedOwnerIds(user, permissions, scope);
        var companyWide = SampleSaleWorkflowAccess.HasCompanyWideAccess(user, scope);
        var parents = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive &&
                (companyWide || allowedOwners.Contains(x.ManagerBy)) && x.Status != "Cancelled" && x.Status != "LabDeclined");
        var parent = await parents.Select(x => new { x.SampleRequestId, x.UpdatedDate }).SingleOrDefaultAsync(ct);
        if (parent is null) return Fail("Request is not accessible.");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        if (parent.UpdatedDate != command.ExpectedRequestUpdatedDate) return Fail("Request changed. Reload before saving.");
        var priceEvents = db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
            x.SampleRequestId == parent.SampleRequestId && SampleRequestPriceFeedbackVisibility.PriceEventTypes.Contains(x.EventType));
        var primary = await priceEvents.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (primary is null || primary.Id != command.ExpectedPriceEventId) return Fail("Price feedback changed. Reload before saving.");
        var note = command.CustomerFeedbackNote.Trim();
        var previous = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == parent.SampleRequestId && x.EventType == SampleFlowRules.PriceFeedbackShared && x.RelatedEventId == primary.Id)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Select(x => x.Note).FirstOrDefaultAsync(ct);
        if (note == previous) return OperationResult<Guid>.Ok(parent.SampleRequestId, "No feedback fields changed.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var now = DateTime.Now;
        var changed = await parents.Where(x => x.UpdatedDate == command.ExpectedRequestUpdatedDate &&
                priceEvents.OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.Id).FirstOrDefault() == command.ExpectedPriceEventId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, now).SetProperty(x => x.UpdatedBy, employee), ct);
        if (changed != 1) return Fail("Request or price feedback changed. Reload before saving.");
        var shared = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now,
            SampleFlowRules.PriceFeedbackShared, now, note, primary.FormulaId, primary.SampleRequestSampleTrialId);
        shared.RelatedEventId = primary.Id;
        db.SampleRequestWorkflowEvents.Add(shared);
        SampleRequestWorkflowNotificationQueue.Queue(db, shared, feedbackNoteUpdated: true);
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = company, SchemaName = "SampleRequests", TableName = "SampleRequests", RecordId = parent.SampleRequestId,
            ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = now,
            Reason = SampleFlowRules.PriceFeedbackShared, CorrelationId = parent.SampleRequestId,
            OldValues = JsonSerializer.SerializeToDocument(new { CustomerFeedbackNote = previous }),
            NewValues = JsonSerializer.SerializeToDocument(new { CustomerFeedbackNote = note })
        });
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Fail("Feedback changed concurrently. Reload before saving."); }
        return OperationResult<Guid>.Ok(parent.SampleRequestId);
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
