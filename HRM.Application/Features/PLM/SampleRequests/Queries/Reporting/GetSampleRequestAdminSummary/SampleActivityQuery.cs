using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class SampleActivityRow
{
    public Guid CompanyId { get; init; }
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public Guid BranchId { get; init; }
    public string? BranchName { get; init; }
    public Guid? EmployeeId { get; init; }
    public Guid? FormulaId { get; init; }
    public string Kind { get; init; } = "";
    public DateTime CompletedAt { get; init; }
    public string Outcome { get; init; } = "";
    public int Responses { get; init; }
    public DateTime? LatestResponseAt { get; init; }
    public int? TrialNo { get; init; }
}

/// <summary>One row per persisted delivery or approved pricing version, never one row per TP/creator.
/// Unlinked replies belong to a same-formula round's recorded-time window. Notes are not extra replies.</summary>
internal static class SampleActivityQuery
{
    internal static IQueryable<SampleActivityRow> Build(IQueryable<SampleRequest> visible, IPLMReadDbContext db,
        IQueryable<ProductPricingVersion> prices, DateTime start, DateTime end, DateTime now)
    {
        visible = SampleRequestWorkScope.Apply(visible, db.SampleRequestWorkflowEvents);
        var feedback = db.SampleRequestWorkflowEvents.Where(e => e.EventType == SampleRequestWorkflowEvents.FeedbackRecorded && e.RecordedAt <= now);
        var eligibleTrials = db.SampleRequestSampleTrials.Where(t => t.SentDate <= now);
        var optionalChips = ChipThenSampleRules.OptionalChips(eligibleTrials).Select(t => t.SampleRequestSampleTrialId);
        var plannedChips = ChipThenSampleRules.PlannedChips(eligibleTrials, db.SampleRequestWorkflowEvents.Where(e => e.RecordedAt <= now)).Select(t => t.SampleRequestSampleTrialId);
        var deliveries = from request in visible
            join trial in db.SampleRequestSampleTrials.Where(t => t.IsActive && t.SentDate != null &&
                t.SentDate >= start && t.SentDate < end && t.SentDate <= now)
                on request.SampleRequestId equals trial.SampleRequestId
            let responseCount = feedback.Count(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId &&
                e.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId)
            select new SampleActivityRow
            {
                CompanyId = request.CompanyId, Id = trial.SampleRequestSampleTrialId, RequestId = request.SampleRequestId,
                BranchId = request.BranchId, BranchName = db.Companies.Where(c => c.CompanyId == request.BranchId).Select(c => c.Name).FirstOrDefault(),
                EmployeeId = trial.SentByEmployeeId, FormulaId = trial.FormulaId,
                Kind = trial.DeliveredSampleQuantityKg > 0 ? "sample" : trial.DeliveredSampleQuantityKg == 0 ? "chip" : "other", CompletedAt = trial.SentDate!.Value,
                Outcome = trial.Status == SampleTrialStatus.Approved ? "accepted" :
                    trial.Status == SampleTrialStatus.Failed || trial.Status == SampleTrialStatus.ReworkRequested ? "rejected" :
                    trial.Status == SampleTrialStatus.Cancelled || request.Status == "Cancelled" ? "cancelled" :
                    request.Status == "LabDeclined" ? "labDeclined" :
                    trial.Status == SampleTrialStatus.SampleSent || trial.Status == SampleTrialStatus.WaitingCustomerFeedback || trial.Status == SampleTrialStatus.PriceQuote
                        ? optionalChips.Contains(trial.SampleRequestSampleTrialId) || plannedChips.Contains(trial.SampleRequestSampleTrialId) ? "optional" : "waiting" : "unknown",
                Responses = responseCount == 0 && trial.CustomerReplyDate != null && trial.CustomerReplyDate <= now ? 1 : responseCount,
                LatestResponseAt = feedback.Where(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId &&
                    e.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId).Select(e => (DateTime?)e.RecordedAt).Max()
                    ?? (trial.CustomerReplyDate <= now ? trial.CustomerReplyDate : null), TrialNo = trial.TrialNo
            };
        var events = db.SampleRequestWorkflowEvents;
        var rounds = from request in visible
            join price in prices.Where(p => p.ApprovedAt != null && p.ApprovedAt >= start && p.ApprovedAt < end &&
                p.ApprovedAt <= now && p.Status == ProductPricingStatus.Approved && p.Currency == "VND" && p.StandardSellingPrice > 0 && p.SourceFormulaId != null)
                on new { request.ProductId, request.CompanyId } equals new { price.ProductId, price.CompanyId }
            where price.ApprovedAt >= request.CreatedDate && db.Formulas.Any(f => f.FormulaId == price.SourceFormulaId &&
                f.CompanyId == request.CompanyId && f.ProductId == request.ProductId) &&
                (request.FormulaId == price.SourceFormulaId || events.Any(e => e.CompanyId == request.CompanyId &&
                    e.SampleRequestId == request.SampleRequestId && e.FormulaId == price.SourceFormulaId &&
                    e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.RecordedAt <= price.ApprovedAt))
            select new
            {
                Request = request, Price = price,
                Next = prices.Where(p => p.CompanyId == price.CompanyId && p.ProductId == price.ProductId &&
                    p.SourceFormulaId == price.SourceFormulaId && p.Currency == "VND" && p.Status == ProductPricingStatus.Approved && p.StandardSellingPrice > 0 &&
                    (p.ApprovedAt > price.ApprovedAt || p.ApprovedAt == price.ApprovedAt && p.Version > price.Version)).Select(p => p.ApprovedAt).Min(),
                Employee = events.Where(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId &&
                    e.FormulaId == price.SourceFormulaId && e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.RecordedAt <= price.ApprovedAt)
                    .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.RecordedByEmployeeId).FirstOrDefault()
            };
        var priceReplies = from round in rounds
            from e in events
            where e.CompanyId == round.Request.CompanyId && e.SampleRequestId == round.Request.SampleRequestId && e.RecordedAt <= now &&
                (e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == SampleRequestWorkflowEvents.PriceRevoked ||
                 e.EventType == SampleFlowRules.PriceRejected || e.EventType == SampleFlowRules.PriceNoResponse ||
                 e.EventType == "CustomerPriceConfirmed" || e.EventType == "CustomerPriceConfirmationRevoked") &&
                e.FormulaId == round.Price.SourceFormulaId &&
                (e.ProductPricingVersionId == round.Price.ProductPricingVersionId ||
                 e.ProductPricingVersionId == null && e.RelatedEventId != null && events.Any(parent => parent.Id == e.RelatedEventId &&
                    parent.CompanyId == e.CompanyId && parent.SampleRequestId == e.SampleRequestId && parent.ProductPricingVersionId == round.Price.ProductPricingVersionId) ||
                 e.ProductPricingVersionId == null && e.RelatedEventId == null && e.RecordedAt >= round.Price.ApprovedAt &&
                    (round.Next == null || e.RecordedAt < round.Next))
            select new { RequestId = round.Request.SampleRequestId, PriceId = round.Price.ProductPricingVersionId, e.Id, e.EventType, e.RecordedAt };
        var quotations = from round in rounds
            let latest = priceReplies.Where(e => e.RequestId == round.Request.SampleRequestId && e.PriceId == round.Price.ProductPricingVersionId)
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.EventType).FirstOrDefault()
            select new SampleActivityRow
            {
                CompanyId = round.Request.CompanyId, Id = round.Price.ProductPricingVersionId, RequestId = round.Request.SampleRequestId,
                BranchId = round.Request.BranchId, BranchName = db.Companies.Where(c => c.CompanyId == round.Request.BranchId).Select(c => c.Name).FirstOrDefault(),
                EmployeeId = round.Employee, FormulaId = round.Price.SourceFormulaId,
                Kind = "quotation", CompletedAt = round.Price.ApprovedAt!.Value,
                Outcome = latest == SampleRequestWorkflowEvents.PriceAccepted || latest == "CustomerPriceConfirmed" ? "accepted" :
                    latest == SampleFlowRules.PriceRejected ? "rejected" : round.Request.Status == "Cancelled" ? "cancelled" : round.Request.Status == "LabDeclined" ? "labDeclined" : "waiting",
                Responses = priceReplies.Count(e => e.RequestId == round.Request.SampleRequestId && e.PriceId == round.Price.ProductPricingVersionId),
                LatestResponseAt = priceReplies.Where(e => e.RequestId == round.Request.SampleRequestId && e.PriceId == round.Price.ProductPricingVersionId)
                    .Select(e => (DateTime?)e.RecordedAt).Max(), TrialNo = null
            };
        return deliveries.Concat(quotations).Select(row => new SampleActivityRow
        {
            CompanyId = row.CompanyId, Id = row.Id, RequestId = row.RequestId, BranchId = row.BranchId, BranchName = row.BranchName,
            EmployeeId = db.Employees.Where(e => e.CompanyId == row.CompanyId && e.EmployeeId == row.EmployeeId)
                .Select(e => (Guid?)e.EmployeeId).FirstOrDefault(),
            FormulaId = row.FormulaId, Kind = row.Kind, CompletedAt = row.CompletedAt, Outcome = row.Outcome,
            Responses = row.Responses, LatestResponseAt = row.LatestResponseAt, TrialNo = row.TrialNo
        });
    }
}
