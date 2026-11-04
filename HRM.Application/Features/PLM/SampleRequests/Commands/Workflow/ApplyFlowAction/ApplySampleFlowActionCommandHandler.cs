using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
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
    ISender sender, ICRMReadDbContext crm) : IRequestHandler<ApplySampleFlowActionCommand, OperationResult<Guid>>
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
        var scope = await visibility.BuildScopeAsync(ct);
        // A no-tracking customer subquery in restricted visibility also annotates the composed outer query.
        // Restore tracking after composition so request state and its concurrency token save with the event.
        var request = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .AsTracking()
            .SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (request is null || !SampleFlowRules.IsOpen(request.Status)) return Fail("Request is not accessible or has ended.");
        if (request.UpdatedDate != command.ExpectedUpdatedDate) return Fail("Request changed. Reload before saving.");
        var lab = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow);
        var sale = SampleSaleWorkflowAccess.CanActFor(user, permissions, request.ManagerBy, scope);
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
        var state = await SampleFlowStateReader.ReadAsync(db, crm, request, lab, sale, ct);
        if (!state.AllowedActions.Contains(command.Action) || command.Action == "Send")
            return Fail("This action is not available in the current step.");
        var now = DateTime.Now;
        var occurredAt = command.OccurredAt ?? now;
        if (occurredAt == default || occurredAt > now.AddMinutes(5)) return Fail("Feedback date is invalid.");
        var formula = state.FormulaId is { } id ? await db.Formulas.SingleOrDefaultAsync(x => x.FormulaId == id &&
            x.CompanyId == company && x.ProductId == request.ProductId && x.IsActive, ct) : null;
        if (command.Action is "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip")
        {
            if (formula is null) return Fail("Formula is required.");
            if ((formula.UpdatedDate ?? formula.CreatedDate) != command.ExpectedFormulaUpdatedDate)
                return Fail("Formula changed. Reload before saving.");
        }
        if (command.Action == "RequestSample" && (command.RequestedKg is not > 0 ||
            !double.IsFinite(command.RequestedKg.Value) || command.RequestedKg > 999999999))
            return Fail("Requested kg must be positive and within range.");
        if (command.Action is "PriceRejected" or "PriceNoResponse" &&
            (!request.RealPriceQuoteDate.HasValue || (string.IsNullOrWhiteSpace(command.Note) && string.IsNullOrWhiteSpace(command.CustomerFeedbackNote)) || string.IsNullOrWhiteSpace(command.PriceReference)))
            return Fail("Record the quotation date, price reference and feedback note first.");
        if (command.Action is "FinalizeChip" or "ReopenChip" && await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x =>
            x.IsActive && x.FormulaId == state.FormulaId && x.SampleRequest.CompanyId == company && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, ct))
            return Fail("This formula has already been sent in kg and cannot be reopened or finalized as chip work.");

        await using var transaction = await db.BeginTransactionAsync(ct);
        // Serialize request-level transitions across users/processes; rechecking the token is mandatory.
        var locked = await db.SampleRequests.Where(x => x.SampleRequestId == request.SampleRequestId &&
            x.CompanyId == company && x.UpdatedDate == command.ExpectedUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        if (locked != 1) return Fail("Request changed. Reload before saving.");
        if (formula is not null && command.Action is "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip")
        {
            var formulaLocked = await db.Formulas.Where(x => x.FormulaId == formula.FormulaId && x.CompanyId == company &&
                x.UpdatedDate == formula.UpdatedDate).ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
            if (formulaLocked != 1) return Fail("Formula changed. Reload before saving.");
        }
        var oldStatus = request.Status;
        var eventType = command.Action switch
        {
            "FinalizeQuotation" => SampleFlowRules.QuotationFinalized,
            "FinalizeChip" => SampleFlowRules.ChipFinalized,
            "ReopenChip" => SampleFlowRules.ChipReopened,
            "RequestSample" => SampleFlowRules.KgRequested,
            "Complete" => SampleFlowRules.CompletedWithoutTrial,
            "PriceRejected" => SampleFlowRules.PriceRejected,
            "PriceNoResponse" => SampleFlowRules.PriceNoResponse,
            _ => throw new InvalidOperationException("Unknown action.")
        };
        if (command.Action == "RequestSample") request.SampleQuantity = command.RequestedKg;
        if (command.Action is "FinalizeChip" or "FinalizeQuotation")
        {
            formula!.Status = FormulaStatus.Completed.ToString();
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
        }
        if (command.Action == "ReopenChip")
        {
            formula!.Status = FormulaStatus.Rework.ToString();
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
        }
        if (command.Action == "Complete")
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
            .OrderByDescending(x => x.TrialNo).Select(x => (Guid?)x.SampleRequestSampleTrialId).FirstOrDefaultAsync(ct);
        var evt = SampleRequestWorkflowEvents.Create(request.SampleRequestId, company, employee, now,
            eventType, occurredAt, command.Note?.Trim(), state.FormulaId, lastChip);
        if (command.Action is "PriceRejected" or "PriceNoResponse") evt.PriceReference = command.PriceReference?.Trim();
        db.SampleRequestWorkflowEvents.Add(evt);
        SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        if (command.Action is "PriceRejected" or "PriceNoResponse" &&
            SampleRequestWorkflowEvents.CreateSharedPriceFeedback(evt, command.CustomerFeedbackNote) is { } shared)
        {
            db.SampleRequestWorkflowEvents.Add(shared);
            SampleRequestWorkflowNotificationQueue.Queue(db, shared);
        }
        await SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(db.AuditLogs, request,
            oldStatus, employee, now, eventType, ct);
        try
        {
            if (command.Action is "FinalizeChip" or "FinalizeQuotation" or "Complete" or "ReopenChip")
                await versions.SaveSnapshotAsync(formula!, employee, now, eventType, true, ct);
            else await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Fail("Workflow or formula changed concurrently. Reload before saving.");
        }
        return OperationResult<Guid>.Ok(request.SampleRequestId);
    }

    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
