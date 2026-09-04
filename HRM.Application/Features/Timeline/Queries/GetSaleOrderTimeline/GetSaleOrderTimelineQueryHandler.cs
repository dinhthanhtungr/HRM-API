using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimeline;

/// <summary>
/// Áp dụng customer visibility, bộ lọc timeline và trạng thái pause hiệu lực; sau đó gắn các EventLog
/// phù hợp vào từng SaleOrder card trong trang kết quả.
/// </summary>
internal sealed class GetSaleOrderTimelineQueryHandler
    : IRequestHandler<GetSaleOrderTimelineQuery, PagedResult<SaleOrderTimelineCardDto>>
{
    private readonly ITimelineDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetSaleOrderTimelineQueryHandler(
        ITimelineDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PagedResult<SaleOrderTimelineCardDto>> Handle(
        GetSaleOrderTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var query = _visibilityService.ApplyMerchandiseOrderVisibility(
            _dbContext.MerchandiseOrders.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        if (request.Id is { } id && id != Guid.Empty)
        {
            query = query.Where(x => x.MerchandiseOrderId == id);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.CustomerNameSnapshot.Contains(keyword) ||
                x.CustomerExternalIdSnapshot.Contains(keyword) ||
                x.CreatedByNavigation!.FullName.Contains(keyword) ||
                x.MerchandiseOrderDetails.Any(detail => detail.ProductExternalIdSnapshot.Contains(keyword)));
        }

        var now = _dateTimeProvider.Now;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (string.Equals(status, MerchadiseStatus.Paused.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.Status != MerchadiseStatus.Cancelled.ToString() &&
                    x.IsDeliveryPaused &&
                    (!x.DeliveryPausedFrom.HasValue || x.DeliveryPausedFrom.Value.Date <= now.Date) &&
                    (!x.DeliveryPausedTo.HasValue || x.DeliveryPausedTo.Value.Date >= now.Date));
            }
            else
            {
                query = query.Where(x => x.Status == status);
            }
        }

        var from = request.FromCreated?.Date;
        var toExclusive = request.ToCreated?.Date.AddDays(1);
        query = ApplyCreatedScopeFilter(query, request.CreatedScope, from, toExclusive);

        if (request.CreatedBy.HasValue || request.CompanyId.HasValue || request.EventType.HasValue)
        {
            query = query.Where(order => _dbContext.EventLogs.Any(log =>
                log.IsActive &&
                log.SourceId == order.MerchandiseOrderId &&
                (!request.CreatedBy.HasValue || log.EmployeeID == request.CreatedBy.Value) &&
                (!request.CompanyId.HasValue || log.CompanyId == request.CompanyId.Value) &&
                (!request.EventType.HasValue || log.EventType == request.EventType.Value)));
        }

        if (request.HasComplaint.HasValue)
        {
            query = request.HasComplaint.Value
                ? query.Where(order => _dbContext.ComplaintReportLines.Any(line =>
                    line.IsActive &&
                    line.ComplaintReport.IsActive &&
                    line.SourceMerchandiseOrderDetail.MerchandiseOrderId == order.MerchandiseOrderId))
                : query.Where(order => !_dbContext.ComplaintReportLines.Any(line =>
                    line.IsActive &&
                    line.ComplaintReport.IsActive &&
                    line.SourceMerchandiseOrderDetail.MerchandiseOrderId == order.MerchandiseOrderId));
        }

        if (request.ComplaintStatus.HasValue)
        {
            query = query.Where(order => _dbContext.ComplaintReportLines.Any(line =>
                line.IsActive &&
                line.ComplaintReport.IsActive &&
                line.ComplaintReport.Status == request.ComplaintStatus.Value &&
                line.SourceMerchandiseOrderDetail.MerchandiseOrderId == order.MerchandiseOrderId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.MerchandiseOrderId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new SaleOrderTimelineCardDto
            {
                MerchandiseOrderId = x.MerchandiseOrderId,
                ExternalId = x.ExternalId,
                PONo = x.PONo,
                CreatedName = x.CreatedByNavigation!.FullName,
                Status = x.Status,
                CreatedDate = x.CreateDate,
                CustomerName = x.CustomerNameSnapshot,
                CustomerExternalId = x.CustomerExternalIdSnapshot,
                TotalPrice = x.TotalPrice,
                Vat = x.Vat,
                IsDeliveryPaused = x.IsDeliveryPaused,
                DeliveryPausedFrom = x.DeliveryPausedFrom,
                DeliveryPausedTo = x.DeliveryPausedTo,
                DeliveryPauseReason = x.DeliveryPauseReason,
                DeliveryPauseType = x.DeliveryPauseType,
                DeliveryPausedBy = x.DeliveryPausedBy,
                ComplaintCount = _dbContext.ComplaintReportLines
                    .Where(line =>
                        line.IsActive &&
                        line.ComplaintReport.IsActive &&
                        line.SourceMerchandiseOrderDetail.MerchandiseOrderId == x.MerchandiseOrderId)
                    .Select(line => line.ComplaintReportId)
                    .Distinct()
                    .Count(),
                HasComplaint = _dbContext.ComplaintReportLines.Any(line =>
                    line.IsActive &&
                    line.ComplaintReport.IsActive &&
                    line.SourceMerchandiseOrderDetail.MerchandiseOrderId == x.MerchandiseOrderId),
                LatestComplaintExternalId = _dbContext.ComplaintReportLines
                    .Where(line =>
                        line.IsActive &&
                        line.ComplaintReport.IsActive &&
                        line.SourceMerchandiseOrderDetail.MerchandiseOrderId == x.MerchandiseOrderId)
                    .OrderByDescending(line => line.ComplaintReport.ReportedAt)
                    .Select(line => line.ComplaintReport.ExternalId)
                    .FirstOrDefault(),
                LatestComplaintStatus = _dbContext.ComplaintReportLines
                    .Where(line =>
                        line.IsActive &&
                        line.ComplaintReport.IsActive &&
                        line.SourceMerchandiseOrderDetail.MerchandiseOrderId == x.MerchandiseOrderId)
                    .OrderByDescending(line => line.ComplaintReport.ReportedAt)
                    .Select(line => line.ComplaintReport.Status.ToString())
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        ApplyEffectivePause(items, now);
        ApplyVatInclusiveTotalPrices(items);
        var orderIds = items.Select(x => x.MerchandiseOrderId).ToArray();
        var logs = await BuildLogQuery()
            .Where(x => orderIds.Contains(x.SourceId))
            .Where(x => !request.CreatedBy.HasValue || x.CreatedBy == request.CreatedBy.Value)
            .Where(x => !request.CompanyId.HasValue || x.CompanyId == request.CompanyId.Value)
            .Where(x => !request.EventType.HasValue || x.EventType == request.EventType.Value)
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.CreatedDate)
            .ToListAsync(cancellationToken);

        var logsBySource = logs
            .GroupBy(x => x.SourceId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<TimelineItemDto>)x.ToList());
        foreach (var item in items)
        {
            item.Details = logsBySource.TryGetValue(item.MerchandiseOrderId, out var itemLogs)
                ? itemLogs
                : Array.Empty<TimelineItemDto>();
        }

        return new PagedResult<SaleOrderTimelineCardDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private IQueryable<HRM.Domain.Entities.OrderSchema.MerchandiseOrder> ApplyCreatedScopeFilter(
        IQueryable<HRM.Domain.Entities.OrderSchema.MerchandiseOrder> query,
        TimelineCreatedScope createdScope,
        DateTime? from,
        DateTime? toExclusive)
    {
        if (!from.HasValue && !toExclusive.HasValue)
        {
            return query;
        }

        return createdScope switch
        {
            TimelineCreatedScope.Manufacturing => query.Where(order => _dbContext.MfgOrderPOs.Any(link =>
                link.IsActive &&
                link.Detail.IsActive &&
                link.Detail.MerchandiseOrderId == order.MerchandiseOrderId &&
                link.ProductionOrder.IsActive &&
                (!from.HasValue || link.ProductionOrder.CreatedDate >= from.Value) &&
                (!toExclusive.HasValue || link.ProductionOrder.CreatedDate < toExclusive.Value))),
            TimelineCreatedScope.Delivery => query.Where(order => order.DeliveryOrderPOs.Any(link =>
                link.IsActive &&
                link.DeliveryOrder.IsActive &&
                (!from.HasValue || link.DeliveryOrder.CreatedDate >= from.Value) &&
                (!toExclusive.HasValue || link.DeliveryOrder.CreatedDate < toExclusive.Value))),
            TimelineCreatedScope.Requisition => query.Where(order => order.MerchandiseOrderDetails.Any(detail =>
                detail.IsActive &&
                (!from.HasValue || detail.DeliveryRequestDate >= from.Value) &&
                (!toExclusive.HasValue || detail.DeliveryRequestDate < toExclusive.Value))),
            _ => query.Where(order =>
                (!from.HasValue || order.CreateDate >= from.Value) &&
                (!toExclusive.HasValue || order.CreateDate < toExclusive.Value))
        };
    }

    private static void ApplyVatInclusiveTotalPrices(IReadOnlyCollection<SaleOrderTimelineCardDto> items)
    {
        foreach (var item in items)
        {
            if (!item.TotalPrice.HasValue)
            {
                continue;
            }

            var vatRate = item.Vat ?? 0m;
            item.TotalPrice = decimal.Round(
                item.TotalPrice.Value * (1m + vatRate / 100m),
                2,
                MidpointRounding.AwayFromZero);
        }
    }

    private IQueryable<TimelineItemDto> BuildLogQuery()
    {
        return _dbContext.EventLogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new TimelineItemDto
            {
                SourceId = x.SourceId,
                SourceCode = x.SourceCode,
                EventType = x.EventType,
                Status = x.Status,
                Note = x.Note,
                CreatedDate = x.CreatedDate,
                CreatedBy = x.EmployeeID,
                CreatedByName = x.CreatedByNavigation!.FullName,
                CompanyId = x.CompanyId,
                CompanyName = x.Company!.Name
            });
    }

    private static void ApplyEffectivePause(List<SaleOrderTimelineCardDto> items, DateTime now)
    {
        foreach (var item in items)
        {
            var pausedActive =
                item.Status != MerchadiseStatus.Cancelled.ToString() &&
                item.IsDeliveryPaused &&
                (!item.DeliveryPausedFrom.HasValue || item.DeliveryPausedFrom.Value.Date <= now.Date) &&
                (!item.DeliveryPausedTo.HasValue || item.DeliveryPausedTo.Value.Date >= now.Date);

            item.IsDeliveryPaused = pausedActive;
            if (pausedActive)
            {
                item.Status = MerchadiseStatus.Paused.ToString();
            }
        }
    }
}
