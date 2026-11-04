using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestSaleWorkQuery
{
    public static IQueryable<SampleRequestSampleTrialReportRow> RequirePublishedQuotationPrice(
        IQueryable<SampleRequestSampleTrialReportRow> rows, ICRMReadDbContext db, string currency)
        => rows.Where(row =>
            row.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ||
            row.HasAcceptedCustomerPrice ||
            (row.Trial != null && (row.Trial.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.SampleSent ||
                row.Trial.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.WaitingCustomerFeedback ||
                row.Trial.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.PriceQuote)) ||
            !((row.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" ||
              (row.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá" ||
              row.SampleRequest.SampleQuantity > 0 ||
              row.SampleRequest.Status == "WaitingCustomerPriceConfirmation") ||
            (((row.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? row.SampleRequest.FormulaId : row.Trial != null ? row.Trial.FormulaId : null) ?? row.ConfirmedFormulaId ?? row.SampleRequest.FormulaId) != null &&
             db.ProductPricingVersions.Where(price => price.CompanyId == row.SampleRequest.CompanyId &&
                 price.ProductId == row.SampleRequest.ProductId && price.IsActive &&
                 price.Status == ProductPricingStatus.Approved && price.StandardSellingPrice > 0 && price.Currency == currency)
                 .OrderByDescending(price => price.Version)
                 .ThenByDescending(price => price.ApprovedAt ?? price.UpdatedDate ?? price.CreatedDate)
                 .Select(price => price.SourceFormulaId).FirstOrDefault() ==
                 ((row.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? row.SampleRequest.FormulaId : row.Trial != null ? row.Trial.FormulaId : null) ?? row.ConfirmedFormulaId ?? row.SampleRequest.FormulaId)));
}
