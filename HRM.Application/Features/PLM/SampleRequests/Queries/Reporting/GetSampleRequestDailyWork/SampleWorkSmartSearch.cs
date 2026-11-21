using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleWorkSmartSearch
{
    internal static bool CanReadMetadataOnly(GetSampleRequestSampleTrialsQuery request)
        => request.SuggestionKeyword is not null && request.WorkAudience.HasValue &&
           request.WorkBucket == SampleRequestWorkBucket.All && request.DailyWorkView == SampleRequestDailyView.All &&
           !request.IncludeTrialHistory;

    internal static SmartSearchSchema<SampleRequestSampleTrialReportRow> Build(
        IQueryable<SampleRequestSampleTrialReportRow> query, IPLMReadDbContext db) =>
        SmartSearch.For(query, fields => [
            fields.Code("sampleRequestCode", x => (x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId, "TP Code", "Mã TP"),
            fields.Code("customerCode", x => x.SampleRequest.Customer.ExternalId, "Mã KH", "Customer code"),
            fields.Name("customerName", x => (x.Trial != null ? x.Trial.CustomerNameSnapshot : null) ?? x.SampleRequest.Customer.CustomerName, "Tên KH", "Customer name"),
            fields.CustomerTaxNumber(x => x.SampleRequest.Customer.CompanyId == x.SampleRequest.CompanyId ? x.SampleRequest.Customer.TaxNumber : null),
            fields.Name("productName", x => (x.Trial != null ? x.Trial.ProductNameSnapshot : null) ?? x.SampleRequest.Product.Name, "Tên sản phẩm", "Product name"),
            fields.Name("endUser", x => x.SampleRequest.Product.IsActive && x.SampleRequest.Product.CompanyId == x.SampleRequest.CompanyId
                ? x.SampleRequest.Product.EndUser : "", "End user", "EndUser", "Người dùng cuối"),
            fields.Code("colourCode", x => (x.Trial != null ? x.Trial.ColourCodeSnapshot : null) ?? x.SampleRequest.Product.ColourCode, "ColorCode", "Mã màu"),
            fields.Name("employeeName", x => x.SampleRequest.ManagerByNavigation.FullName, "Nhân viên", "Employee"),
            fields.Code("formulaCode", x => db.Formulas.Where(f => f.CompanyId == x.SampleRequest.CompanyId &&
                f.ProductId == x.SampleRequest.ProductId && f.IsActive &&
                f.FormulaId == (((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null)
                    ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId)).Select(f => f.ExternalId).FirstOrDefault(), "Công thức", "Formula", "VU")
        ]);
}
