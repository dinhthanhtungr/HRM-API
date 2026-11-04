using System.Linq.Expressions;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal static class SampleWorkDecisionQuery
{
    private sealed class QueryFacts
    {
        public SampleRequestSampleTrialReportRow Row { get; init; } = null!;
        public SampleWorkFacts Facts { get; init; } = null!;
    }

    internal static IQueryable<SampleRequestSampleTrialReportRow> Enrich(
        IQueryable<SampleRequestSampleTrialReportRow> rows, IPLMDbContext db, ICRMReadDbContext crm, string currency)
    {
        var bases = rows.Select(row => new
        {
            Row = row,
            FormulaId = (row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt ? row.SampleRequest.FormulaId :
                row.Trial != null ? row.Trial.FormulaId : null) ?? row.ConfirmedFormulaId ?? row.SampleRequest.FormulaId
        });
        var facts = from basis in bases
                    join formula in db.Formulas.Where(f => f.IsActive) on basis.FormulaId equals (Guid?)formula.FormulaId into formulas
                    from formula in formulas.DefaultIfEmpty()
                    select new QueryFacts
                    {
                        Row = basis.Row,
                        Facts = new SampleWorkFacts
                        {
                            Open = basis.Row.SampleRequest.Status != "Completed" && basis.Row.SampleRequest.Status != "Cancelled",
                            Legacy = basis.Row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt,
                            Sent = basis.Row.SampleRequest.Status == "SampleSent",
                            Quotation = (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" ||
                                (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá",
                            RequestedKg = basis.Row.SampleRequest.SampleQuantity > 0,
                            HasTrial = basis.Row.Trial != null,
                            HasSentKg = basis.FormulaId != null && db.SampleRequestSampleTrials.Any(t =>
                                t.IsActive && t.FormulaId == basis.FormulaId &&
                                t.SampleRequest.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                t.DeliveredSampleQuantityKg > 0 && t.SentDate != null),
                            PendingTrial = basis.Row.Trial != null && (basis.Row.Trial.Status == SampleTrialStatus.SampleSent ||
                                basis.Row.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback || basis.Row.Trial.Status == SampleTrialStatus.PriceQuote),
                            Chip = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg == 0,
                            Kg = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg > 0,
                            Failed = basis.Row.Trial != null && basis.Row.Trial.Status == SampleTrialStatus.Failed,
                            Approved = basis.Row.Trial != null && basis.Row.Trial.Status == SampleTrialStatus.Approved,
                            UnknownSent = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg == null && basis.Row.Trial.SentDate != null,
                            FormulaStatus = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId ? formula.Status : null,
                            FinalizedChip = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.Row.HasFinalizedChip,
                            AcceptedPrice = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.Row.HasAcceptedCustomerPrice,
                            ApprovedPrice = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.FormulaId != null && crm.ProductPricingVersions.Where(p => p.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                p.ProductId == basis.Row.SampleRequest.ProductId && p.IsActive && p.Status == ProductPricingStatus.Approved &&
                                p.StandardSellingPrice > 0 && p.Currency == currency).OrderByDescending(p => p.Version)
                                .ThenByDescending(p => p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate).Select(p => p.SourceFormulaId).FirstOrDefault() == basis.FormulaId,
                            LabConfirmed = basis.Row.LabFormulaConfirmedAt.HasValue,
                            Quoted = basis.Row.SampleRequest.RealPriceQuoteDate.HasValue
                        }
                    };
        return facts.Select(Projection());
    }

    private static Expression<Func<QueryFacts, SampleRequestSampleTrialReportRow>> Projection()
    {
        Expression<Func<QueryFacts, SampleRequestSampleTrialReportRow>> template = x => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = x.Row.SampleRequest, Trial = x.Row.Trial, TrialCount = x.Row.TrialCount,
            ConfirmedFormulaId = x.Row.ConfirmedFormulaId, LabFormulaConfirmedAt = x.Row.LabFormulaConfirmedAt,
            HasPriceFeedback = x.Row.HasPriceFeedback, HasAcceptedCustomerPrice = x.Row.HasAcceptedCustomerPrice, HasFinalizedChip = x.Row.HasFinalizedChip,
            NeedsSchedule = x.Facts.Open && (x.Facts.Quotation && !x.Facts.RequestedKg
                ? x.Row.SampleRequest.ExpectedPriceQuoteDate == null : x.Row.SampleRequest.ExpectedDeliveryDate == null)
        };
        var parameter = template.Parameters[0];
        var decision = SampleWorkDecisionRules.Replace(SampleWorkDecisionRules.Expression.Body,
            SampleWorkDecisionRules.Expression.Parameters[0], Expression.Property(parameter, nameof(QueryFacts.Facts)));
        var body = (MemberInitExpression)template.Body;
        return Expression.Lambda<Func<QueryFacts, SampleRequestSampleTrialReportRow>>(Expression.MemberInit(body.NewExpression,
            body.Bindings.Concat([Expression.Bind(typeof(SampleRequestSampleTrialReportRow).GetProperty(nameof(SampleRequestSampleTrialReportRow.WorkDecision))!, decision)])), parameter);
    }
}
