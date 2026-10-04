using HRM.Application.Abstractions.Persistence.Executive;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Dtos;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Shared;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.MerchandiseOrderPriceHistory;

/// <summary>
/// Returns company- and customer-scoped actual selling prices from eligible Merchandise Order lines.
/// </summary>
internal sealed class GetMerchandiseOrderPriceHistoryQueryHandler
    : IRequestHandler<GetMerchandiseOrderPriceHistoryQuery,
        OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>>
{
    private static readonly string[] SupportedSortFields = ["orderedAt", "unitPrice", "quantity"];
    private readonly IExecutiveReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetMerchandiseOrderPriceHistoryQueryHandler(
        IExecutiveReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>> Handle(
        GetMerchandiseOrderPriceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>.Fail(validationError);

        var companyId = _currentUser.CompanyId!.Value;
        var item = await ResolveItemAsync(request.ItemId, companyId, cancellationToken);
        if (item.Error is not null)
            return OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>.Fail(item.Error);

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var orders = _visibilityService.ApplyMerchandiseOrderVisibility(
            _dbContext.MerchandiseOrders.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var query = orders
            .Where(order =>
                MerchandiseOrderPriceHistoryRules.EligibleOrderTypes.Contains(order.OrderType) &&
                MerchandiseOrderPriceHistoryRules.EligibleStatuses.Contains(order.Status))
            .SelectMany(order => order.MerchandiseOrderDetails, (order, detail) => new { order, detail })
            .Where(x => x.detail.IsActive && x.detail.ProductId == item.ProductId)
            .Select(x => new MerchandiseOrderPriceHistoryRow
            {
                Id = x.detail.MerchandiseOrderDetailId,
                MerchandiseOrderId = x.order.MerchandiseOrderId,
                MerchandiseOrderCode = x.order.ExternalId,
                OrderedAt = x.order.CreateDate,
                OrderType = x.order.OrderType == OrderType.Merchandise
                    ? nameof(OrderType.Merchandise)
                    : x.order.OrderType == OrderType.SampleRequest
                        ? nameof(OrderType.SampleRequest)
                        : nameof(OrderType.Complaint),
                OrderStatus = x.order.Status,
                ItemId = x.detail.ProductId,
                ItemCode = x.detail.Product.Code ?? x.detail.ProductExternalIdSnapshot,
                ItemName = x.detail.Product.Name ?? x.detail.ProductNameSnapshot,
                Quantity = x.detail.ExpectedQuantity,
                Unit = x.detail.Product.Unit,
                UnitPrice = x.detail.UnitPriceAgreed,
                Currency = x.order.Currency,
                CustomerId = x.order.CustomerId,
                CustomerCode = x.order.CustomerExternalIdSnapshot,
                CustomerName = x.order.CustomerNameSnapshot,
                SaleEmployeeId = x.order.ManagerById,
                SaleName = x.order.ManagerByNameSnapshot
            });

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
            query = query.Where(x => x.CustomerId == customerId);
        if (request.NormalizedCurrency is { } currency)
            query = query.Where(x => x.Currency == currency);
        if (request.FromDate.HasValue)
            query = query.Where(x => x.OrderedAt >= request.FromDate.Value.Date);
        if (request.ToDate.HasValue)
            query = query.Where(x => x.OrderedAt < request.ToDate.Value.Date.AddDays(1));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplySorting(query, request)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new MerchandiseOrderPriceHistoryItemDto
            {
                Id = x.Id, MerchandiseOrderId = x.MerchandiseOrderId, MerchandiseOrderCode = x.MerchandiseOrderCode,
                OrderedAt = x.OrderedAt, OrderType = x.OrderType, OrderStatus = x.OrderStatus, ItemId = x.ItemId, ItemCode = x.ItemCode,
                ItemName = x.ItemName, Quantity = x.Quantity, Unit = x.Unit, UnitPrice = x.UnitPrice,
                Currency = x.Currency, CustomerId = x.CustomerId, CustomerCode = x.CustomerCode, CustomerName = x.CustomerName,
                SaleEmployeeId = x.SaleEmployeeId, SaleName = x.SaleName
            })
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>.Ok(
            new PagedResult<MerchandiseOrderPriceHistoryItemDto>(
                items, totalCount, request.NormalizedPageNumber, request.NormalizedPageSize));
    }

    internal static bool IsEligibleStatus(string status) => MerchandiseOrderPriceHistoryRules.IsEligibleStatus(status);
    internal static bool IsEligibleOrderType(OrderType orderType) =>
        MerchandiseOrderPriceHistoryRules.IsEligibleOrderType(orderType);

    private async Task<(Guid ProductId, string? Error)> ResolveItemAsync(
        Guid? itemId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var products = _dbContext.Products.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive);
        if (itemId is { } productId && productId != Guid.Empty)
            products = products.Where(x => x.ProductId == productId);

        var productIds = await products.Select(x => x.ProductId).Take(2).ToListAsync(cancellationToken);
        return productIds.Count switch
        {
            0 => (Guid.Empty, "itemId must identify an active product in the current company."),
            > 1 => (Guid.Empty, "itemId is not unique in the current company."),
            _ => (productIds[0], null)
        };
    }

    private static IOrderedQueryable<MerchandiseOrderPriceHistoryRow> ApplySorting(IQueryable<MerchandiseOrderPriceHistoryRow> query, GetMerchandiseOrderPriceHistoryQuery request)
        => request.NormalizedSortBy.ToLowerInvariant() switch
        {
            "unitprice" => request.SortDescending
                ? query.OrderByDescending(x => x.UnitPrice).ThenByDescending(x => x.OrderedAt).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.UnitPrice).ThenBy(x => x.OrderedAt).ThenBy(x => x.Id),
            "quantity" => request.SortDescending
                ? query.OrderByDescending(x => x.Quantity).ThenByDescending(x => x.OrderedAt).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Quantity).ThenBy(x => x.OrderedAt).ThenBy(x => x.Id),
            _ => request.SortDescending
                ? query.OrderByDescending(x => x.OrderedAt).ThenByDescending(x => x.MerchandiseOrderId).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.OrderedAt).ThenBy(x => x.MerchandiseOrderId).ThenBy(x => x.Id)
        };

    private string? Validate(GetMerchandiseOrderPriceHistoryQuery request)
    {
        if (!CanAccess(_currentUser)) return "Only President or Developer can access merchandise order price history.";
        if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId == Guid.Empty) return "Current company context is required.";
        if (request.ItemId is null || request.ItemId == Guid.Empty) return "itemId is required.";
        if (request.PageNumber < 1) return "pageNumber must be at least 1.";
        if (request.PageSize is < 1 or > 100) return "pageSize must be between 1 and 100.";
        if (!SupportedSortFields.Contains(request.NormalizedSortBy, StringComparer.OrdinalIgnoreCase)) return "sortBy must be one of: orderedAt, unitPrice, quantity.";
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date) return "fromDate cannot be later than toDate.";
        return null;
    }

    internal static bool CanAccess(ICurrentUser user) => user.IsInRole(ApplicationRoles.President) || user.IsInRole(ApplicationRoles.Developer);

    private sealed class MerchandiseOrderPriceHistoryRow
    {
        public Guid Id { get; init; } public Guid MerchandiseOrderId { get; init; } public string MerchandiseOrderCode { get; init; } = string.Empty;
        public DateTime OrderedAt { get; init; } public string OrderType { get; init; } = string.Empty; public string OrderStatus { get; init; } = string.Empty; public Guid ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty; public string ItemName { get; init; } = string.Empty; public decimal Quantity { get; init; }
        public string? Unit { get; init; } public decimal UnitPrice { get; init; } public string? Currency { get; init; } public Guid CustomerId { get; init; }
        public string CustomerCode { get; init; } = string.Empty; public string CustomerName { get; init; } = string.Empty; public Guid SaleEmployeeId { get; init; }
        public string SaleName { get; init; } = string.Empty;
    }
}
