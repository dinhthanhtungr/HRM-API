using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;

internal static class SampleRequestSummaryQueryRules
{
    public static async Task<IQueryable<SampleRequest>> BuildAsync(
        IPLMReadDbContext db, ICustomerVisibilityService visibility,
        SampleRequestSummaryFilters request, CancellationToken ct)
    {
        var scope = await visibility.BuildReadScopeAsync(ct);
        var visibilityScope = request.NormalizedKeyword is not null
            ? scope with { CanViewInternalCustomer = true }
            : scope;

        var sampleRequestQuery = visibility.ApplySampleRequestVisibility(
            db.SampleRequests
            .Where(x => x.IsActive && (x.RequestType == null || x.RequestType != HRM.Application.Features.PLM.Shared.Rules.SemiFinishedRequestRules.RequestType))
            .AsNoTracking()
            .AsQueryable(),
            db.Customers.AsNoTracking(),
            visibilityScope);

        if (request.CompanyId.HasValue)
        {
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CompanyId == request.CompanyId.Value);
        }

        if (request.ProductId.HasValue)
        {
            sampleRequestQuery = sampleRequestQuery.Where(x => x.ProductId == request.ProductId.Value);
        }

        if (request.FromDate.HasValue)
        {
            var fromDate = request.FromDate.Value.Date;
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CreatedDate >= fromDate);
        }

        if (request.ToDate.HasValue)
        {
            var toDateExclusive = request.ToDate.Value.Date.AddDays(1);
            sampleRequestQuery = sampleRequestQuery.Where(x => x.CreatedDate < toDateExclusive);
        }

        if (request.Status.HasValue)
        {
            var status = request.Status.Value.ToString();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
        {
            var color = request.Color.Trim();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Product.ColourName == color);
        }

        if (!string.IsNullOrWhiteSpace(request.AdditiveCode))
        {
            var additiveCode = request.AdditiveCode.Trim();
            sampleRequestQuery = sampleRequestQuery.Where(x => x.Product.Additive == additiveCode);
        }

        sampleRequestQuery = SampleRequestSummaryKeywordSearch.Apply(sampleRequestQuery, db, request.Keyword);
        return ApplySelections(sampleRequestQuery, db, request);
    }

    internal static IQueryable<SampleRequest> ApplySelections(
        IQueryable<SampleRequest> query, IPLMReadDbContext db, SampleRequestSummaryFilters request)
    {
        if (request.EmployeeIds.Length > 0)
            query = query.Where(x => request.EmployeeIds.Contains(x.ManagerBy) ||
                request.EmployeeIds.Contains(x.CreatedBy) ||
                (x.Product.CreatedBy.HasValue && request.EmployeeIds.Contains(x.Product.CreatedBy.Value)));
        if (request.GroupIds.Length > 0)
            query = query.Where(x => db.CustomerAssignments.Any(a => a.IsActive &&
                a.CompanyId == x.CompanyId && a.CustomerId == x.CustomerId &&
                a.Group.CompanyId == x.CompanyId && request.GroupIds.Contains(a.GroupId)));
        if (request.FormulaIds.Length > 0)
            query = query.Where(x => x.Product.Formulas.Any(f => f.IsActive &&
                f.CompanyId == x.CompanyId && request.FormulaIds.Contains(f.FormulaId)));
        if (request.ColourCodes.Length > 0)
            query = query.Where(x => x.Product.ColourCode != null && request.ColourCodes.Contains(x.Product.ColourCode));
        if (request.EndUsers.Length > 0)
            query = query.Where(x => x.Product.IsActive && x.Product.EndUser != null && request.EndUsers.Contains(x.Product.EndUser));
        if (request.CustomerIds.Length > 0)
            query = query.Where(x => request.CustomerIds.Contains(x.CustomerId));
        if (request.CustomerCodeIds.Length > 0)
            query = query.Where(x => request.CustomerCodeIds.Contains(x.CustomerId));
        if (request.CustomerTaxNumberIds.Length > 0)
            query = query.Where(x => x.Customer.CompanyId == x.CompanyId && request.CustomerTaxNumberIds.Contains(x.CustomerId));
        if (request.ProductIds.Length > 0)
            query = query.Where(x => request.ProductIds.Contains(x.ProductId));
        if (request.SampleRequestIds.Length > 0)
            query = query.Where(x => request.SampleRequestIds.Contains(x.SampleRequestId));
        return query;
    }
}
