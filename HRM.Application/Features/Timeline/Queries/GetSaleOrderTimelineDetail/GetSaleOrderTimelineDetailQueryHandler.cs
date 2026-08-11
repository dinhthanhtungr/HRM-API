using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Deliveries;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

/// <summary>
/// Kiểm tra visibility của SaleOrder rồi tổng hợp MFG, delivery, số lượng giao và EventLog theo từng
/// MerchandiseOrderDetailId để tránh trộn các dòng có cùng ProductId.
/// </summary>
internal sealed class GetSaleOrderTimelineDetailQueryHandler
    : IRequestHandler<GetSaleOrderTimelineDetailQuery, PagedResult<SaleOrderTimelineDetailRowDto>>
{
    private readonly ITimelineDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSaleOrderTimelineDetailQueryHandler(
        ITimelineDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<SaleOrderTimelineDetailRowDto>> Handle(
        GetSaleOrderTimelineDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return new PagedResult<SaleOrderTimelineDetailRowDto>(
                Array.Empty<SaleOrderTimelineDetailRowDto>(),
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize);
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var canAccess = await _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.MerchandiseOrderId == request.Id, cancellationToken);
        if (!canAccess)
        {
            return new PagedResult<SaleOrderTimelineDetailRowDto>(
                Array.Empty<SaleOrderTimelineDetailRowDto>(),
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize);
        }

        var detailQuery = _dbContext.MerchandiseOrderDetails
            .AsNoTracking()
            .Where(x => x.MerchandiseOrderId == request.Id && x.IsActive);

        var totalCount = await detailQuery.CountAsync(cancellationToken);
        var details = await detailQuery
            .OrderBy(x => x.MerchandiseOrderDetailId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new DetailRow(
                x.MerchandiseOrderDetailId,
                x.ProductId,
                x.ProductExternalIdSnapshot,
                x.ProductNameSnapshot,
                x.UnitPriceAgreed,
                x.DeliveryRequestDate,
                x.ExpectedDeliveryDate,
                x.ExpectedQuantity))
            .ToListAsync(cancellationToken);

        var detailIds = details.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var mfgLinks = await _dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.MerchandiseOrderDetailId) &&
                x.ProductionOrder.IsActive)
            .Select(x => new MfgLinkRow(
                x.MerchandiseOrderDetailId,
                x.MfgProductionOrderId,
                x.ProductionOrder.ExternalId,
                x.ProductionOrder.CreatedDate,
                x.ProductionOrder.UpdatedDate,
                x.ProductionOrder.ExpectedDate))
            .ToListAsync(cancellationToken);

        var deliveries = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.IsAttach &&
                x.MerchandiseOrderDetailId.HasValue &&
                detailIds.Contains(x.MerchandiseOrderDetailId.Value) &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.CompanyId == scope.CompanyId)
            .Select(x => new DeliveryRow(
                x.Id,
                x.MerchandiseOrderDetailId!.Value,
                x.DeliveryOrder.ExternalId,
                x.LotNoList,
                x.Quantity,
                x.DeliveryOrder.CreatedDate))
            .ToListAsync(cancellationToken);

        var deliveryDetailIds = deliveries.Select(x => x.DeliveryOrderDetailId).ToArray();
        var deliveryLotRows = await _dbContext.DeliveryOrderDetailLotConsumptions
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                deliveryDetailIds.Contains(x.DeliveryOrderDetailId) &&
                x.DeliveryOrderDetail.DeliveryOrder.CompanyId == scope.CompanyId)
            .Select(x => new { x.DeliveryOrderDetailId, x.LotNo })
            .ToListAsync(cancellationToken);
        var deliveryLotsByDetail = deliveryLotRows
            .GroupBy(x => x.DeliveryOrderDetailId)
            .ToDictionary(x => x.Key, x => x.Select(lot => lot.LotNo).ToArray());

        var mfgIds = mfgLinks.Select(x => x.MfgProductionOrderId).Distinct().ToArray();
        var complaints = await _dbContext.ComplaintReportLines
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.ComplaintReport.IsActive &&
                detailIds.Contains(x.SourceMerchandiseOrderDetailId))
            .Select(x => new ComplaintRow(
                x.SourceMerchandiseOrderDetailId,
                x.ComplaintReportId,
                x.ComplaintReportLineId,
                x.ComplaintReport.ExternalId,
                x.ComplaintReport.Status.ToString(),
                x.ComplaintReport.ResolutionType.HasValue
                    ? x.ComplaintReport.ResolutionType.Value.ToString()
                    : string.Empty,
                x.ComplaintReport.Summary,
                x.ComplaintQuantity,
                x.ApprovedReplacementQuantity,
                x.ComplaintReport.ReportedAt,
                x.ComplaintReport.ProcessingMerchandiseOrders
                    .Where(order => order.IsActive)
                    .Select(order => (Guid?)order.MerchandiseOrderId)
                    .FirstOrDefault(),
                x.ComplaintReport.ProcessingMerchandiseOrders
                    .Where(order => order.IsActive)
                    .Select(order => order.ExternalId)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var logs = await _dbContext.EventLogs
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                mfgIds.Contains(x.SourceId) &&
                (string.IsNullOrWhiteSpace(request.Status) || x.Status == request.Status.Trim()))
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.CreatedDate)
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
            })
            .ToListAsync(cancellationToken);

        var mfgByDetail = mfgLinks
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .ToDictionary(x => x.Key, x => x.OrderBy(m => m.CreatedDate).ThenBy(m => m.ExternalId).ToList());
        var deliveryByDetail = deliveries
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .ToDictionary(x => x.Key, x => x.OrderBy(d => d.CreatedDate).ToList());
        var logsByMfg = logs
            .GroupBy(x => x.SourceId)
            .ToDictionary(x => x.Key, x => x.OrderBy(log => log.CreatedDate).ToList());
        var complaintsByDetail = complaints
            .GroupBy(x => x.SourceMerchandiseOrderDetailId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(item => item.ReportedAt).ToList());

        var rows = details.Select(detail =>
        {
            mfgByDetail.TryGetValue(detail.MerchandiseOrderDetailId, out var rowMfgs);
            deliveryByDetail.TryGetValue(detail.MerchandiseOrderDetailId, out var rowDeliveries);
            rowMfgs ??= new List<MfgLinkRow>();
            rowDeliveries ??= new List<DeliveryRow>();
            complaintsByDetail.TryGetValue(detail.MerchandiseOrderDetailId, out var rowComplaints);
            rowComplaints ??= new List<ComplaintRow>();

            var rowLogs = rowMfgs
                .SelectMany(mfg => logsByMfg.TryGetValue(mfg.MfgProductionOrderId, out var mfgLogs)
                    ? mfgLogs
                    : Enumerable.Empty<TimelineItemDto>())
                .OrderBy(x => x.CreatedDate)
                .ToList();
            var deliveredQuantity = rowDeliveries.Sum(x => x.Quantity);

            return new SaleOrderTimelineDetailRowDto
            {
                MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                ExternalId = string.Join(", ", rowMfgs.Select(x => x.ExternalId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                ColourCode = detail.ProductExternalIdSnapshot,
                ProductName = detail.ProductNameSnapshot,
                UnitPrice = detail.UnitPrice,
                RequestDate = detail.RequestDate,
                ExpectedDate = rowMfgs.LastOrDefault()?.ExpectedDate,
                RequestQuantity = detail.RequestQuantity,
                DeliveredQuantity = deliveredQuantity,
                RemainingQuantity = detail.RequestQuantity - deliveredQuantity,
                Deliveries = rowDeliveries.Select(x => new DeliveryInfoDto
                {
                    DOExternalId = x.ExternalId,
                    LotNoList = DeliveryOrderLotReadRules.ResolveDisplay(
                        deliveryLotsByDetail.GetValueOrDefault(x.DeliveryOrderDetailId, []),
                        x.LotNoList),
                    QuantityDelivery = x.Quantity,
                    CreatedDate = x.CreatedDate
                }).ToList(),
                Complaints = rowComplaints.Select(x => new SaleOrderComplaintInfoDto
                {
                    ComplaintReportId = x.ComplaintReportId,
                    ComplaintReportLineId = x.ComplaintReportLineId,
                    ExternalId = x.ExternalId,
                    Status = x.Status,
                    ResolutionType = x.ResolutionType,
                    Summary = x.Summary,
                    ComplaintQuantity = x.ComplaintQuantity,
                    ApprovedReplacementQuantity = x.ApprovedReplacementQuantity,
                    ReportedAt = x.ReportedAt,
                    HandlingMerchandiseOrderId = x.HandlingOrderId,
                    HandlingMerchandiseOrderExternalId = x.HandlingOrderExternalId
                }).ToList(),
                Details = rowLogs
            };
        }).ToList();

        return new PagedResult<SaleOrderTimelineDetailRowDto>(
            rows,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private sealed record DetailRow(
        Guid MerchandiseOrderDetailId,
        Guid ProductId,
        string ProductExternalIdSnapshot,
        string ProductNameSnapshot,
        decimal UnitPrice,
        DateTime RequestDate,
        DateTime? ExpectedDate,
        decimal RequestQuantity);

    private sealed record MfgLinkRow(
        Guid MerchandiseOrderDetailId,
        Guid MfgProductionOrderId,
        string ExternalId,
        DateTime CreatedDate,
        DateTime UpdatedDate,
        DateTime? ExpectedDate);

    private sealed record DeliveryRow(
        Guid DeliveryOrderDetailId,
        Guid MerchandiseOrderDetailId,
        string? ExternalId,
        string? LotNoList,
        decimal Quantity,
        DateTime? CreatedDate);

    private sealed record ComplaintRow(
        Guid SourceMerchandiseOrderDetailId,
        Guid ComplaintReportId,
        Guid ComplaintReportLineId,
        string ExternalId,
        string Status,
        string ResolutionType,
        string? Summary,
        decimal ComplaintQuantity,
        decimal? ApprovedReplacementQuantity,
        DateTime ReportedAt,
        Guid? HandlingOrderId,
        string? HandlingOrderExternalId);
}
