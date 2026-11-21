using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

internal sealed class RecordSampleOptionFeedbackCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, IDateTimeProvider clock, ICRMWriteDbContext crm,
    SampleDeliveryCompletion completion)
    : IRequestHandler<RecordSampleOptionFeedbackCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(RecordSampleOptionFeedbackCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) ||
            user.CompanyId is not { } company || user.EmployeeId is not { } employee ||
            command.OperationId == Guid.Empty || command.TrialId == Guid.Empty || command.ReplyDate == default ||
            command.ReplyDate > clock.Now.AddMinutes(5) || command.Note?.Length > 5000 || command.CorrectionReason?.Length > 2000)
            return Fail("Feedback input is invalid.");
        var scope = await visibility.BuildScopeAsync(ct);
        var parent = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (parent is null || !SampleSaleWorkflowAccess.CanActFor(user, permissions, parent.ManagerBy, scope) ||
            !await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == employee && e.CompanyId == company && e.IsActive, ct))
            return Fail("Sample request is outside your Sale workflow scope.");
        var replay = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == command.OperationId, ct);
        if (replay is not null)
        {
            var replayNote = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company &&
                e.SampleRequestId == command.SampleRequestId && e.RelatedEventId == command.OperationId && e.EventType == SampleRequestWorkflowEvents.FeedbackRecorded)
                .Select(e => e.Note).SingleOrDefaultAsync(ct);
            var replayReason = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company &&
                e.SampleRequestId == command.SampleRequestId && e.RelatedEventId == command.OperationId &&
                e.EventType == SampleDeliveryRules.OptionFeedbackCorrection).Select(e => e.Note).SingleOrDefaultAsync(ct);
            return replay.CompanyId == company && replay.SampleRequestId == command.SampleRequestId &&
                replay.EventType == SampleDeliveryRules.OptionFeedback && replay.RelatedEventId == command.BatchId && replay.SampleRequestSampleTrialId == command.TrialId &&
                replay.Note == (command.Approved ? "Approved" : "Failed") && replay.OccurredAt == command.ReplyDate && replayNote == command.Note?.Trim() &&
                replayReason == (string.IsNullOrWhiteSpace(command.CorrectionReason) ? null : command.CorrectionReason.Trim())
                ? OperationResult<Guid>.Ok(command.TrialId) : Fail("Operation id is already in use. Reload before retrying.");
        }
        if (!SampleFlowRules.IsOpen(parent.Status)) return Fail("The request is closed.");
        await using var transaction = command.UseExistingTransaction ? null : await db.BeginTransactionAsync(ct);
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == parent.SampleRequestId && x.CompanyId == company &&
            x.IsActive && x.UpdatedDate == command.ExpectedRequestUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return Fail("Request changed. Reload before saving.");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        var events = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company && e.SampleRequestId == parent.SampleRequestId)
            .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        if (!events.Any(e => e.Id == command.BatchId && e.EventType == SampleDeliveryRules.BatchSent))
            return Fail("Delivery batch was not found.");
        var ids = events.Where(e => e.RelatedEventId == command.BatchId && e.EventType == SampleRequestWorkflowEvents.SampleSent)
            .Select(e => e.SampleRequestSampleTrialId).ToArray();
        var trials = await db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == parent.SampleRequestId && t.IsActive)
            .OrderByDescending(t => t.TrialNo).ToListAsync(ct);
        if (trials.Count == 0 || !ids.Contains(trials[0].SampleRequestSampleTrialId))
            return Fail("Only the latest delivery batch can be changed before Lab proceeds.");
        var batch = trials.Where(t => ids.Contains(t.SampleRequestSampleTrialId)).ToArray();
        var selected = batch.SingleOrDefault(t => t.SampleRequestSampleTrialId == command.TrialId);
        if (selected is null || selected.SentDate is null || command.ReplyDate.Date < selected.SentDate.Value.Date ||
            selected.UpdatedDate != command.ExpectedTrialUpdatedDate)
            return Fail("Trial changed or feedback date is before delivery. Reload before saving.");
        var feedbackTargets = SampleDeliveryRules.FeedbackTargets(batch, selected, command.Approved, command.PreserveSiblingResults);
        var correction = !command.ImplicitFeedbackWithinBatch && (selected.CustomerReplyDate.HasValue ||
            command.Approved && feedbackTargets.Any(t => t != selected && t.Status == SampleTrialStatus.Approved));
        if (!correction && !string.IsNullOrWhiteSpace(command.CorrectionReason))
            return Fail("A correction reason is only used for recorded feedback.");
        if (correction && string.IsNullOrWhiteSpace(command.CorrectionReason))
            return Fail("A reason is required to correct recorded feedback.");
        if (correction &&
            !permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback))
            return Fail("You cannot correct recorded feedback.");
        // Không sửa hồi tố khi Lab đã xác nhận công thức mới hoặc đã có kế hoạch mới.
        var sentAt = events.Single(e => e.Id == command.BatchId).RecordedAt;
        if (events.Any(e => e.RecordedAt > sentAt && (e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed ||
            e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.ChipThenSampleRequested)))
            return Fail("Lab has proceeded or the delivery plan changed. Feedback correction is blocked.");
        var batchFormulaIds = batch.Where(t => t.FormulaId.HasValue).Select(t => t.FormulaId!.Value).ToArray();
        if (await db.FormulaVersions.AsNoTracking().AnyAsync(v => batchFormulaIds.Contains(v.FormulaId) && v.CreatedAt > sentAt &&
            v.ChangeReason != "Sample options sent", ct))
            return Fail("A sent formula has changed. Feedback correction is blocked.");
        var now = clock.Now;
        var oldValues = JsonSerializer.SerializeToDocument(batch.Select(t => new { t.SampleRequestSampleTrialId, t.Status, t.CustomerReplyDate, t.CustomerReplyNote }).ToArray());
        SampleDeliveryRules.ApplyFeedback(batch, selected, command.Approved, command.ReplyDate, command.Note?.Trim(), employee, now, command.PreserveSiblingResults);
        var winner = batch.Where(t => t.Status == SampleTrialStatus.Approved).OrderByDescending(t => t.TrialNo).FirstOrDefault();
        parent.FormulaId = winner?.FormulaId ?? selected.FormulaId;
        parent.Status = "SampleSent";
        var allFailed = winner is null && batch.All(t => t.Status == SampleTrialStatus.Failed);
        if (allFailed) SampleRequestStatusTransitionRules.MarkCustomerFailed(parent);
        // Chỉ mở công thức đại diện để làm lại khi cả đợt không đạt; option không được chọn
        // không tự mở sửa công thức khi một option khác vẫn đạt hoặc đang chờ.
        var formulaToChange = command.Approved ? selected.FormulaId : allFailed ? batch.OrderByDescending(t => t.TrialNo).First().FormulaId : null;
        if (formulaToChange.HasValue && (winner is null || command.DeferCompletion))
        {
            var formula = await db.Formulas.SingleOrDefaultAsync(f => f.FormulaId == formulaToChange && f.CompanyId == company && f.ProductId == parent.ProductId && f.IsActive, ct);
            if (formula is null || formula.Status is not ("SampleSent" or "Rework")) return Fail("Formula changed. Reload before saving feedback.");
            var formulaLocked = await db.Formulas.Where(f => f.FormulaId == formula.FormulaId && f.CompanyId == company && f.UpdatedDate == formula.UpdatedDate)
                .ExecuteUpdateAsync(set => set.SetProperty(f => f.UpdatedDate, f => f.UpdatedDate), ct);
            if (formulaLocked != 1) return Fail("Formula changed. Reload before saving feedback.");
            formula.Status = command.Approved ? "SampleSent" : "Rework";
            formula.UpdatedDate = now; formula.UpdatedBy = employee;
        }
        parent.UpdatedDate = now; parent.UpdatedBy = employee;
        if (winner is not null && !command.DeferCompletion &&
            !await completion.ApplyAsync(parent, winner, employee, now, ct))
            return Fail("Formula changed. Reload before saving feedback.");
        var operation = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now,
            SampleDeliveryRules.OptionFeedback, command.ReplyDate, command.Approved ? "Approved" : "Failed", selected.FormulaId, selected.SampleRequestSampleTrialId);
        operation.Id = command.OperationId; operation.RelatedEventId = command.BatchId;
        db.SampleRequestWorkflowEvents.Add(operation);
        if (correction)
        {
            var correctionEvent = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now,
                SampleDeliveryRules.OptionFeedbackCorrection, command.ReplyDate, command.CorrectionReason!.Trim(), selected.FormulaId, selected.SampleRequestSampleTrialId);
            correctionEvent.RelatedEventId = operation.Id;
            db.SampleRequestWorkflowEvents.Add(correctionEvent);
        }
        foreach (var trial in feedbackTargets)
        {
            var feedback = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now,
                trial == selected ? SampleRequestWorkflowEvents.FeedbackRecorded : SampleDeliveryRules.OptionNotSelected,
                command.ReplyDate, trial.CustomerReplyNote, trial.FormulaId, trial.SampleRequestSampleTrialId);
            feedback.RelatedEventId = operation.Id;
            db.SampleRequestWorkflowEvents.Add(feedback);
            if (trial == selected && !command.SuppressNotification) SampleRequestWorkflowNotificationQueue.Queue(db, feedback, new SendSampleRequestMessageCommand
            {
                SampleRequestId = parent.SampleRequestId, Type = SampleRequestNotificationType.GeneralMessage,
                Message = $"Phản hồi mẫu {parent.ExternalId}, VU {trial.BatchNo}: {(command.Approved ? "Đạt" : "Không đạt")}. {trial.CustomerReplyNote}"
            });
        }
        // Dùng cùng scoped DbContext/giao dịch như luồng phản hồi Trial hiện hành.
        crm.CustomerInteractions.Add(new CustomerInteraction
        {
            Id = command.OperationId, CompanyId = company, CustomerId = parent.CustomerId,
            AssignedSaleEmployeeId = parent.ManagerBy, InteractionType = CustomerInteractionType.SampleTrial,
            Subject = $"Phản hồi mẫu {parent.ExternalId}", Content = command.Note?.Trim() ?? "",
            Outcome = command.Approved ? "APPROVED" : "FAIL", InteractionAt = command.ReplyDate, CreatedBy = employee, CreatedDate = now
        });
        crm.CustomerInteractionReferences.Add(new CustomerInteractionReference
        {
            Id = Guid.CreateVersion7(), InteractionId = command.OperationId, CompanyId = company,
            ReferenceType = CustomerInteractionReferenceType.SampleTrial, ReferenceId = selected.SampleRequestSampleTrialId,
            ReferenceCodeSnapshot = parent.ExternalId, ReferenceNameSnapshot = selected.ProductNameSnapshot,
            IsPrimary = true, CreatedBy = employee, CreatedDate = now
        });
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = company, SchemaName = "SampleRequests", TableName = "SampleRequestSampleTrials", RecordId = selected.SampleRequestSampleTrialId,
            ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = now, CorrelationId = parent.SampleRequestId,
            Reason = correction ? command.CorrectionReason!.Trim() : SampleDeliveryRules.OptionFeedback, OldValues = oldValues,
            NewValues = JsonSerializer.SerializeToDocument(batch.Select(t => new { t.SampleRequestSampleTrialId, t.Status, t.CustomerReplyDate, t.CustomerReplyNote }).ToArray())
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(selected.SampleRequestSampleTrialId);
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
