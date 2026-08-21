using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Queries;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReports;

internal sealed class GetComplaintReportsQueryHandler
    : IRequestHandler<GetComplaintReportsQuery, PagedResult<ComplaintReportListItemDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetComplaintReportsQueryHandler(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<ComplaintReportListItemDto>> Handle(
        GetComplaintReportsQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleCustomers = _visibilityService.ApplyCustomerVisibility(
            _dbContext.Customers.AsNoTracking(),
            scope);
        var query = _dbContext.ComplaintReports
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                visibleCustomers.Any(customer => customer.CustomerId == x.CustomerId));

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (request.ResolutionType.HasValue)
        {
            query = query.Where(x =>
                x.ResolutionType == request.ResolutionType.Value ||
                (!x.ResolutionType.HasValue && x.RequestedResolutionType == request.ResolutionType.Value));
        }

        if (request.ReportedFrom.HasValue)
        {
            query = query.Where(x => x.ReportedAt >= request.ReportedFrom.Value.Date);
        }

        if (request.ReportedTo.HasValue)
        {
            var toExclusive = request.ReportedTo.Value.Date.AddDays(1);
            query = query.Where(x => x.ReportedAt < toExclusive);
        }

        if (request.SourceOrderId is { } sourceOrderId && sourceOrderId != Guid.Empty)
        {
            query = query.Where(x => x.ComplaintReportLines.Any(line =>
                line.IsActive &&
                line.SourceMerchandiseOrderDetail.MerchandiseOrderId == sourceOrderId));
        }

        if (request.AssignedToMe)
        {
            query = query.Where(x =>
                x.CreatedBy == scope.EmployeeId ||
                x.EffectivenessPersonInChargeId == scope.EmployeeId ||
                x.CapaActions.Any(action =>
                    action.IsActive &&
                    action.PersonInChargeId == scope.EmployeeId &&
                    action.CompletedAt == null) ||
                _dbContext.CustomerAssignments.Any(assignment =>
                    assignment.CompanyId == scope.CompanyId &&
                    assignment.CustomerId == x.CustomerId &&
                    assignment.EmployeeId == scope.EmployeeId &&
                    assignment.IsActive));
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.Customer.ExternalId.Contains(keyword) ||
                x.Customer.CustomerName.Contains(keyword) ||
                (x.Summary != null && x.Summary.Contains(keyword)) ||
                x.ComplaintReportLines.Any(line =>
                    line.IsActive &&
                    (line.ProductExternalIdSnapshot.Contains(keyword) ||
                     line.ProductNameSnapshot.Contains(keyword) ||
                     EF.Functions.ILike(line.FormulaExternalIdSnapshot, $"%{keyword}%"))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.ReportedAt)
            .ThenByDescending(x => x.ComplaintReportId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new
            {
                x.ComplaintReportId,
                x.ExternalId,
                x.Status,
                x.RequestedResolutionType,
                x.ResolutionType,
                x.CustomerId,
                CustomerExternalId = x.CustomerExternalIdSnapshot != string.Empty
                    ? x.CustomerExternalIdSnapshot
                    : x.Customer.ExternalId,
                CustomerName = x.CustomerNameSnapshot != string.Empty
                    ? x.CustomerNameSnapshot
                    : x.Customer.CustomerName,
                x.Summary,
                x.ReportedAt,
                ComplaintQuantity = x.ComplaintReportLines
                    .Where(line => line.IsActive)
                    .Sum(line => (decimal?)line.ComplaintQuantity) ?? 0,
                LineCount = x.ComplaintReportLines.Count(line => line.IsActive),
                HasOpenActions = x.CapaActions.Any(action => action.IsActive && action.CompletedAt == null),
                HandlingOrder = x.ProcessingMerchandiseOrders
                    .Where(order => order.IsActive)
                    .Select(order => new { order.MerchandiseOrderId, order.ExternalId })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new ComplaintReportListItemDto
        {
            ComplaintReportId = x.ComplaintReportId,
            ExternalId = x.ExternalId,
            Status = x.Status.ToString(),
            RequestedResolutionType = x.RequestedResolutionType?.ToString(),
            ResolutionType = x.ResolutionType?.ToString(),
            CustomerId = x.CustomerId,
            CustomerExternalId = x.CustomerExternalId,
            CustomerName = x.CustomerName,
            Summary = x.Summary,
            ReportedAt = x.ReportedAt,
            ComplaintQuantity = x.ComplaintQuantity,
            LineCount = x.LineCount,
            HasHandlingOrder = x.HandlingOrder is not null,
            HasOpenActions = x.HasOpenActions,
            TimelineBadge = ComplaintReportReadRules.TimelineBadge(x.Status),
            HandlingMerchandiseOrderId = x.HandlingOrder?.MerchandiseOrderId,
            HandlingMerchandiseOrderExternalId = x.HandlingOrder?.ExternalId
        }).ToList();

        return new PagedResult<ComplaintReportListItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

}
