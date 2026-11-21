using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;

internal sealed class ApplySampleFlowActionCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, FormulaVersionService versions,
    ISender sender, ICRMReadDbContext crm, IDateTimeProvider? clock = null) : IRequestHandler<ApplySampleFlowActionCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(ApplySampleFlowActionCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty)
            return Fail("Employee and company are required.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive, ct))
            return Fail("Employee is not active.");
        if (command.CustomerFeedbackNote?.Length > 2000 || command.Note?.Length > 2000 || command.PriceReference?.Length > 500)
            return Fail("Feedback is too long.");
        var scope = command.Action == "ResumeAfterLabRejection"
            ? await visibility.BuildReadScopeAsync(ct) : await visibility.BuildScopeAsync(ct);
        // A no-tracking customer subquery in restricted visibility also annotates the composed outer query.
        // Restore tracking after composition so request state and its concurrency token save with the event.
        var request = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .AsTracking()
            .SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (request is null ||
            !SampleFlowRules.IsOpen(request.Status) && command.Action is not ("RequestChip" or "RequestSample" or ChipThenSampleRules.Action or "RequestRequote"))
            return Fail("Request is not accessible or has ended.");
        if (request.UpdatedDate != command.ExpectedUpdatedDate) return Fail("Request changed. Reload before saving.");
        var lab = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow);
        var sale = SampleSaleWorkflowAccess.CanActFor(user, permissions, request.ManagerBy, scope);
        if (command.Action == "ResumeAfterLabRejection")
            sale = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow);
        if (command.Action != "ResumeAfterLabRejection" && SampleFlowRules.IsOpen(request.Status) &&
            await SampleRequestLabPauseRules.IsPausedAsync(db, company, request.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        if (command.Action == "CancelRejectedRequest")
            sale = await SampleSaleWorkflowAccess.CustomersAllowedForCancellation(db.Customers.AsNoTracking(), user, permissions, scope)
                .AnyAsync(c => c.CustomerId == request.CustomerId, ct);
        if (command.Action == "PriceAccepted")
        {
            if (!sale) return Fail("You cannot record the customer price.");
            return await sender.Send(new HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice.ConfirmCustomerPriceCommand
            {
                SampleRequestId = request.SampleRequestId, ExpectedUpdatedDate = command.ExpectedUpdatedDate,
                CustomerAcceptedAt = command.OccurredAt, Note = command.Note, CustomerFeedbackNote = command.CustomerFeedbackNote, Reference = command.PriceReference,
                QuotationLineId = command.QuotationLineId
            }, ct);
        }
        if (command.FormulaId.HasValue && command.Action != "Complete") return Fail("Formula selection is only available when completing a request.");
        var state = await SampleFlowStateReader.ReadAsync(db, crm, request, lab, sale, ct, command.FormulaId);
        if ((!state.AllowedActions.Contains(command.Action) && !state.PlanActions.Contains(command.Action)) || command.Action == "Send")
            return Fail("This action is not available in the current step.");
        var now = clock?.Now ?? DateTime.Now;
        var followUp = SampleFollowUpRules.IsDeliveryPlan(command.Action) || command.Action == "RequestRequote";
        if (followUp && !await db.Products.AsNoTracking().AnyAsync(p => p.ProductId == request.ProductId && p.CompanyId == company && p.IsActive, ct))
            return Fail("Select an active product for this request before requesting additional work.");
        if (followUp && !SampleFlowRules.IsOpen(request.Status) && string.IsNullOrWhiteSpace(command.Note))
            return Fail("A reason is required to reopen this request.");
        if (command.Action is "RequestLowerPrice" or "CancelRejectedRequest" or "RejectFormula" or "ResumeAfterLabRejection" && string.IsNullOrWhiteSpace(command.Note))
            return Fail("A reason or instruction is required.");
        if (command.Action == "RequestRequote" && (state.FormulaId is null || state.FormulaUpdatedDate != command.ExpectedFormulaUpdatedDate || string.IsNullOrWhiteSpace(command.Note)))
            return Fail("A current formula and reason are required for a price review.");
        var occurredAt = command.OccurredAt ?? now;
        if (occurredAt == default || occurredAt > now.AddMinutes(5)) return Fail("Feedback date is invalid.");
        var formula = state.FormulaId is { } id ? await db.Formulas.SingleOrDefaultAsync(x => x.FormulaId == id &&
            x.CompanyId == company && x.ProductId == request.ProductId && x.IsActive, ct) : null;
        var changeCompletion = (request.Status == "Completed" || state.Stage is "FinalizeChipFormula" or "FinalizeQuotationFormula" or "AwaitingSaleDecision" or "QuotationAccepted" or "RequestFollowUp") &&
            command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action or "WaitRequest";
        var resumeFinalizedFormula = changeCompletion && formula?.Status == "Completed" &&
            command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action &&
            !await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x => x.IsActive && x.FormulaId == formula.FormulaId &&
                x.SampleRequest.CompanyId == company && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, ct);
        if (changeCompletion || resumeFinalizedFormula || command.Action is "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip" or "RequestChipFinalization")
        {
            if (formula is null && !SampleFollowUpRules.IsDeliveryPlan(command.Action)) return Fail("Formula is required.");
            if (formula is not null && (formula.UpdatedDate ?? formula.CreatedDate) != command.ExpectedFormulaUpdatedDate)
                return Fail("Formula changed. Reload before saving.");
        }
        if (command.Action is "RequestSample" or ChipThenSampleRules.Action && (command.RequestedKg is not > 0 ||
            !double.IsFinite(command.RequestedKg.Value) || command.RequestedKg > 999999999))
            return Fail("Requested kg must be positive and within range.");
        if (command.Action is "RequestChip" or ChipThenSampleRules.Action && command.RequestedSets is not (>= 1 and <= 30))
            return Fail("Requested chip sets must be between 1 and 30.");
        if (command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action or "RequestLabFinalization" or "RequestChipFinalization" or "WaitChipReply" or "WaitRequest" && command.Note?.Length > 1900)
            return Fail("The note to Lab must not exceed 1900 characters.");
        if (command.Action == "RequestLabFinalization" && string.IsNullOrWhiteSpace(command.Note))
            return Fail("A note to Lab is required.");
        if (command.Action is "RequestChip" or ChipThenSampleRules.Action or "RequestLabFinalization" &&
            await ChipThenSampleRules.BlockingPending(db.SampleRequestSampleTrials.AsNoTracking())
                .AnyAsync(x => x.SampleRequestId == request.SampleRequestId, ct))
            return Fail("Record feedback for the pending delivery before requesting another action.");
        if (command.Action is "PriceRejected" or "PriceNoResponse" &&
            (!request.RealPriceQuoteDate.HasValue || (string.IsNullOrWhiteSpace(command.Note) && string.IsNullOrWhiteSpace(command.CustomerFeedbackNote))))
            return Fail("Record the quotation date and feedback note first.");
        if (command.Action is "PriceRejected" or "PriceNoResponse" && command.QuotationLineId.HasValue)
        {
            var line = await db.QuotationLines.AsNoTracking().Where(x => x.QuotationLineId == command.QuotationLineId &&
                x.IsActive && x.SampleRequestId == request.SampleRequestId && x.ProductId == request.ProductId &&
                x.Quotation.IsActive && x.Quotation.CompanyId == company && x.Quotation.CustomerId == request.CustomerId &&
                (x.Quotation.Status == HRM.Domain.Enums.CustomerEnum.QuotationStatus.Sent ||
                 x.Quotation.Status == HRM.Domain.Enums.CustomerEnum.QuotationStatus.Negotiating ||
                 x.Quotation.Status == HRM.Domain.Enums.CustomerEnum.QuotationStatus.Accepted))
                .Select(x => new { x.Quotation.ExternalId, x.Quotation.Version, x.SortOrder }).SingleOrDefaultAsync(ct);
            if (line is null) return Fail("Select a sent quotation line of this request/customer/product.");
            command.PriceReference = $"{line.ExternalId} · v{line.Version} · line {line.SortOrder}";
        }
        if (command.Action is "FinalizeChip" or "ReopenChip" && await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x =>
            x.IsActive && x.FormulaId == state.FormulaId && x.SampleRequest.CompanyId == company && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, ct))
            return Fail("This formula has already been sent in kg and cannot be reopened or finalized as chip work.");

        await using var transaction = command.UseExistingTransaction ? null : await db.BeginTransactionAsync(ct);
        // Serialize request-level transitions across users/processes; rechecking the token is mandatory.
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == request.SampleRequestId &&
            x.CompanyId == company && x.UpdatedDate == command.ExpectedUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return Fail("Request changed. Reload before saving.");
        if (command.Action is "FinalizeQuotation" or "FinalizeChip")
        {
            var finalizationEvents = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company &&
                e.SampleRequestId == request.SampleRequestId).OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
            var finalizationTrials = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == request.SampleRequestId &&
                t.SampleRequest.CompanyId == company && t.IsActive).ToListAsync(ct);
            var error = SaleRequestedFinalizationRules.Validate(request, state.FormulaId, command.Action, finalizationEvents, finalizationTrials);
            if (error is not null) return Fail(error);
        }
        if (command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action)
        {
            var planEvents = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == request.SampleRequestId).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
            var planTrials = await db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestId == request.SampleRequestId &&
                x.SampleRequest.CompanyId == company && x.IsActive).ToListAsync(ct);
            if (SampleRequestPlanRules.IsUnchangedPendingPlan(request, planEvents, planTrials,
                command.Action, command.RequestedKg, command.RequestedSets))
                return Fail("The pending delivery plan and quantities are unchanged.");
        }
        if (formula is not null && (changeCompletion || resumeFinalizedFormula || command.Action is "RequestRequote" or "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip"))
        {
            var formulaLocked = await db.Formulas.Where(x => x.FormulaId == formula.FormulaId && x.CompanyId == company &&
                x.UpdatedDate == formula.UpdatedDate).ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
            if (formulaLocked != 1) return Fail("Formula changed. Reload before saving.");
        }
        var oldStatus = request.Status;
        if (command.Action == "Complete" && !await SampleFollowUpRules.IsLegacyAsync(db, request, ct))
        {
            var completionTrials = await db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestId == request.SampleRequestId &&
                x.SampleRequest.CompanyId == company && x.IsActive).OrderByDescending(x => x.TrialNo)
                .ThenByDescending(x => x.CreatedDate).ToListAsync(ct);
            var completionEvents = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == request.SampleRequestId).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
            var latest = DeliveryOptions.SampleDeliveryRules.Representative(completionTrials, completionEvents);
            if (KgSampleCompletionRules.RequiresPriceAcceptance(request, completionEvents) &&
                !SampleFlowStateReader.ActivePrice(completionEvents, formula?.FormulaId))
                return Fail("Customer price must be accepted for this request before completing it.");
            if (request.SampleQuantity > 0 && latest?.DeliveryKind != HRM.Domain.Enums.SampleRequests.SampleDeliveryKind.TrialSample)
                return Fail("An accepted kg sample is required before completing this request.");
            if (latest?.DeliveryKind == HRM.Domain.Enums.SampleRequests.SampleDeliveryKind.ColorChip &&
                latest.Status != HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Approved)
                return Fail("Chip feedback must be accepted before completing this request.");
            var batchId = completionEvents.FirstOrDefault(e => e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                e.SampleRequestSampleTrialId == latest?.SampleRequestSampleTrialId)?.RelatedEventId;
            var accepted = completionTrials.Where(t => t.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Approved &&
                batchId.HasValue && completionEvents.Any(e => e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                    e.RelatedEventId == batchId && e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId)).ToArray();
            if (accepted.Length > 1 && !command.FormulaId.HasValue)
                return Fail("Select one accepted sample before completing this request.");
            if (command.FormulaId.HasValue)
            {
                latest = accepted.FirstOrDefault(t => t.FormulaId == command.FormulaId);
                if (latest is null) return Fail("Select an accepted sample from the latest delivery batch.");
            }
            if (latest?.DeliveryKind == HRM.Domain.Enums.SampleRequests.SampleDeliveryKind.TrialSample)
            {
                var outcomes = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                    x.SampleRequestId == request.SampleRequestId).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
                if (!KgSampleCompletionRules.IsSatisfied(latest, formula?.FormulaId, outcomes, request))
                    return Fail("Both sample and price must be accepted for this request before completing it.");
                var feedbackLocked = await db.SampleRequestSampleTrials.Where(x => x.SampleRequestSampleTrialId == latest.SampleRequestSampleTrialId &&
                    x.IsActive && x.UpdatedDate == latest.UpdatedDate && x.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Approved)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
                if (feedbackLocked != 1) return Fail("Sample feedback changed. Reload before completing.");
            }
        }
        var oldRequestSnapshot = SampleRequestDataChangeAuditHelper.BuildSampleRequestAuditSnapshot(request);
        if (!SampleFlowRules.IsOpen(oldStatus) && followUp)
        {
            request.Status = "InProgress";
            request.ExpectedDeliveryDate = null;
        }
        if (followUp && (!SampleFlowRules.IsOpen(oldStatus) || await SampleFollowUpRules.IsLegacyAsync(db, request, ct)))
        {
            db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(request.SampleRequestId, company, employee, now,
                SampleFollowUpRules.Started, note: command.Note?.Trim()));
        }
        var eventType = command.Action switch
        {
            "RejectFormula" => SampleRequestWorkflowEvents.LabDeclined,
            "ResumeAfterLabRejection" => SampleRequestLabPauseRules.Resumed,
            "FinalizeQuotation" => SampleFlowRules.QuotationFinalized,
            "FinalizeChip" => SampleFlowRules.ChipFinalized,
            "ReopenChip" => SampleFlowRules.ChipReopened,
            "RequestSample" => SampleFlowRules.KgRequested,
            "RequestChip" => SampleFlowRules.ChipRequested,
            ChipThenSampleRules.Action => SampleFlowRules.ChipThenSampleRequested,
            "RequestLabFinalization" => SampleFlowRules.LabFinalizationRequested,
            "RequestChipFinalization" => SampleFlowRules.ChipFinalizationRequested,
            "WaitChipReply" => SampleFlowRules.ChipFollowUpDeferred,
            "WaitRequest" => SampleFlowRules.CompletionDeferred,
            "RequestLowerPrice" => SampleFlowRules.LowerPriceRequested,
            "WaitCustomerReply" => SampleFlowRules.PriceFollowUpDeferred,
            "CancelRejectedRequest" => SampleRequestWorkflowEvents.RequestCancelled,
            "Complete" => SampleFlowRules.CompletedWithoutTrial,
            "RequestRequote" => SampleFollowUpRules.RequoteRequested,
            "PriceRejected" => SampleFlowRules.PriceRejected,
            "PriceNoResponse" => SampleFlowRules.PriceNoResponse,
            _ => throw new InvalidOperationException("Unknown action.")
        };
        if (command.Action is "RequestSample" or ChipThenSampleRules.Action) request.SampleQuantity = command.RequestedKg;
        if (command.Action == "CancelRejectedRequest") request.Status = "Cancelled";
        if (command.Action == "RejectFormula") request.Status = "LabDeclined";
        if (command.Action is "RequestChip" or ChipThenSampleRules.Action)
        {
            // InfoType also stores chip sets. Preserve an existing quotation obligation before changing that label.
            if (SampleRequestCustomerPriceRules.IsQuotation(request.InfoType) &&
                !await db.SampleRequestWorkflowEvents.AsNoTracking().AnyAsync(x => x.CompanyId == company &&
                    x.SampleRequestId == request.SampleRequestId && x.EventType == SampleFlowRules.QuotationRequired, ct))
                db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(request.SampleRequestId, company,
                    employee, now, SampleFlowRules.QuotationRequired, formulaId: state.FormulaId));
            request.InfoType = $"Thẻ màu ({command.RequestedSets})";
            request.SampleQuantity = command.Action == ChipThenSampleRules.Action ? command.RequestedKg : 0;
        }
        // Reuse the completed VU for delivery; keep its finalized version in history.
        if (resumeFinalizedFormula)
        {
            formula!.Status = FormulaStatus.Approved.ToString();
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
        }
        if (command.Action == "ReopenChip")
        {
            formula!.Status = FormulaStatus.Rework.ToString();
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
        }
        if (command.Action is "Complete" or "FinalizeChip" or "FinalizeQuotation")
        {
            formula!.Status = FormulaStatus.Completed.ToString();
            request.FormulaId = formula!.FormulaId;
            request.Status = "Completed";
            var productFormulas = await db.Formulas.Where(x => x.CompanyId == company && x.ProductId == request.ProductId && x.IsActive).ToListAsync(ct);
            foreach (var candidate in productFormulas) candidate.IsSelect = candidate.FormulaId == formula.FormulaId;
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
        }
        request.UpdatedDate = now;
        request.UpdatedBy = employee;
        var lastChip = await db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestId == request.SampleRequestId && x.IsActive)
            .OrderByDescending(x => command.Action == "Complete" && x.FormulaId == state.FormulaId && x.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Approved)
            .ThenByDescending(x => x.TrialNo).Select(x => (Guid?)x.SampleRequestSampleTrialId).FirstOrDefaultAsync(ct);
        var evt = SampleRequestWorkflowEvents.Create(request.SampleRequestId, company, employee, now,
            eventType, occurredAt, command.Note?.Trim(), state.FormulaId, lastChip);
        if (changeCompletion)
            evt.RelatedEventId = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x =>
                x.CompanyId == company && x.SampleRequestId == request.SampleRequestId && x.FormulaId == state.FormulaId &&
                (x.EventType == SampleFlowRules.ChipFinalizationRequested || x.EventType == SampleFlowRules.LabFinalizationRequested ||
                 x.EventType == SampleFlowRules.ChipFinalized || x.EventType == SampleFlowRules.QuotationFinalized ||
                 x.EventType == SampleFlowRules.CompletionDeferred || x.EventType == SampleFlowRules.ChipThenSampleRequested || x.EventType == SampleFlowRules.ChipRequested || x.EventType == SampleFlowRules.KgRequested))
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (command.Action is "RequestLowerPrice" or "WaitCustomerReply" or "CancelRejectedRequest")
            evt.RelatedEventId = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == request.SampleRequestId && x.EventType == SampleFlowRules.PriceRejected)
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action)
            evt.Note = (command.Action == ChipThenSampleRules.Action
                ? $"Yêu cầu gửi chip: {command.RequestedSets} bộ kèm mẫu: {command.RequestedKg} kg."
                : command.Action == "RequestChip"
                ? $"Yêu cầu gửi chip: {command.RequestedSets} bộ."
                : $"Yêu cầu chạy mẫu: {command.RequestedKg} kg.") +
                (string.IsNullOrWhiteSpace(command.Note) ? "" : "\n" + command.Note.Trim());
        if (command.Action is "PriceRejected" or "PriceNoResponse")
        {
            evt.PriceReference = command.PriceReference?.Trim();
            evt.QuotationLineId = command.QuotationLineId;
        }
        db.SampleRequestWorkflowEvents.Add(evt);
        if (command.Action is "RejectFormula" or "ResumeAfterLabRejection")
        {
            evt.RelatedEventId = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == request.SampleRequestId &&
                (x.EventType == SampleRequestLabPauseRules.Rejected || x.EventType == SampleRequestLabPauseRules.Resumed))
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            var label = command.Action == "RejectFormula" ? "Lab từ chối yêu cầu" : "Sale mở lại yêu cầu";
            SampleRequestWorkflowNotificationQueue.Queue(db, evt, new HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage.SendSampleRequestMessageCommand
            { SampleRequestId = request.SampleRequestId, Type = HRM.Domain.Enums.SampleRequests.SampleRequestNotificationType.GeneralMessage,
                TitleOverride = $"{label} · {request.ExternalId}", Message = $"{label} {request.ExternalId}.\nLý do: {evt.Note}" });
        }
        else if (command.Action != "RequestRequote") SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        if (command.Action is "PriceRejected" or "PriceNoResponse" &&
            SampleRequestWorkflowEvents.CreateSharedPriceFeedback(evt, command.CustomerFeedbackNote) is { } shared)
        {
            db.SampleRequestWorkflowEvents.Add(shared);
            SampleRequestWorkflowNotificationQueue.Queue(db, shared);
        }
        await SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(db.AuditLogs, request,
            oldStatus, employee, now, eventType, ct);
        if (command.Action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action)
            await SampleRequestDataChangeAuditHelper.AddSampleRequestAuditIfChangedAsync(db, request, employee,
                now, evt.Id, oldRequestSnapshot, eventType, ct);
        try
        {
            if (resumeFinalizedFormula || command.Action is "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip")
                await versions.SaveSnapshotAsync(formula!, employee, now, eventType, true, ct);
            else await db.SaveChangesAsync(ct);
            if (command.Action == "RequestRequote")
            {
                if (state.FormulaId is not { } requoteId || state.FormulaUpdatedDate != command.ExpectedFormulaUpdatedDate)
                    return Fail("Select a valid formula and reload before requesting a price review.");
                var requote = await sender.Send(new HRM.Application.Features.PLM.Formulas.Commands.RequestFormulaRequote.RequestFormulaRequoteCommand(
                    requoteId, new() { SampleRequestId = request.SampleRequestId, Message = command.Note!, IsUrgent = command.IsUrgent }), ct);
                if (!requote.Success) return Fail(requote.Message ?? "Price review request failed.");
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Fail("Workflow or formula changed concurrently. Reload before saving.");
        }
        return OperationResult<Guid>.Ok(request.SampleRequestId);
    }

    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
