using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Requests;

internal static class SampleRequestTrackingReader
{
    internal static Guid? ResolveCompany(ICurrentUser user, ICurrentUserPermissionService permissions) =>
        permissions.HasPermission(ApplicationPermissions.PLM.ViewSampleRequestTracking) &&
        user.EmployeeId is { } employeeId && employeeId != Guid.Empty &&
        user.CompanyId is { } companyId && companyId != Guid.Empty
            ? companyId
            : null;

    internal static IQueryable<SampleRequest> VisibleRequests(IPLMReadDbContext db, Guid companyId) =>
        db.SampleRequests.AsNoTracking().Where(sample =>
            sample.CompanyId == companyId && sample.IsActive &&
            sample.Product.CompanyId == companyId && sample.Product.IsActive &&
            sample.Customer.CompanyId == companyId && sample.Customer.IsActive == true &&
            sample.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            sample.Customer.ExternalId != null &&
            sample.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId &&
            (sample.RequestType == null || sample.RequestType.Trim() == "" ||
             sample.RequestType.Trim().ToLower() == "public"));

    internal static IQueryable<SampleRequestTrackingMilestoneDto> ProjectMilestones(
        IPLMReadDbContext db, IQueryable<SampleRequest> visibleRequests, Guid companyId) =>
        db.SampleRequestWorkflowEvents.AsNoTracking()
            .Where(row => row.CompanyId == companyId &&
                visibleRequests.Any(request => request.SampleRequestId == row.SampleRequestId) &&
                (row.EventType == SampleRequestWorkflowEvents.Created ||
                 row.EventType == SampleRequestWorkflowEvents.RequestCancelled ||
                 row.EventType == SampleRequestWorkflowEvents.LabDeclined ||
                 row.EventType == SampleDeliveryRules.BatchSent ||
                 (row.EventType == SampleRequestWorkflowEvents.SampleSent && row.RelatedEventId == null)))
            .GroupBy(row => row.EventType == SampleRequestWorkflowEvents.Created ? nameof(SampleRequestStatus.New)
                : row.EventType == SampleRequestWorkflowEvents.RequestCancelled ? nameof(SampleRequestStatus.Cancelled)
                : row.EventType == SampleRequestWorkflowEvents.LabDeclined ? nameof(SampleRequestStatus.LabDeclined)
                : nameof(SampleRequestStatus.SampleSent))
            .Select(group => new { Status = group.Key, At = group.Max(row => row.OccurredAt ?? row.RecordedAt) })
            .OrderByDescending(row => row.At).ThenBy(row => row.Status)
            .Select(row => new SampleRequestTrackingMilestoneDto(row.Status, row.At));

    internal static IQueryable<SampleRequestTrackingDto> Project(IQueryable<SampleRequest> query) =>
        query.Select(sample => new SampleRequestTrackingDto(
            sample.SampleRequestId,
            sample.ExternalId,
            sample.Product.Code,
            sample.Product.Name,
            sample.Status == nameof(SampleRequestStatus.New) ||
            sample.Status == nameof(SampleRequestStatus.Pending) ||
            sample.Status == nameof(SampleRequestStatus.SampleSent) ||
            sample.Status == nameof(SampleRequestStatus.Completed) ||
            sample.Status == nameof(SampleRequestStatus.Cancelled) ||
            sample.Status == nameof(SampleRequestStatus.LabDeclined)
                ? sample.Status
                : nameof(SampleRequestStatus.InProgress),
            sample.ExpectedDeliveryDate));
}
