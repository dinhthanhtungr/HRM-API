using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.Orders;
using HRM.Application.Features.PLM.ComplaintReports.Queries;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetBySourceOrder;

internal sealed class GetComplaintReportsBySourceOrderQueryHandler
    : IRequestHandler<GetComplaintReportsBySourceOrderQuery, IReadOnlyList<ComplaintReportListItemDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetComplaintReportsBySourceOrderQueryHandler(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<IReadOnlyList<ComplaintReportListItemDto>> Handle(
        GetComplaintReportsBySourceOrderQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canAccess = await _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.MerchandiseOrderId == request.MerchandiseOrderId, cancellationToken);
        if (!canAccess)
        {
            return Array.Empty<ComplaintReportListItemDto>();
        }

        var rows = await _dbContext.ComplaintReports
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                x.ComplaintReportLines.Any(line =>
                    line.IsActive &&
                    line.SourceMerchandiseOrderDetail.MerchandiseOrderId == request.MerchandiseOrderId))
            .OrderByDescending(x => x.ReportedAt)
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
                    .Where(line => line.IsActive && line.SourceMerchandiseOrderDetail.MerchandiseOrderId == request.MerchandiseOrderId)
                    .Sum(line => line.ComplaintQuantity),
                LineCount = x.ComplaintReportLines.Count(line =>
                    line.IsActive &&
                    line.SourceMerchandiseOrderDetail.MerchandiseOrderId == request.MerchandiseOrderId),
                HasOpenActions = x.CapaActions.Any(action => action.IsActive && action.CompletedAt == null),
                HandlingOrder = x.ProcessingMerchandiseOrders
                    .Where(order => order.IsActive)
                    .Select(order => new { order.MerchandiseOrderId, order.ExternalId })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ComplaintReportListItemDto
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
    }
}
