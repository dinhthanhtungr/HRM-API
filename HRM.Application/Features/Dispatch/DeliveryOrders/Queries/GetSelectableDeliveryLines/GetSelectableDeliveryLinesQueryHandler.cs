using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            .Where(d =>
                d.IsActive &&
                !d.IsAttach &&
                d.DeliveryOrder.IsActive &&
                d.DeliveryOrder.CompanyId == companyId &&
                d.MerchandiseOrderDetailId.HasValue)
            .GroupBy(d => d.MerchandiseOrderDetailId!.Value)
            .Select(g => new
            {
                MerchandiseOrderDetailId = g.Key,
                DeliveredQuantity = g.Sum(x => x.Quantity)
            });

        var lineBaseQuery =
            _dbContext.MerchandiseOrderDetails
                .AsNoTracking()
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
                .Select(x => new
                {
                    x.Order,
                    x.Detail,
                    x.DeliveredQuantity,
                    RemainingQuantity = x.Detail.ExpectedQuantity - x.DeliveredQuantity
                })
                .Where(x =>
                    x.Detail.IsActive &&
                    x.Order.CompanyId == companyId &&
                    x.RemainingQuantity > 0m);

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            lineBaseQuery = lineBaseQuery.Where(x => x.Order.CustomerId == customerId);
        }

        lineBaseQuery = lineBaseQuery.Where(x => x.Order.IsActive);

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            lineBaseQuery = lineBaseQuery.Where(x =>
                x.Order.ExternalId.Contains(keyword) ||
                x.Order.PONo.Contains(keyword) ||
                x.Order.CustomerNameSnapshot.Contains(keyword) ||
                x.Order.PhoneSnapshot.Contains(keyword) ||
                x.Detail.ProductExternalIdSnapshot.Contains(keyword) ||
                x.Detail.ProductNameSnapshot.Contains(keyword) ||
                (x.Detail.Comment ?? string.Empty).Contains(keyword));
        }

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
            })
            .ToListAsync(cancellationToken);

        var orderIds = pageOrders
            .Select(x => x.MerchandiseOrderId)
            .ToArray();

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
                Note = x.Detail.Comment,
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
                g => g.Select(x => new DeliveryLotOptionDto
                    {
                        LotNo = x.LotNo,
                        LotKey = x.LotNo,
                        StockType = "FinishedGood",
                        Quantity = Math.Min(x.AvailableQuantity, x.ProductAvailableQuantity),
                        Bags = null
                    })
                    .OrderBy(x => x.LotNo)
                    .ThenBy(x => x.LotKey)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var linesByOrder = pageLines
            .GroupBy(x => x.MerchandiseOrderId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(line =>
                    {
                        var lotOptions = stockLookup.TryGetValue(line.ProductExternalId, out var lots)
                            ? lots
                            : new List<DeliveryLotOptionDto>();

                        return new SelectableDeliveryOrderLineDto
                        {
                            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
                            ProductId = line.ProductId,
                            ProductExternalId = line.ProductExternalId,
                            ProductName = line.ProductName,
                            PONo = line.PONo,
                            Note = line.Note,
                            OrderedQuantity = line.OrderedQuantity,
                            DeliveredQuantity = line.DeliveredQuantity,
                            RemainingQuantity = Math.Max(0m, line.RemainingQuantity),
                            AvailableStockQuantity = lotOptions.Sum(x => x.Quantity),
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
