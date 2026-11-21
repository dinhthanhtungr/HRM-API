using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordQuotationFeedback;

/// <summary>One atomic save; existing commands continue enforcing price readiness, ownership and field rights.</summary>
internal sealed class RecordSampleQuotationFeedbackCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, ISender sender, ICRMWriteDbContext crm,
    ILogger<RecordSampleQuotationFeedbackCommandHandler>? logger = null)
    : IRequestHandler<RecordSampleQuotationFeedbackCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(RecordSampleQuotationFeedbackCommand command, CancellationToken ct)
    {
        using var timings = new SampleWorkQueryTimings(
            logger ?? NullLogger<RecordSampleQuotationFeedbackCommandHandler>.Instance, true, "quotation");
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } employee ||
            !await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive, ct))
            return Fail("Active employee and company are required.");
        timings.Mark("actor");
        var scope = await visibility.BuildScopeAsync(ct);
        var record = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers.AsNoTracking(), scope)
            .AsTracking().SingleOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (record is null ||
            !SampleSaleWorkflowAccess.CanActFor(user, permissions, record.ManagerBy, scope))
            return Fail("Sample request is outside your scope or has ended.");
        timings.Mark("visibility");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, record.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        if (command.Feedback is not null && string.IsNullOrWhiteSpace(command.Feedback.CustomerFeedbackNote))
            return Fail("Customer feedback shared with Lab is required.");
        var interactionId = QuotationFeedbackInteraction.Id(company, employee, command);
        if (interactionId.HasValue && await QuotationFeedbackInteraction.ExistsAsync(crm, interactionId.Value, record, employee, ct))
            return OperationResult<Guid>.Ok(record.SampleRequestId);
        if (!SampleFlowRules.IsOpen(record.Status)) return Fail("Sample request has ended.");
        if (record.UpdatedDate != command.ExpectedUpdatedDate)
            return Fail("Sample request changed. Reload before saving.");
        var quotedAt = command.QuotedAt ?? record.RealPriceQuoteDate;
        var feedback = command.Feedback;
        if (quotedAt is null || quotedAt == default(DateTime) || quotedAt > DateTime.Now.AddMinutes(5))
            return Fail("Record a valid quotation date first.");
        if (feedback is not null && (feedback.Outcome is not ("Accepted" or "Rejected" or "NoResponse") ||
            feedback.Reference?.Length > 500 || feedback.Note?.Length > 2000 || feedback.CustomerFeedbackNote?.Length > 2000 ||
            (feedback.Outcome != "NoResponse" && !feedback.OccurredAt.HasValue) ||
            feedback.OccurredAt?.Date < quotedAt.Value.Date || feedback.OccurredAt > DateTime.Now.AddMinutes(5)))
            return Fail("Customer feedback or its date is invalid.");
        var dateChanged = command.QuotedAt.HasValue && record.RealPriceQuoteDate != quotedAt;
        var customer = interactionId.HasValue
            ? await visibility.ApplyCustomerVisibility(crm.Customers, scope).AsTracking()
                .SingleOrDefaultAsync(x => x.CustomerId == record.CustomerId && x.CompanyId == company && x.IsActive == true, ct)
            : null;
        if (interactionId.HasValue && customer is null) return Fail("Customer is outside your CRM scope.");
        if (!dateChanged && feedback is null) return OperationResult<Guid>.Ok(record.SampleRequestId);
        timings.Mark("validation");
        await using var transaction = await db.BeginTransactionAsync(ct);
        timings.Mark("begin");
        try
        {
            if (dateChanged)
            {
                var patch = await sender.Send(new PatchSampleRequestCommand
                {
                    SampleRequestId = record.SampleRequestId, RealPriceQuoteDate = quotedAt,
                    ExpectedUpdatedDate = record.UpdatedDate, DeferSaveChanges = true
                }, ct);
                if (!patch.Success) return patch;
                await db.SaveChangesAsync(ct);
            }
            timings.Mark("quotation-date");
            // Nested handlers resolve this same tracked aggregate. A scalar SQL read rounds
            // DateTime's 100ns ticks to PostgreSQL microseconds, while tracking keeps the
            // in-memory value after SaveChanges. Pass the tracked token inside this transaction
            // rather than comparing the two precision representations as concurrent edits.
            var token = record.UpdatedDate;
            if (feedback is not null)
            {
                OperationResult<Guid> result;
                if (feedback.Outcome == "Accepted")
                    result = await sender.Send(new ConfirmCustomerPriceCommand
                    {
                        SampleRequestId = record.SampleRequestId, ExpectedUpdatedDate = token,
                        CustomerAcceptedAt = feedback.OccurredAt, QuotationLineId = feedback.QuotationLineId,
                        Reference = feedback.Reference, Note = feedback.Note, CustomerFeedbackNote = feedback.CustomerFeedbackNote
                    }, ct);
                else
                    result = await sender.Send(new ApplySampleFlowActionCommand
                    {
                        SampleRequestId = record.SampleRequestId, ExpectedUpdatedDate = token,
                        Action = feedback.Outcome == "Rejected" ? "PriceRejected" : "PriceNoResponse",
                        OccurredAt = feedback.OccurredAt, PriceReference = feedback.Reference, QuotationLineId = feedback.QuotationLineId,
                        Note = feedback.Note, CustomerFeedbackNote = feedback.CustomerFeedbackNote,
                        UseExistingTransaction = true
                    }, ct);
                if (!result.Success) return result;
            }
            timings.Mark("feedback");
            if (interactionId.HasValue)
            {
                QuotationFeedbackInteraction.Add(crm, interactionId.Value, record, employee, feedback!, customer!, DateTime.Now);
                await crm.SaveChangesAsync(ct);
            }
            timings.Mark("crm");
            await transaction.CommitAsync(ct);
            timings.Mark("commit");
            return OperationResult<Guid>.Ok(record.SampleRequestId);
        }
        catch (DbUpdateException)
        {
            return Fail("Sample request changed. Reload before saving.");
        }
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
