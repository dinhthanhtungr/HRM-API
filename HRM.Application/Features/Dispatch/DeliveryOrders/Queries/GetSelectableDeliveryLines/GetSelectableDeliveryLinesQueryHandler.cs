using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;

internal sealed class GetSelectableDeliveryLinesQueryHandler
    : IRequestHandler<GetSelectableDeliveryLinesQuery, PagedResult<SelectableDeliveryOrderDto>>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly DeliveryOrderLotInventoryService _inventoryService;

    public GetSelectableDeliveryLinesQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser,
        DeliveryOrderLotInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _inventoryService = inventoryService;
    }

    public async Task<PagedResult<SelectableDeliveryOrderDto>> Handle(
        GetSelectableDeliveryLinesQuery request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanRead(_currentUser) ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return new PagedResult<SelectableDeliveryOrderDto>(
                [],
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize);
        }

        var deliveredQuantityQuery = _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .CountedForAllocation(companyId)
            .GroupBy(d => d.MerchandiseOrderDetailId!.Value)
            .Select(g => new
            {
                MerchandiseOrderDetailId = g.Key,
                DeliveredQuantity = g.Sum(x => x.Quantity)
            });

        var lineBaseQuery =
            _dbContext.MerchandiseOrderDetails
                .AsNoTracking()
                .Where(detail =>
                    (detail.MerchandiseOrder.CustomerExternalIdSnapshot == null ||
                     detail.MerchandiseOrder.CustomerExternalIdSnapshot.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId) &&
                    (detail.MerchandiseOrder.Customer == null || detail.MerchandiseOrder.Customer.ExternalId == null ||
                     detail.MerchandiseOrder.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId))
                .GroupJoin(
                    deliveredQuantityQuery,
                    detail => detail.MerchandiseOrderDetailId,
                    delivered => delivered.MerchandiseOrderDetailId,
                    (detail, deliveredGroup) => new
                    {
                        Order = detail.MerchandiseOrder,
                        Detail = detail,
                        DeliveredQuantity = deliveredGroup
                            .Select(x => x.DeliveredQuantity)
                            .FirstOrDefault()
                    })
                .Select(x => new SelectableDeliveryLineSource
                {
                    Order = x.Order,
                    Detail = x.Detail,
                    DeliveredQuantity = x.DeliveredQuantity,
                    RemainingQuantity = x.Detail.ExpectedQuantity - x.DeliveredQuantity
                })
                .Where(x =>
                    x.Detail.IsActive &&
                    x.Order.CompanyId == companyId);

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            lineBaseQuery = lineBaseQuery.Where(x => x.Order.CustomerId == customerId);
        }

        lineBaseQuery = lineBaseQuery.Where(x => x.Order.IsActive);
        if (request.MerchandiseOrderId is { } selectedOrderId && selectedOrderId != Guid.Empty)
            lineBaseQuery = lineBaseQuery.Where(x => x.Order.MerchandiseOrderId == selectedOrderId);

        lineBaseQuery = SelectableDeliveryKeywordSearch.Apply(lineBaseQuery, request.Keyword, companyId);

        var orderQuery = lineBaseQuery
            .Select(x => x.Order)
            .Distinct()
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.MerchandiseOrderId);

        var totalCount = await orderQuery.CountAsync(cancellationToken);

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var pageOrders = await orderQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new
            {
                order.MerchandiseOrderId,
                order.ExternalId,
                order.PONo,
                order.CustomerId,
                order.CustomerNameSnapshot,
                order.CustomerExternalIdSnapshot,
                order.PhoneSnapshot,
                order.Receiver,
                order.DeliveryAddress,
                order.PaymentType,
                order.Status,
                order.Currency,
                order.Note
                ,order.IsDeliveryPaused, order.DeliveryPausedFrom, order.DeliveryPausedTo,
                order.DeliveryPauseReason, order.DeliveryPauseType,
                DeliveryPausedName = order.DeliveryPausedByNavigation != null && order.DeliveryPausedByNavigation.CompanyId == companyId
                    ? order.DeliveryPausedByNavigation.FullName : null
            })
            .ToListAsync(cancellationToken);

        var orderIds = pageOrders
            .Select(x => x.MerchandiseOrderId)
            .ToArray();

        var promisedDates = await DeliveryPlanSourceQuery.ActiveLinks(_dbContext.MfgOrderPOs.AsNoTracking(), companyId)
            .Where(x => orderIds.Contains(x.Detail.MerchandiseOrderId))
            .GroupBy(x => x.Detail.MerchandiseOrderId)
            .Select(g => new { OrderId = g.Key, Date = g.Max(x => x.ProductionOrder.ExpectedDate) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Date, cancellationToken);

        var labelNames = DeliveryCustomerLabelNames.Query(_dbContext.CustomerLabelDetails.AsNoTracking(), companyId);
        var pageLines = await lineBaseQuery
            .Where(x => orderIds.Contains(x.Order.MerchandiseOrderId))
            .OrderBy(x => x.Order.PONo)
            .ThenBy(x => x.Detail.ProductExternalIdSnapshot)
            .Select(x => new
            {
                x.Order.MerchandiseOrderId,
                x.Order.PONo,
                x.Detail.MerchandiseOrderDetailId,
                x.Detail.ProductId,
                ProductExternalId = x.Detail.ProductExternalIdSnapshot,
                ProductName = x.Detail.ProductNameSnapshot,
                ProductDisplayCode = x.Detail.Product != null && x.Detail.Product.CompanyId == companyId ? x.Detail.Product.ColourCode : null,
                ProductDisplayName = labelNames.Where(n => n.Header.CustomerId == x.Order.CustomerId && n.Header.ProductId == x.Detail.ProductId)
                    .Select(n => n.FieldValue!.Trim()).FirstOrDefault() ??
                    (x.Detail.Product != null && x.Detail.Product.CompanyId == companyId ? x.Detail.Product.Name : null),
                Note = x.Detail.Comment,
                BagWeightKg = x.Detail.Product != null && x.Detail.Product.IsActive && x.Detail.Product.CompanyId == companyId
                    ? x.Detail.Product.SampleRequests.Where(sr => sr.CompanyId == companyId && sr.IsActive &&
                        sr.CustomerId == x.Order.CustomerId)
                        .OrderByDescending(sr => sr.CreatedDate).ThenByDescending(sr => sr.SampleRequestId)
                        .Select(sr => sr.BagWeight > 0 ? (int?)sr.BagWeight : null).FirstOrDefault()
                    : null,
                OrderedQuantity = x.Detail.ExpectedQuantity,
                x.DeliveredQuantity,
                x.RemainingQuantity
            })
            .ToListAsync(cancellationToken);

        var inventory = await _inventoryService.LoadAsync(
            companyId,
            pageLines.Select(x => (x.ProductId, x.ProductExternalId)).ToArray(),
            includeCost: false,
            cancellationToken: cancellationToken);
        var stockLookup = inventory.Values
            .Where(x => x.AvailableQuantity > 0m && x.ProductAvailableQuantity > 0m)
            .GroupBy(x => x.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    AvailableQuantity = DeliveryOrderLotInventoryRules.CalculateAvailableQuantity(g),
                    Lots = g.Select(x => new DeliveryLotOptionDto
                    {
                        LotNo = x.LotNo,
                        LotKey = x.LotNo,
                        StockType = x.StockType.ToString(),
                        Quantity = Math.Min(x.AvailableQuantity, x.ProductAvailableQuantity),
                        Bags = null
                    })
                    .OrderBy(x => x.LotNo)
                    .ThenBy(x => x.LotKey)
                    .ToList()
                },
                StringComparer.OrdinalIgnoreCase);

        var linesByOrder = pageLines
            .GroupBy(x => x.MerchandiseOrderId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(line =>
                    {
                        var stock = stockLookup.GetValueOrDefault(line.ProductExternalId);
                        var lotOptions = stock?.Lots ?? new List<DeliveryLotOptionDto>();

                        return new SelectableDeliveryOrderLineDto
                        {
                            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
                            ProductId = line.ProductId,
                            ProductExternalId = line.ProductExternalId,
                            ProductName = line.ProductName,
                            ProductDisplayCode = line.ProductDisplayCode,
                            ProductDisplayName = line.ProductDisplayName,
                            BagWeightKg = line.BagWeightKg ?? 25,
                            PONo = line.PONo,
                            Note = line.Note,
                            OrderedQuantity = line.OrderedQuantity,
                            DeliveredQuantity = line.DeliveredQuantity,
                            RemainingQuantity = Math.Max(0m, line.RemainingQuantity),
                            AvailableStockQuantity = stock?.AvailableQuantity ?? 0m,
                            LotOptions = lotOptions
                        };
                    })
                    .ToList());

        var items = pageOrders
            .Select(order => new SelectableDeliveryOrderDto
            {
                MerchandiseOrderId = order.MerchandiseOrderId,
                ExternalId = order.ExternalId,
                PONo = order.PONo,
                CustomerId = order.CustomerId,
                CustomerNameSnapshot = order.CustomerNameSnapshot,
                CustomerExternalIdSnapshot = order.CustomerExternalIdSnapshot,
                PhoneSnapshot = order.PhoneSnapshot,
                Receiver = order.Receiver,
                DeliveryAddress = order.DeliveryAddress,
                PaymentType = order.PaymentType,
                Status = order.Status,
                Currency = order.Currency,
                Note = order.Note,
                PromisedDeliveryDate = promisedDates.GetValueOrDefault(order.MerchandiseOrderId),
                IsDeliveryPaused = order.IsDeliveryPaused, DeliveryPausedFrom = order.DeliveryPausedFrom,
                DeliveryPausedTo = order.DeliveryPausedTo, DeliveryPauseReason = order.DeliveryPauseReason,
                DeliveryPauseType = order.DeliveryPauseType, DeliveryPausedName = order.DeliveryPausedName,
                Lines = linesByOrder.TryGetValue(order.MerchandiseOrderId, out var lines)
                    ? lines
                    : Array.Empty<SelectableDeliveryOrderLineDto>()
            })
            .ToList();

        return new PagedResult<SelectableDeliveryOrderDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}
