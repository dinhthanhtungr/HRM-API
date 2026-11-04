using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Lab.Samples;

internal sealed class LabSampleReportReader(IPLMReadDbContext db, ICustomerVisibilityService visibility)
{
    public async Task<IQueryable<LabSampleRowDto>> QueryAsync(LabSampleReportFilter filter, CancellationToken ct, DateOnly? cohortFrom = null)
    {
        var scope = await visibility.BuildReadScopeAsync(ct);
        var from = LabSampleReportPeriods.LocalMidnight(cohortFrom ?? filter.From);
        var until = LabSampleReportPeriods.LocalMidnight(filter.To.AddDays(1));
        var samples = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.CompanyId == scope.CompanyId && x.IsActive && x.Status != "Cancelled"
                && x.Customer.IsActive == true && x.Customer.CompanyId == scope.CompanyId
                && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId
                && x.CreatedDate >= from && x.CreatedDate < until);
        if (filter.CustomerId.HasValue) samples = samples.Where(x => x.CustomerId == filter.CustomerId);
        if (filter.SaleEmployeeId.HasValue) samples = samples.Where(x => x.ManagerBy == filter.SaleEmployeeId);
        if (filter.LabReceivedByEmployeeId.HasValue) samples = samples.Where(x => x.LabReceivedByEmployeeId == filter.LabReceivedByEmployeeId);
        if (!string.IsNullOrWhiteSpace(filter.RequestType))
        {
            var type = filter.RequestType.Trim();
            samples = samples.Where(x => x.RequestType == type);
        }
        // JSON paths được Npgsql dịch thành SQL; không tải payload audit hoặc Formula price/material.
        var colourEvents = db.AuditLogs.AsNoTracking().Where(a => a.SchemaName == "SampleRequests"
            && a.TableName == "Products" && a.CompanyId == scope.CompanyId && a.ChangedAt < until
            && a.NewValues != null && a.NewValues.RootElement.GetProperty("ColourCode").GetString() != null
            && a.NewValues.RootElement.GetProperty("ColourCode").GetString()!.Trim() != ""
            && (a.OldValues == null || a.OldValues.RootElement.GetProperty("ColourCode").GetString() == null
                || a.OldValues.RootElement.GetProperty("ColourCode").GetString()!.Trim() == ""));
        var completedEvents = db.AuditLogs.AsNoTracking().Where(a => a.SchemaName == "SampleRequests"
            && a.TableName == "SampleRequests" && a.CompanyId == scope.CompanyId
            && a.NewValues != null && a.NewValues.RootElement.GetProperty("Status").GetString() == "Completed"
            && a.OldValues != null && a.OldValues.RootElement.GetProperty("Status").GetString() != null
            && a.OldValues.RootElement.GetProperty("Status").GetString() != "Completed");
        return samples.Select(x => new LabSampleRowDto
        {
            SampleRequestId = x.SampleRequestId, ExternalId = x.ExternalId,
            CustomerName = x.Customer.CompanyId == scope.CompanyId ? x.Customer.CustomerName : "",
            ProductName = x.Product.CompanyId == scope.CompanyId ? x.Product.Name ?? "" : "",
            ColourCode = x.Product.CompanyId == scope.CompanyId ? x.Product.ColourCode : null,
            SaleEmployeeId = x.ManagerBy, SaleEmployeeName = x.ManagerByNavigation.FullName,
            LabReceivedByEmployeeId = x.LabReceivedByEmployeeId,
            LabReceivedByEmployeeName = x.LabReceivedByNavigation == null ? null : x.LabReceivedByNavigation.FullName,
            Status = x.Status, CreatedAt = x.CreatedDate, LabReceivedAt = x.LabReceivedDate,
            ColourAssignedAt = colourEvents.Where(a => a.RecordId == x.ProductId).Select(a => (DateTime?)a.ChangedAt).Min(),
            LabApprovedAt = db.FormulaVersions.Where(v => v.Formula.CompanyId == scope.CompanyId
                && v.Status == "Approved" && v.ChangeReason == "Status changed to Approved" && v.CreatedAt < until
                && (x.FormulaId == v.FormulaId || x.SampleRequestSampleTrials.Any(t => t.IsActive && t.FormulaId == v.FormulaId)))
                .Select(v => (DateTime?)v.CreatedAt).Min(),
            CustomerApprovedAt = completedEvents.Where(a => a.RecordId == x.SampleRequestId && a.ChangedAt >= x.CreatedDate && a.ChangedAt < until)
                .Select(a => (DateTime?)a.ChangedAt).Min(),
            ConversionUnknown = x.Status == "Completed" && !completedEvents.Any(a => a.RecordId == x.SampleRequestId && a.ChangedAt >= x.CreatedDate),
            TrialCount = x.SampleRequestSampleTrials.Count(t => t.IsActive),
            WaitingFeedback = x.SampleRequestSampleTrials.Where(t => t.IsActive).OrderByDescending(t => t.TrialNo)
                .Select(t => t.Status == SampleTrialStatus.WaitingCustomerFeedback || t.Status == SampleTrialStatus.PriceQuote)
                .FirstOrDefault(),
            Accepted = x.SampleRequestSampleTrials.Any(t => t.IsActive && t.Status == SampleTrialStatus.Approved && t.CustomerReplyDate < until),
            Rejected = x.SampleRequestSampleTrials.Any(t => t.IsActive && t.Status == SampleTrialStatus.Failed && t.CustomerReplyDate < until)
        });
    }
}
