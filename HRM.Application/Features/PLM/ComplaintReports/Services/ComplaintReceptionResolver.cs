using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

internal sealed class ComplaintReceptionResolver
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public ComplaintReceptionResolver(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    internal async Task<OperationResult<ResolvedComplaintReception>> ResolveAsync(
        Guid customerId,
        IReadOnlyCollection<ComplaintReportLineRequest> requestedLines,
        Guid? excludedComplaintReportId,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleCustomers = _visibilityService.ApplyCustomerVisibility(
            _dbContext.Customers.AsNoTracking(),
            scope);
        var customer = await visibleCustomers
            .Where(x => x.CustomerId == customerId && x.CompanyId == scope.CompanyId)
            .Select(x => new { x.CustomerId, x.ExternalId, x.CustomerName })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<ResolvedComplaintReception>.Fail(
                "Không tìm thấy khách hàng hoặc bạn không có quyền truy cập.");
        }

        var detailIds = requestedLines.Select(x => x.SourceMerchandiseOrderDetailId).ToArray();
        var visibleOrders = _visibilityService.ApplyMerchandiseOrderVisibility(
            _dbContext.MerchandiseOrders.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);
        var sourceRows = await (
            from detail in _dbContext.MerchandiseOrderDetails.AsNoTracking()
            join order in visibleOrders on detail.MerchandiseOrderId equals order.MerchandiseOrderId
            where detailIds.Contains(detail.MerchandiseOrderDetailId) &&
                  detail.IsActive &&
                  order.IsActive &&
                  order.CompanyId == scope.CompanyId &&
                  order.CustomerId == customerId
            select new SourceRow(
                detail.MerchandiseOrderDetailId,
                order.MerchandiseOrderId,
                order.ExternalId,
                order.CustomerNameSnapshot,
                detail.ProductId,
                detail.ProductExternalIdSnapshot,
                detail.ProductNameSnapshot,
                detail.FormulaId,
                detail.FormulaExternalIdSnapshot))
            .ToListAsync(cancellationToken);
        if (sourceRows.Count != detailIds.Length)
        {
            return OperationResult<ResolvedComplaintReception>.Fail(
                "Một hoặc nhiều dòng đơn không tồn tại, không thuộc khách hàng hoặc nằm ngoài phạm vi truy cập.");
        }

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
            .Select(x => new DeliveryRow(
                x.Id,
                x.MerchandiseOrderDetailId!.Value,
                x.Quantity,
                x.LotNoList,
                x.DeliveryOrder.UpdatedDate ?? x.DeliveryOrder.CreatedDate))
            .ToListAsync(cancellationToken);
        var deliveryIds = deliveryRows.Select(x => x.DeliveryOrderDetailId).ToArray();
        var lotRows = await _dbContext.DeliveryOrderDetailLotConsumptions
            .AsNoTracking()
            .Where(x => x.IsActive && deliveryIds.Contains(x.DeliveryOrderDetailId))
            .Select(x => new LotRow(x.Id, x.DeliveryOrderDetailId, x.LotNo, x.Quantity))
            .ToListAsync(cancellationToken);

        var previousLineQuery = _dbContext.ComplaintReportLines
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.SourceMerchandiseOrderDetailId) &&
                x.ComplaintReport.IsActive &&
                x.ComplaintReport.CompanyId == scope.CompanyId &&
                x.ComplaintReport.Status != ComplaintReportStatus.Cancelled &&
                x.ComplaintReport.Status != ComplaintReportStatus.Rejected);
        if (excludedComplaintReportId.HasValue)
        {
            previousLineQuery = previousLineQuery.Where(x => x.ComplaintReportId != excludedComplaintReportId.Value);
        }

        var previousByLine = await previousLineQuery
            .GroupBy(x => x.SourceMerchandiseOrderDetailId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(x => x.ComplaintQuantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);

        var previousLotQuery = _dbContext.ComplaintReportLineLots
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                deliveryIds.Contains(x.SourceDeliveryOrderDetailId) &&
                x.ComplaintReportLine.IsActive &&
                x.ComplaintReportLine.ComplaintReport.IsActive &&
                x.ComplaintReportLine.ComplaintReport.CompanyId == scope.CompanyId &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Cancelled &&
                x.ComplaintReportLine.ComplaintReport.Status != ComplaintReportStatus.Rejected);
        if (excludedComplaintReportId.HasValue)
        {
            previousLotQuery = previousLotQuery.Where(
                x => x.ComplaintReportLine.ComplaintReportId != excludedComplaintReportId.Value);
        }

        var previousLotRows = await previousLotQuery
            .Select(x => new
            {
                x.SourceDeliveryOrderDetailId,
                x.SourceLotConsumptionId,
                x.ComplaintQuantity
            })
            .ToListAsync(cancellationToken);
        var previousByConsumption = previousLotRows
            .Where(x => x.SourceLotConsumptionId.HasValue)
            .GroupBy(x => x.SourceLotConsumptionId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.ComplaintQuantity));
        var previousByUnnormalizedDelivery = previousLotRows
            .Where(x => !x.SourceLotConsumptionId.HasValue)
            .GroupBy(x => x.SourceDeliveryOrderDetailId)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.ComplaintQuantity));

        var mfgRows = await _dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                detailIds.Contains(x.MerchandiseOrderDetailId) &&
                x.ProductionOrder.CompanyId == scope.CompanyId)
            .Select(x => new MfgRow(
                x.MerchandiseOrderDetailId,
                x.MfgProductionOrderId,
                x.ProductionOrder.ExternalId,
                x.ProductionOrder.CreatedDate,
                x.ProductionOrder.ProductionSelectVersions
                    .Where(version => version.ValidTo == null && version.CompanyId == scope.CompanyId)
                    .OrderByDescending(version => version.ValidFrom)
                    .ThenByDescending(version => version.ProductionSelectVersionId)
                    .Select(version => version.ManufacturingFormulaId)
                    .FirstOrDefault(),
                x.ProductionOrder.ProductionSelectVersions
                    .Where(version => version.ValidTo == null && version.CompanyId == scope.CompanyId)
                    .OrderByDescending(version => version.ValidFrom)
                    .ThenByDescending(version => version.ProductionSelectVersionId)
                    .Select(version => version.ManufacturingFormula != null
                        ? version.ManufacturingFormula.ExternalId
                        : null)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var sourceById = sourceRows.ToDictionary(x => x.DetailId);
        var deliveriesById = deliveryRows.ToDictionary(x => x.DeliveryOrderDetailId);
        var deliveriesByLine = deliveryRows.ToLookup(x => x.DetailId);
        var lotsById = lotRows.ToDictionary(x => x.LotConsumptionId);
        var lotCountByDelivery = lotRows.ToLookup(x => x.DeliveryOrderDetailId);
        var mfgByLine = mfgRows
            .GroupBy(x => x.DetailId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(row => row.CreatedDate).First());
        var resolvedLines = new List<ResolvedComplaintLine>(requestedLines.Count);

        foreach (var requestLine in requestedLines)
        {
            var source = sourceById[requestLine.SourceMerchandiseOrderDetailId];
            var delivered = deliveriesByLine[source.DetailId].Sum(x => x.DeliveredQuantity);
            var remaining = Math.Max(0, delivered - previousByLine.GetValueOrDefault(source.DetailId));
            if (delivered <= 0 || requestLine.ComplaintQuantity > remaining)
            {
                return OperationResult<ResolvedComplaintReception>.Fail(
                    $"Số lượng khiếu nại của dòng {source.OrderExternalId} vượt số lượng đã giao còn có thể khiếu nại.");
            }

            var resolvedLots = new List<ResolvedComplaintLot>(requestLine.Lots.Count);
            foreach (var requestLot in requestLine.Lots)
            {
                if (!deliveriesById.TryGetValue(requestLot.SourceDeliveryOrderDetailId, out var delivery) ||
                    delivery.DetailId != source.DetailId)
                {
                    return OperationResult<ResolvedComplaintReception>.Fail(
                        "Lot không thuộc delivery detail của dòng đơn nguồn đã chọn.");
                }

                if (requestLot.SourceLotConsumptionId is { } consumptionId)
                {
                    if (!lotsById.TryGetValue(consumptionId, out var lot) ||
                        lot.DeliveryOrderDetailId != delivery.DeliveryOrderDetailId)
                    {
                        return OperationResult<ResolvedComplaintReception>.Fail(
                            "Lot consumption không thuộc delivery detail đã chọn.");
                    }

                    var lotRemaining = Math.Max(0, lot.DeliveredQuantity - previousByConsumption.GetValueOrDefault(lot.LotConsumptionId));
                    if (requestLot.ComplaintQuantity > lotRemaining)
                    {
                        return OperationResult<ResolvedComplaintReception>.Fail(
                            $"Số lượng khiếu nại lot {lot.LotNo} vượt số lượng còn lại.");
                    }

                    resolvedLots.Add(new ResolvedComplaintLot(
                        delivery.DeliveryOrderDetailId,
                        lot.LotConsumptionId,
                        lot.LotNo,
                        lot.DeliveredQuantity,
                        requestLot.ComplaintQuantity,
                        delivery.DeliveredAt));
                    continue;
                }

                if (lotCountByDelivery[delivery.DeliveryOrderDetailId].Any() ||
                    string.IsNullOrWhiteSpace(delivery.LotNoList))
                {
                    return OperationResult<ResolvedComplaintReception>.Fail(
                        "Delivery detail có lot chuẩn hóa hoặc thiếu mã lot; SourceLotConsumptionId là bắt buộc.");
                }

                var unnormalizedRemaining = Math.Max(
                    0,
                    delivery.DeliveredQuantity - previousByUnnormalizedDelivery.GetValueOrDefault(delivery.DeliveryOrderDetailId));
                if (requestLot.ComplaintQuantity > unnormalizedRemaining)
                {
                    return OperationResult<ResolvedComplaintReception>.Fail(
                        $"Số lượng khiếu nại lot {delivery.LotNoList} vượt số lượng còn lại.");
                }

                resolvedLots.Add(new ResolvedComplaintLot(
                    delivery.DeliveryOrderDetailId,
                    null,
                    delivery.LotNoList.Trim(),
                    delivery.DeliveredQuantity,
                    requestLot.ComplaintQuantity,
                    delivery.DeliveredAt));
            }

            mfgByLine.TryGetValue(source.DetailId, out var mfg);
            resolvedLines.Add(new ResolvedComplaintLine(
                source,
                requestLine.ComplaintQuantity,
                Normalize(requestLine.IssueType),
                Normalize(requestLine.Severity),
                Normalize(requestLine.Description),
                mfg?.MfgProductionOrderId,
                mfg?.MfgExternalId,
                mfg?.ManufacturingFormulaId,
                mfg?.ManufacturingFormulaExternalId,
                resolvedLots));
        }

        return OperationResult<ResolvedComplaintReception>.Ok(new ResolvedComplaintReception(
            customer.CustomerId,
            customer.ExternalId,
            customer.CustomerName,
            resolvedLines));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal sealed record ResolvedComplaintReception(
        Guid CustomerId,
        string CustomerExternalId,
        string CustomerName,
        IReadOnlyList<ResolvedComplaintLine> Lines);

    internal sealed record ResolvedComplaintLine(
        SourceRow Source,
        decimal ComplaintQuantity,
        string? IssueType,
        string? Severity,
        string? Description,
        Guid? SourceMfgProductionOrderId,
        string? SourceMfgProductionOrderExternalId,
        Guid? ManufacturingFormulaId,
        string? ManufacturingFormulaExternalId,
        IReadOnlyList<ResolvedComplaintLot> Lots);

    internal sealed record ResolvedComplaintLot(
        Guid SourceDeliveryOrderDetailId,
        Guid? SourceLotConsumptionId,
        string LotNo,
        decimal DeliveredQuantity,
        decimal ComplaintQuantity,
        DateTime? DeliveredAt);

    internal sealed record SourceRow(
        Guid DetailId,
        Guid OrderId,
        string OrderExternalId,
        string CustomerNameSnapshot,
        Guid ProductId,
        string ProductExternalId,
        string ProductName,
        Guid FormulaId,
        string FormulaExternalId);

    private sealed record DeliveryRow(
        Guid DeliveryOrderDetailId,
        Guid DetailId,
        decimal DeliveredQuantity,
        string? LotNoList,
        DateTime? DeliveredAt);

    private sealed record LotRow(
        Guid LotConsumptionId,
        Guid DeliveryOrderDetailId,
        string LotNo,
        decimal DeliveredQuantity);

    private sealed record MfgRow(
        Guid DetailId,
        Guid MfgProductionOrderId,
        string MfgExternalId,
        DateTime CreatedDate,
        Guid? ManufacturingFormulaId,
        string? ManufacturingFormulaExternalId);
}
