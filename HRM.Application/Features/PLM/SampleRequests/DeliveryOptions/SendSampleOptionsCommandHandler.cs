using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

internal sealed class SendSampleOptionsCommandHandler(IPLMWriteDbContext db, ICRMReadDbContext crm, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, IDateTimeProvider clock,
    FormulaVersionService versions) : IRequestHandler<SendSampleOptionsCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(SendSampleOptionsCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow) ||
            user.CompanyId is not { } company || user.EmployeeId is not { } employee)
            return Fail("You cannot send samples.");
        if (command.BatchId == Guid.Empty || command.Options is null || command.Options.Count is < 1 or > 30 ||
            command.Options.Any(o => o.FormulaId == Guid.Empty || o.QuantityKg is <= 0 or > 999999999 || decimal.Round(o.QuantityKg, 4) != o.QuantityKg) ||
            command.Options.Select(o => o.FormulaId).Distinct().Count() != command.Options.Count ||
            command.SentDate == default || command.SentDate > clock.Now.AddMinutes(5))
            return Fail("Select 1 to 30 distinct formulas with positive kg and a valid delivery date.");
        if (!await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == employee && e.CompanyId == company && e.IsActive, ct))
            return Fail("Current employee is not active in this company.");
        var scope = await visibility.BuildScopeAsync(ct);
        var parent = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .Include(x => x.Customer).Include(x => x.Product).ThenInclude(x => x.Category)
            .SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (parent is null) return Fail("Sample request is not accessible.");
        var replay = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == command.BatchId && e.CompanyId == company &&
            e.SampleRequestId == parent.SampleRequestId && e.EventType == SampleDeliveryRules.BatchSent, ct);
        if (replay is not null)
        {
            var sentTrials = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == parent.SampleRequestId &&
                db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == company && e.SampleRequestId == parent.SampleRequestId &&
                    e.EventType == SampleRequestWorkflowEvents.SampleSent && e.RelatedEventId == command.BatchId && e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId))
                .Select(t => new { t.FormulaId, t.DeliveredSampleQuantityKg, t.LabNote }).ToArrayAsync(ct);
            return replay.OccurredAt == command.SentDate && sentTrials.Length == command.Options.Count && command.Options.All(o =>
                sentTrials.Any(t => t.FormulaId == o.FormulaId && t.DeliveredSampleQuantityKg == o.QuantityKg &&
                    (o.Note == null || t.LabNote == o.Note.Trim())))
                ? OperationResult<Guid>.Ok(command.BatchId) : Fail("Batch id already used with different options. Reload before retrying.");
        }
        if (!SampleFlowRules.IsOpen(parent.Status) || await SampleFollowUpRules.IsLegacyAsync(db, parent, ct))
            return Fail("This request cannot receive a batch delivery.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == parent.SampleRequestId && x.CompanyId == company &&
            x.IsActive && x.UpdatedDate == command.ExpectedRequestUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return Fail("Request changed. Reload before sending.");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        var ids = command.Options.Select(o => o.FormulaId).ToArray();
        var formulas = await db.Formulas.Where(f => ids.Contains(f.FormulaId) && f.CompanyId == company &&
            f.ProductId == parent.ProductId && f.IsActive && f.Product.IsActive).ToDictionaryAsync(f => f.FormulaId, ct);
        if (formulas.Count != ids.Length) return Fail("A selected formula is not accessible.");
        if (formulas[command.Options[0].FormulaId].Status == "Draft")
            return Fail("The primary sample must use a confirmed formula. Draft formulas may only accompany it.");
        var primaryFlow = await SampleFlowStateReader.ReadAsync(db, crm, parent, true, false, ct, command.Options[0].FormulaId);
        if (primaryFlow.DeliveryKind != "TrialSample" || !primaryFlow.AllowedActions.Contains("Send"))
            return Fail("The primary formula is not ready for sample delivery.");
        var latestTrial = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == parent.SampleRequestId && t.IsActive)
            .OrderByDescending(t => t.TrialNo).FirstOrDefaultAsync(ct);
        var retryFormulaId = latestTrial is { Status: SampleTrialStatus.Failed, DeliveredSampleQuantityKg: > 0, SentDate: not null }
            ? latestTrial.FormulaId : null;
        var eligibleIds = await SampleDeliveryRules.SendableFormulas(db.Formulas.AsNoTracking(), db.SampleRequestSampleTrials, parent, retryFormulaId)
            .Where(f => ids.Contains(f.FormulaId)).Select(f => f.FormulaId).ToArrayAsync(ct);
        foreach (var option in command.Options.OrderBy(o => o.FormulaId))
        {
            var formula = formulas[option.FormulaId];
            if (!eligibleIds.Contains(formula.FormulaId) || FormulaConcurrencyRules.HasExpectedUpdatedDateConflictWithDatabasePrecision(option.ExpectedUpdatedDate, formula.UpdatedDate ?? formula.CreatedDate))
                return Fail("A formula changed or is not confirmed. Reload before sending.");
            if (await ChipThenSampleRules.BlockingPending(db.SampleRequestSampleTrials.AsNoTracking(), primaryFlow.ChipBeforeSample)
                .AnyAsync(t => t.SampleRequestId == parent.SampleRequestId, ct))
                return Fail("Record feedback for the pending delivery before sending another batch.");
            if (!FormulaSampleSentRules.CanRetryFailedKg(latestTrial, parent.SampleRequestId, formula.FormulaId) &&
                await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(t => t.FormulaId == formula.FormulaId && t.IsActive &&
                t.SentDate != null && t.DeliveredSampleQuantityKg > 0 && t.SampleRequest.CompanyId == company, ct))
                return Fail("A selected formula has already been sent in kg. Select a new confirmed formula.");
            var formulaLocked = await db.Formulas.Where(f => f.FormulaId == formula.FormulaId && f.CompanyId == company &&
                f.UpdatedDate == formula.UpdatedDate && f.Status == formula.Status)
                .ExecuteUpdateAsync(set => set.SetProperty(f => f.UpdatedDate, f => f.UpdatedDate), ct);
            if (formulaLocked != 1) return Fail("Formula changed. Reload before sending.");
        }
        var now = clock.Now;
        var planEvents = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company &&
            e.SampleRequestId == parent.SampleRequestId).OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        var previousTrials = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == parent.SampleRequestId && t.IsActive).ToListAsync(ct);
        var includeChip = ChipThenSampleRules.NeedsChipRecord(parent, null, planEvents, previousTrials);
        var nextNo = (await db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == parent.SampleRequestId)
            .Select(t => (int?)t.TrialNo).MaxAsync(ct) ?? 0) + 1;
        var batch = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now, SampleDeliveryRules.BatchSent, command.SentDate);
        batch.Id = command.BatchId;
        db.SampleRequestWorkflowEvents.Add(batch);
        foreach (var option in command.Options)
        {
            var formula = formulas[option.FormulaId];
            var chipNo = includeChip ? nextNo++ : (int?)null;
            var trial = new SampleRequestSampleTrial
            {
                SampleRequestSampleTrialId = Guid.CreateVersion7(), SampleRequestId = parent.SampleRequestId,
                FormulaId = formula.FormulaId, TrialNo = nextNo++, Status = SampleTrialStatus.WaitingCustomerFeedback, CustomerReplyStatus = "WAITING",
                DeliveredSampleQuantityKg = option.QuantityKg, SentDate = command.SentDate, SentByEmployeeId = employee,
                CreatedDate = now, CreatedBy = employee, UpdatedDate = now, UpdatedBy = employee,
                CustomerNameSnapshot = parent.Customer.CustomerName, SampleRequestExternalIdSnapshot = parent.ExternalId,
                ProductNameSnapshot = parent.Product.Name, ColourCodeSnapshot = parent.Product.ColourCode,
                CategoryNameSnapshot = parent.Product.Category?.Name, BatchNo = formula.ExternalId,
                LabNote = (option.Note ?? formula.Note)?.Trim()
            };
            db.SampleRequestSampleTrials.Add(trial);
            var sent = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, employee, now,
                SampleRequestWorkflowEvents.SampleSent, command.SentDate, formulaId: formula.FormulaId, trialId: trial.SampleRequestSampleTrialId);
            sent.RelatedEventId = batch.Id;
            db.SampleRequestWorkflowEvents.Add(sent);
            if (chipNo.HasValue) CombinedSampleDelivery.AddChip(db, parent, trial, chipNo.Value, employee, now, sent.Id);
            if (option == command.Options[0])
                SampleRequestWorkflowNotificationQueue.Queue(db, sent, new SendSampleRequestMessageCommand
                {
                    SampleRequestId = parent.SampleRequestId, Type = SampleRequestNotificationType.GeneralMessage,
                    TopicOverride = TopicNotifications.SampleRequestSampleSent,
                    Message = $"Lab đã gửi {command.Options.Count} option{(includeChip ? " kèm chip" : "")} cho {parent.ExternalId}: {string.Join(", ", command.Options.Select(o => formulas[o.FormulaId].ExternalId))}. Sale vui lòng ghi nhận phản hồi mẫu."
                });
            formula.Status = "SampleSent"; formula.SentDate = command.SentDate; formula.SentBy = employee;
            formula.UpdatedDate = now; formula.UpdatedBy = employee;
        }
        parent.Status = "SampleSent"; parent.UpdatedDate = now; parent.UpdatedBy = employee;
        foreach (var formula in formulas.Values.OrderBy(f => f.FormulaId))
            await versions.SaveSnapshotAsync(formula, employee, now, "Sample options sent", true, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(batch.Id);
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
