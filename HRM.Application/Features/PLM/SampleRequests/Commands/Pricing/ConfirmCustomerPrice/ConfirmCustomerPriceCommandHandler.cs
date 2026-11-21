using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;

internal sealed class ConfirmCustomerPriceCommandHandler(
    IPLMWriteDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    ICustomerVisibilityService visibility, ISender sender, ICRMReadDbContext crm,
    ILogger<ConfirmCustomerPriceCommandHandler>? logger = null)
    : IRequestHandler<ConfirmCustomerPriceCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(ConfirmCustomerPriceCommand request, CancellationToken ct)
    {
        using var timings = new SampleWorkQueryTimings(
            logger ?? NullLogger<ConfirmCustomerPriceCommandHandler>.Instance, true, "price");
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice) ||
            user.CompanyId is not { } company || user.EmployeeId is not { } employee)
            return OperationResult<Guid>.Fail("You cannot confirm customer prices.");
        var scope = await visibility.BuildScopeAsync(ct);
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive, ct))
            return OperationResult<Guid>.Fail("Current employee is not active in this company.");
        var record = await visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers, scope)
            .SingleOrDefaultAsync(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        if (record is null || !SampleSaleWorkflowAccess.CanActFor(user, permissions, record.ManagerBy, scope)) return OperationResult<Guid>.Fail("Sample request is outside your scope.");
        if (await Services.Workflow.SampleRequestLabPauseRules.IsPausedAsync(db, company, record.SampleRequestId, ct))
            return OperationResult<Guid>.Fail(Services.Workflow.SampleRequestLabPauseRules.PausedMessage);
        var latestAudit = await SampleRequestCustomerPriceAudit.LatestAsync(db, company, record.SampleRequestId, ct);
        var confirmation = latestAudit is null ? null : SampleRequestCustomerPriceAudit.Read(latestAudit);
        if (!(SampleRequestCustomerPriceRules.IsColourSet(record.InfoType) || SampleRequestCustomerPriceRules.IsQuotation(record.InfoType) || record.SampleQuantity is > 0) || record.Status is "Completed" or "Cancelled" or "LabDeclined")
            return OperationResult<Guid>.Fail("Price confirmation does not apply to this request.");
        if (record.UpdatedDate != request.ExpectedUpdatedDate)
            return OperationResult<Guid>.Fail("Sample request changed. Reload before saving.");
        if (request.CustomerFeedbackNote?.Length > 2000 || request.Note?.Length > 2000 || request.Reference?.Length > 500)
            return OperationResult<Guid>.Fail("Price reference or note is too long.");
        if (request.Revoke && string.IsNullOrWhiteSpace(request.Note))
            return OperationResult<Guid>.Fail("A reason is required to revoke confirmation.");
        if (request.Revoke && confirmation?.IsActive != true)
            return OperationResult<Guid>.Fail("There is no active price acceptance to revoke.");
        if (!request.Revoke && confirmation?.IsActive == true)
            return OperationResult<Guid>.Ok(record.SampleRequestId, "Customer price is already confirmed.");
        Guid? formulaId = request.Revoke ? latestAudit?.FormulaId : null;
        Guid? pricingVersionId = request.Revoke ? latestAudit?.ProductPricingVersionId : null;
        timings.Mark("validation");
        if (!request.Revoke)
        {
            var readiness = await sender.Send(new HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork.GetSampleRequestDailyWorkQuery
            {
                SampleRequestId = record.SampleRequestId,
                View = SampleRequestDailyView.All,
                PageSize = 1,
                // Same scoped readiness/price capabilities as the dialog, without list ranking
                // or aggregating trials belonging to unrelated requests inside this save.
                WorkflowDetail = true
            }, ct);
            timings.Mark("readiness");
            var readyItem = readiness.Items.SingleOrDefault();
            if (readyItem is null) return OperationResult<Guid>.Fail("PriceFeedback.RequestUnavailable");
            if (!readyItem.LabFormulaConfirmedAt.HasValue) return OperationResult<Guid>.Fail("PriceFeedback.LabConfirmationRequired");
            if (!readyItem.RealPriceQuoteDate.HasValue) return OperationResult<Guid>.Fail("PriceFeedback.QuotationDateRequired");
            if (!readyItem.HasApprovedStandardPrice) return OperationResult<Guid>.Fail("PriceFeedback.StandardPriceRequired");
            if (!readyItem.CanConfirmCustomerPrice) return OperationResult<Guid>.Fail("PriceFeedback.NotAllowed");
            // Preserve the actual price/formula basis even when the customer reference is external.
            var approvedPrice = await crm.ProductPricingVersions.AsNoTracking()
                .Where(x => x.CompanyId == company && x.ProductId == record.ProductId && x.IsActive &&
                    x.Currency == "VND" && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new { x.ProductPricingVersionId, x.SourceFormulaId })
                .FirstOrDefaultAsync(ct);
            if (approvedPrice?.SourceFormulaId is null || !await db.Formulas.AsNoTracking().AnyAsync(f =>
                    f.FormulaId == approvedPrice.SourceFormulaId && f.CompanyId == company &&
                    f.ProductId == record.ProductId && f.IsActive, ct))
                return OperationResult<Guid>.Fail("PriceFeedback.StandardPriceRequired");
            formulaId = approvedPrice.SourceFormulaId;
            pricingVersionId = approvedPrice.ProductPricingVersionId;
        }
        timings.Mark("approved-price");

        var reference = request.Revoke ? confirmation?.PriceReference : request.Reference?.Trim();
        if (!request.Revoke && request.QuotationLineId.HasValue)
        {
            var line = await db.QuotationLines.AsNoTracking().Where(x => x.QuotationLineId == request.QuotationLineId &&
                    x.IsActive && x.SampleRequestId == record.SampleRequestId && x.ProductId == record.ProductId &&
                    x.Quotation.IsActive && x.Quotation.CompanyId == company && x.Quotation.CustomerId == record.CustomerId &&
                    (x.Quotation.Status == QuotationStatus.Sent || x.Quotation.Status == QuotationStatus.Negotiating ||
                     x.Quotation.Status == QuotationStatus.Accepted))
                .Select(x => new { x.Quotation.ExternalId, x.Quotation.Version, x.SortOrder, x.ProductPricingVersionId,
                    SourceFormulaId = x.ProductPricingVersion != null && x.ProductPricingVersion.CompanyId == company &&
                        x.ProductPricingVersion.ProductId == record.ProductId ? x.ProductPricingVersion.SourceFormulaId : null })
                .SingleOrDefaultAsync(ct);
            if (line is null) return OperationResult<Guid>.Fail("Select a sent quotation line of this request/customer/product.");
            reference = $"{line.ExternalId} · v{line.Version} · line {line.SortOrder}";
            pricingVersionId = line.ProductPricingVersionId;
            // A sent quotation is a historical price basis; do not silently replace it with today's price.
            // Legacy/manual quotations may have no recorded source formula. Keep that unknown
            // rather than attributing their historical price to today's confirmed formula.
            formulaId = line.SourceFormulaId;
        }
        var now = DateTime.Now;
        var acceptedAt = request.CustomerAcceptedAt ?? now;
        if (acceptedAt.Kind == DateTimeKind.Utc) acceptedAt = acceptedAt.ToLocalTime();
        acceptedAt = DateTime.SpecifyKind(acceptedAt, DateTimeKind.Unspecified);
        if (!request.Revoke && (acceptedAt == DateTime.MinValue || acceptedAt > now.AddMinutes(5)))
            return OperationResult<Guid>.Fail("Customer acceptance date is invalid or in the future.");
        var oldStatus = record.Status;
        // Thông tin giá độc lập với Trial đã gửi; không đưa SampleSent về InProgress khi thu hồi.
        record.Status = SampleRequestCustomerPriceRules.ResolveStatusAfterPriceEvent(record.Status, request.Revoke);
        record.UpdatedBy = employee;
        record.UpdatedDate = now;
        var workflowEvent = SampleRequestCustomerPriceAudit.CreateEvent(record.SampleRequestId, company, employee, now,
            request.Revoke, oldStatus, record.Status, request.Revoke ? confirmation?.CustomerAcceptedAt : acceptedAt,
            request.Revoke ? confirmation?.QuotationLineId : request.QuotationLineId, reference, request.Note?.Trim());
        workflowEvent.RelatedEventId = request.Revoke ? latestAudit?.Id : null;
        workflowEvent.FormulaId = formulaId;
        workflowEvent.ProductPricingVersionId = pricingVersionId;
        db.SampleRequestWorkflowEvents.Add(workflowEvent);
        SampleRequestWorkflowNotificationQueue.Queue(db, workflowEvent);
        if (SampleRequestWorkflowEvents.CreateSharedPriceFeedback(workflowEvent, request.CustomerFeedbackNote) is { } shared)
        {
            db.SampleRequestWorkflowEvents.Add(shared);
            SampleRequestWorkflowNotificationQueue.Queue(db, shared);
        }
        await SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(db.AuditLogs,
            record, oldStatus, employee, now, "CustomerPriceStatusChanged", ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<Guid>.Fail("Price confirmation changed concurrently. Reload before saving.");
        }
        timings.Mark("save");
        return OperationResult<Guid>.Ok(record.SampleRequestId, "Price confirmation saved; notification queued.");
    }
}
