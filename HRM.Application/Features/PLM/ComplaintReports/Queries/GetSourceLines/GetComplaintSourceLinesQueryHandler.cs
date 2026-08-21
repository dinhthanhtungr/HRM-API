using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Commons.Deliveries;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Queries;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetSourceLines;

internal sealed class GetComplaintSourceLinesQueryHandler
    : IRequestHandler<GetComplaintSourceLinesQuery, IReadOnlyList<ComplaintSourceLineDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetComplaintSourceLinesQueryHandler(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<IReadOnlyList<ComplaintSourceLineDto>> Handle(
        GetComplaintSourceLinesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Array.Empty<ComplaintSourceLineDto>();
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleOrders = _visibilityService.ApplyMerchandiseOrderVisibility(
                _dbContext.MerchandiseOrders.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CustomerId == request.CustomerId &&
                x.IsActive &&
                x.Status != MerchadiseStatus.Cancelled.ToString());

        if (request.MerchandiseOrderId is { } orderId && orderId != Guid.Empty)
        {
            visibleOrders = visibleOrders.Where(x => x.MerchandiseOrderId == orderId);
        }

        var deliveryTotals = _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.IsAttach &&
                x.MerchandiseOrderDetailId.HasValue &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.CompanyId == scope.CompanyId &&
                x.DeliveryOrder.Status != DeliveryOrderStatus.Canceled.ToString() &&
                x.DeliveryOrder.Status != "Cancelled")
            .GroupBy(x => x.MerchandiseOrderDetailId!.Value)
            .Select(group => new
            {
                MerchandiseOrderDetailId = group.Key,
                DeliveredQuantity = group.Sum(x => x.Quantity)
            });

        var baseQuery =
            from detail in _dbContext.MerchandiseOrderDetails.AsNoTracking()
            join order in visibleOrders on detail.MerchandiseOrderId equals order.MerchandiseOrderId
            join delivery in deliveryTotals on detail.MerchandiseOrderDetailId equals delivery.MerchandiseOrderDetailId
            where detail.IsActive && delivery.DeliveredQuantity > 0
            select new
            {
                order.MerchandiseOrderId,
                MerchandiseOrderExternalId = order.ExternalId,
                OrderCreatedDate = order.CreateDate,
                detail.MerchandiseOrderDetailId,
                detail.ProductId,
                ProductExternalId = detail.ProductExternalIdSnapshot,
                ProductName = detail.ProductNameSnapshot,
                detail.FormulaId,
                FormulaExternalId = detail.FormulaExternalIdSnapshot,
                OrderedQuantity = detail.ExpectedQuantity,
                delivery.DeliveredQuantity,
                detail.BagType,
                detail.PackageWeight
            };

        if (request.NormalizedKeyword is { } keyword)
        {
            baseQuery = baseQuery.Where(x =>
                x.MerchandiseOrderExternalId.Contains(keyword) ||
                x.ProductExternalId.Contains(keyword) ||
                x.ProductName.Contains(keyword) ||
                EF.Functions.ILike(x.FormulaExternalId, $"%{keyword}%"));
        }

        var sourceRows = await baseQuery
            .OrderByDescending(x => x.OrderCreatedDate)
            .ThenBy(x => x.ProductExternalId)
            .Take(request.NormalizedTake)
            .ToListAsync(cancellationToken);
        if (sourceRows.Count == 0)
        {
            return Array.Empty<ComplaintSourceLineDto>();
        }

        var detailIds = sourceRows.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var deliveryRows = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.IsAttach &&
                x.MerchandiseOrderDetailId.HasValue &&
                detailIds.Contains(x.MerchandiseOrderDetailId.Value) &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.CompanyId == scope.CompanyId &&
                x.DeliveryOrder.Status != DeliveryOrderStatus.Canceled.ToString() &&
                x.DeliveryOrder.Status != "Cancelled")
            .Select(x => new
            {
                DeliveryOrderDetailId = x.Id,
                MerchandiseOrderDetailId = x.MerchandiseOrderDetailId!.Value,
                DeliveryOrderId = x.DeliveryOrderId,
                DeliveryOrderExternalId = x.DeliveryOrder.ExternalId ?? string.Empty,
                x.DeliveryOrder.Status,
                DeliveredQuantity = x.Quantity,
                x.LotNoList,
                DeliveryDate = x.DeliveryOrder.UpdatedDate ?? x.DeliveryOrder.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var deliveryDetailIds = deliveryRows.Select(x => x.DeliveryOrderDetailId).ToArray();
        var lotRows = await _dbContext.DeliveryOrderDetailLotConsumptions
            .AsNoTracking()
            .Where(x => x.IsActive && deliveryDetailIds.Contains(x.DeliveryOrderDetailId))
            .Select(x => new
            {
                LotConsumptionId = x.Id,
                x.DeliveryOrderDetailId,
                x.LotNo,
                DeliveredQuantity = x.Quantity,
                DeliveryDate = x.DeliveryOrderDetail.DeliveryOrder.UpdatedDate ?? x.DeliveryOrderDetail.DeliveryOrder.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var activeComplaintByDetail = await _dbContext.ComplaintReportLines
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.SourceMerchandiseOrderDetailId) &&
                x.ComplaintReport.IsActive &&
                x.ComplaintReport.CompanyId == scope.CompanyId &&
                x.ComplaintReport.Status != ComplaintReportStatus.Cancelled &&
                x.ComplaintReport.Status != ComplaintReportStatus.Rejected)
            .GroupBy(x => x.SourceMerchandiseOrderDetailId)
            .Select(group => new { DetailId = group.Key, Quantity = group.Sum(x => x.ComplaintQuantity) })
            .ToDictionaryAsync(x => x.DetailId, x => x.Quantity, cancellationToken);

        var lotConsumptionIds = lotRows.Select(x => x.LotConsumptionId).ToArray();
        var activeComplaintByLot = await _dbContext.ComplaintReportLineLots
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.SourceLotConsumptionId.HasValue &&
                lotConsumptionIds.Contains(x.SourceLotConsumptionId.Value) &&
                x.ComplaintReportLine.IsActive &&
                x.ComplaintReportLine.ComplaintReport.IsActive &&
                x.ComplaintReportLine.ComplaintReport.CompanyId == scope.CompanyId &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Cancelled &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Rejected)
            .GroupBy(x => x.SourceLotConsumptionId!.Value)
            .Select(group => new { LotId = group.Key, Quantity = group.Sum(x => x.ComplaintQuantity) })
            .ToDictionaryAsync(x => x.LotId, x => x.Quantity, cancellationToken);

        var activeComplaintByLegacyDelivery = await _dbContext.ComplaintReportLineLots
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.SourceLotConsumptionId.HasValue &&
                deliveryDetailIds.Contains(x.SourceDeliveryOrderDetailId) &&
                x.ComplaintReportLine.IsActive &&
                x.ComplaintReportLine.ComplaintReport.IsActive &&
                x.ComplaintReportLine.ComplaintReport.CompanyId == scope.CompanyId &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Cancelled &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Rejected)
            .GroupBy(x => x.SourceDeliveryOrderDetailId)
            .Select(group => new { DeliveryId = group.Key, Quantity = group.Sum(x => x.ComplaintQuantity) })
            .ToDictionaryAsync(x => x.DeliveryId, x => x.Quantity, cancellationToken);

        var mfgRows = await _dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.MerchandiseOrderDetailId) &&
                x.ProductionOrder.CompanyId == scope.CompanyId)
            .Select(x => new
            {
                x.MerchandiseOrderDetailId,
                x.MfgProductionOrderId,
                MfgExternalId = x.ProductionOrder.ExternalId,
                x.ProductionOrder.CreatedDate,
                CurrentFormula = x.ProductionOrder.ProductionSelectVersions
                    .Where(version => version.ValidTo == null && version.CompanyId == scope.CompanyId)
                    .OrderByDescending(version => version.ValidFrom)
                    .ThenByDescending(version => version.ProductionSelectVersionId)
                    .Select(version => new
                    {
                        version.ManufacturingFormulaId,
                        ExternalId = version.ManufacturingFormula != null
                            ? version.ManufacturingFormula.ExternalId
                            : null
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var mfgByDetail = mfgRows
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(row => row.CreatedDate).First());

        var lotsByDelivery = lotRows
            .GroupBy(x => x.DeliveryOrderDetailId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ComplaintSourceLotDto>)group.Select(lot =>
                {
                    var complained = activeComplaintByLot.GetValueOrDefault(lot.LotConsumptionId);
                    return new ComplaintSourceLotDto
                    {
                        LotConsumptionId = lot.LotConsumptionId,
                        LotNo = lot.LotNo,
                        DeliveredQuantity = lot.DeliveredQuantity,
                        DeliveryDate = lot.DeliveryDate,
                        ActiveComplaintQuantity = complained,
                        RemainingComplaintableQuantity = ComplaintReportReadRules.RemainingComplaintableQuantity(
                            lot.DeliveredQuantity,
                            complained)
                    };
                }).ToList());

        var deliveriesByDetail = deliveryRows
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ComplaintSourceDeliveryDto>)group
                    .OrderByDescending(x => x.DeliveryDate)
                    .Select(delivery => new ComplaintSourceDeliveryDto
                    {
                        DeliveryOrderDetailId = delivery.DeliveryOrderDetailId,
                        DeliveryOrderId = delivery.DeliveryOrderId,
                        DeliveryOrderExternalId = delivery.DeliveryOrderExternalId,
                        Status = delivery.Status,
                        DeliveredQuantity = delivery.DeliveredQuantity,
                        DeliveryDate = delivery.DeliveryDate,
                        Lots = lotsByDelivery.TryGetValue(delivery.DeliveryOrderDetailId, out var normalizedLots)
                            ? normalizedLots
                            : BuildLegacyFallbackLots(
                                delivery.DeliveryOrderDetailId,
                                delivery.LotNoList,
                                delivery.DeliveredQuantity,
                                delivery.DeliveryDate,
                                activeComplaintByLegacyDelivery)
                    })
                    .ToList());

        return sourceRows.Select(row =>
        {
            var complained = activeComplaintByDetail.GetValueOrDefault(row.MerchandiseOrderDetailId);
            mfgByDetail.TryGetValue(row.MerchandiseOrderDetailId, out var mfg);
            return new ComplaintSourceLineDto
            {
                MerchandiseOrderId = row.MerchandiseOrderId,
                MerchandiseOrderExternalId = row.MerchandiseOrderExternalId,
                OrderCreatedDate = row.OrderCreatedDate,
                MerchandiseOrderDetailId = row.MerchandiseOrderDetailId,
                ProductId = row.ProductId,
                ProductExternalId = row.ProductExternalId,
                ProductName = row.ProductName,
                FormulaId = row.FormulaId,
                FormulaExternalId = row.FormulaExternalId,
                OrderedQuantity = row.OrderedQuantity,
                DeliveredQuantity = row.DeliveredQuantity,
                ActiveComplaintQuantity = complained,
                RemainingComplaintableQuantity = ComplaintReportReadRules.RemainingComplaintableQuantity(
                    row.DeliveredQuantity,
                    complained),
                BagType = row.BagType,
                PackageWeight = row.PackageWeight,
                SourceMfgProductionOrderId = mfg?.MfgProductionOrderId,
                SourceMfgProductionOrderExternalId = mfg?.MfgExternalId,
                ManufacturingFormulaId = mfg?.CurrentFormula?.ManufacturingFormulaId,
                ManufacturingFormulaExternalId = mfg?.CurrentFormula?.ExternalId,
                Deliveries = deliveriesByDetail.GetValueOrDefault(
                    row.MerchandiseOrderDetailId,
                    Array.Empty<ComplaintSourceDeliveryDto>())
            };
        }).ToList();
    }

    private static IReadOnlyList<ComplaintSourceLotDto> BuildLegacyFallbackLots(
        Guid deliveryOrderDetailId,
        string? legacyLotNoList,
        decimal deliveredQuantity,
        DateTime? deliveryDate,
        IReadOnlyDictionary<Guid, decimal> complainedByDelivery)
    {
        var legacyLots = DeliveryOrderLotReadRules.SplitLegacy(legacyLotNoList);
        if (legacyLots.Count == 0)
        {
            return [];
        }

        var complained = complainedByDelivery.GetValueOrDefault(deliveryOrderDetailId);
        return
        [
            new ComplaintSourceLotDto
            {
                LotConsumptionId = null,
                LotNo = string.Join(", ", legacyLots),
                DeliveredQuantity = deliveredQuantity,
                DeliveryDate = deliveryDate,
                ActiveComplaintQuantity = complained,
                RemainingComplaintableQuantity = ComplaintReportReadRules.RemainingComplaintableQuantity(
                    deliveredQuantity,
                    complained)
            }
        ];
    }
}
