using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Deliveries;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

/// <summary>
/// Kiểm tra visibility của SaleOrder rồi tổng hợp MFG, delivery, nhập kho và EventLog theo từng
/// MerchandiseOrderDetailId để tránh trộn các dòng có cùng ProductId.
/// </summary>
internal sealed class GetSaleOrderTimelineDetailQueryHandler
    : IRequestHandler<GetSaleOrderTimelineDetailQuery, PagedResult<SaleOrderTimelineDetailRowDto>>
{
    private readonly ITimelineDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;

    public GetSaleOrderTimelineDetailQueryHandler(
        ITimelineDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
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

        var scope = await _visibilityService.BuildReadScopeAsync(cancellationToken);
        var canConfirmOtherOwners = StandardSampleConfirmationRules.CanConfirmOtherOwners(_currentUser);
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
                x.FormulaId,
                x.Product.ColourCode ?? x.Product.Code ?? x.ProductExternalIdSnapshot,
                x.Product.Name ?? x.ProductNameSnapshot,
                x.UnitPriceAgreed,
                x.DeliveryRequestDate,
                x.ExpectedDeliveryDate,
                x.ExpectedQuantity,
                x.Status,
                _currentUser.IsAuthenticated && scope.EmployeeId != Guid.Empty
                    && (x.MerchandiseOrder.ManagerById == scope.EmployeeId || canConfirmOtherOwners)
                    && x.MerchandiseOrder.IsActive && x.MerchandiseOrder.CompanyId == scope.CompanyId
                    && x.MerchandiseOrder.Status != nameof(MerchadiseStatus.Cancelled)
                    && x.MerchandiseOrder.Status != nameof(MerchadiseStatus.Delivered)
                    && x.MerchandiseOrder.Status != nameof(MerchadiseStatus.Completed)
                    && x.Status == nameof(MerchadiseStatus.WaitingForStandardSample)))
            .ToListAsync(cancellationToken);

        var detailIds = details.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var mfgLinks = await _dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.MerchandiseOrderDetailId) &&
                x.ProductionOrder.CompanyId == scope.CompanyId &&
                x.ProductionOrder.IsActive)
            .Select(x => new MfgLinkRow(
                x.MerchandiseOrderDetailId,
                x.MfgProductionOrderId,
                x.ProductionOrder.ExternalId,
                x.ProductionOrder.CreatedDate,
                x.ProductionOrder.UpdatedDate,
                x.ProductionOrder.ExpectedDate,
                x.ProductionOrder.Status))
            .ToListAsync(cancellationToken);

        var deliveries = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .EffectiveDeliveryLines(scope.CompanyId)
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
        var formulaCodes = await _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == scope.CompanyId && mfgIds.Contains(x.MfgProductionOrderId))
            .SelectMany(x => x.ProductionSelectVersions.Where(version =>
                version.CompanyId == scope.CompanyId &&
                version.ValidFrom.HasValue &&
                version.ManufacturingFormula != null &&
                version.ManufacturingFormula.CompanyId == scope.CompanyId), (mfg, version) => new
            {
                mfg.MfgProductionOrderId,
                version.ValidFrom,
                ExternalId = version.ManufacturingFormula!.ExternalId
            })
            .OrderBy(x => x.ValidFrom)
            .ThenBy(x => x.ExternalId)
            .ToListAsync(cancellationToken);
        var formulaCodesByMfg = formulaCodes
            .ToLookup(x => x.MfgProductionOrderId, x => x.ExternalId);

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

        logs.AddRange(await SaleOrderWarehouseReceiptTimelineQuery.LoadAsync(
            _dbContext, scope.CompanyId, mfgIds, cancellationToken));

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

            var allRowLogs = rowMfgs.DistinctBy(x => x.MfgProductionOrderId)
                .SelectMany(mfg => logsByMfg.TryGetValue(mfg.MfgProductionOrderId, out var mfgLogs)
                    ? mfgLogs
                    : Enumerable.Empty<TimelineItemDto>())
                .ToList();
            var receipts = allRowLogs.Where(x => x.ReceiptQuantity.HasValue).ToList();
            var importedQuantity = receipts.Sum(x => x.ReceiptQuantity ?? 0);
            var warehouseEvents = SaleOrderWarehouseReceiptTimelineRules.BuildEvents(
                receipts, detail.RequestQuantity, request.Status);
            var rowLogs = allRowLogs.Where(x => !x.ReceiptQuantity.HasValue)
                .OrderBy(x => x.CreatedDate).Concat(warehouseEvents).ToList();
            var deliveredQuantity = rowDeliveries.Sum(x => x.Quantity);

            return new SaleOrderTimelineDetailRowDto
            {
                SaleOrderDetailStatus = detail.Status,
                CurrentMfgStatus = rowMfgs.OrderByDescending(x => x.UpdatedDate)
                    .ThenByDescending(x => x.CreatedDate).ThenByDescending(x => x.MfgProductionOrderId)
                    .Select(x => x.Status).FirstOrDefault(),
                ProductId = detail.ProductId,
                FormulaId = detail.FormulaId,
                CanConfirmStandardSample = detail.CanConfirmStandardSample,
                MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                ExternalId = string.Join(", ", rowMfgs
                    .SelectMany(x => formulaCodesByMfg[x.MfgProductionOrderId])
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                ColourCode = detail.ProductCode,
                ProductName = detail.ProductName,
                UnitPrice = detail.UnitPrice,
                RequestDate = detail.RequestDate,
                ExpectedDate = rowMfgs.LastOrDefault()?.ExpectedDate,
                RequestQuantity = detail.RequestQuantity,
                ImportedQuantity = importedQuantity,
                WarehouseStatus = SaleOrderWarehouseReceiptTimelineRules.ResolveStatus(importedQuantity, detail.RequestQuantity),
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
        Guid FormulaId,
        string ProductCode,
        string ProductName,
        decimal UnitPrice,
        DateTime RequestDate,
        DateTime? ExpectedDate,
        decimal RequestQuantity,
        string Status,
        bool CanConfirmStandardSample);

    private sealed record MfgLinkRow(
        Guid MerchandiseOrderDetailId,
        Guid MfgProductionOrderId,
        string ExternalId,
        DateTime CreatedDate,
        DateTime UpdatedDate,
        DateTime? ExpectedDate,
        string Status);

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
