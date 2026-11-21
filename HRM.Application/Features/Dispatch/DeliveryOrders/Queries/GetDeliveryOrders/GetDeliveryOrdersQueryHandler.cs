using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrders;

internal sealed class GetDeliveryOrdersQueryHandler
    : IRequestHandler<GetDeliveryOrdersQuery, PagedResult<DeliveryOrderListItemDto>>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissionService _permissionService;

    public GetDeliveryOrdersQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser,
        ICurrentUserPermissionService permissionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<PagedResult<DeliveryOrderListItemDto>> Handle(
        GetDeliveryOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanRead(_currentUser) ||
            _currentUser.CompanyId is not { } currentCompanyId ||
            currentCompanyId == Guid.Empty)
        {
            return new PagedResult<DeliveryOrderListItemDto>(
                [],
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize);
        }

        var canViewCost = DeliveryOrderCostVisibilityRules.CanViewCost(_permissionService);
        var canManage = DeliveryOrderAccessRules.CanManage(_currentUser);

        var query = _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(x => x.CompanyId == currentCompanyId);

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (request.DeliveryOrderId is { } deliveryOrderId && deliveryOrderId != Guid.Empty)
        {
            query = query.Where(x => x.Id == deliveryOrderId);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!DeliveryOrderLifecycleRules.TryNormalizeStatus(request.Status, out var status))
            {
                return EmptyResult(request);
            }

            var normalizedStatus = status.ToString();
            query = status == HRM.Domain.Enums.Deliveries.DeliveryOrderStatus.Canceled
                ? query.Where(x => x.Status == normalizedStatus || x.Status == "Cancelled")
                : query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.PONo))
        {
            var poNo = request.PONo.Trim();
            query = query.Where(x =>
                x.DeliveryOrderPOs.Any(po =>
                    po.IsActive &&
                    (po.MerchandiseOrder.PONo ?? string.Empty).Contains(poNo)) ||
                x.Details.Any(detail =>
                    detail.IsActive &&
                    (detail.PONo ?? string.Empty).Contains(poNo)));
        }

        if (!string.IsNullOrWhiteSpace(request.LotNo))
        {
            var lotNo = request.LotNo.Trim();
            query = query.Where(x => x.Details.Any(detail =>
                detail.IsActive &&
                (detail.LotConsumptions.Any(lot =>
                     lot.IsActive && lot.LotNo.Contains(lotNo)) ||
                 (!detail.LotConsumptions.Any(lot => lot.IsActive) &&
                  (detail.LotNoList ?? string.Empty).Contains(lotNo)))));
        }

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
        {
            return EmptyResult(request);
        }

        if (request.From.HasValue)
        {
            var fromDate = request.From.Value.Date;
            query = query.Where(x => x.CreatedDate >= fromDate);
        }

        if (request.To.HasValue)
        {
            var toDateExclusive = request.To.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedDate < toDateExclusive);
        }

        var smartSearch = HRM.Application.Commons.Searching.SmartSearch.For(query, fields => [
            fields.Code("deliveryOrderCode", x => x.ExternalId, "Mã phiếu giao", "Delivery order"),
            fields.Code("customerCode", x => x.CustomerExternalIdSnapShot, "Mã KH", "Customer code"),
            fields.Name("customerName", x => x.Customer.CustomerName, "Tên KH", "Customer name"),
            fields.CustomerTaxNumber(x => x.Customer.CompanyId == currentCompanyId ? x.Customer.TaxNumber : null),
            fields.Codes("saleOrderCode", x => x.DeliveryOrderPOs.Where(p => p.MerchandiseOrder.CompanyId == currentCompanyId),
                p => p.MerchandiseOrder.ExternalId, "Mã đơn hàng", "Order code"),
            fields.Codes("purchaseOrderNumber", x => x.DeliveryOrderPOs.Where(p => p.MerchandiseOrder.CompanyId == currentCompanyId),
                p => p.MerchandiseOrder.PONo, "Số PO", "PO"),
            fields.Codes("colourCode", x => x.Details.Where(d => d.Product != null && d.Product.CompanyId == currentCompanyId),
                d => d.Product!.ColourCode, "ColorCode", "Mã màu"),
            fields.Names("productName", x => x.Details, d => d.ProductNameSnapShot, "Tên sản phẩm", "Product name")
        ]);
        query = smartSearch.ApplyRequest(query, request.NormalizedKeyword, request, out var smartKeyword);
        if (!string.IsNullOrWhiteSpace(smartKeyword))
        {
            var keyword = smartKeyword;
            query = query.Where(x =>
                EF.Functions.ILike((x.ExternalId ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.CustomerExternalIdSnapShot ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Customer.CustomerName ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                x.Customer.CompanyId == x.CompanyId && EF.Functions.ILike(x.Customer.TaxNumber ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                x.DeliveryOrderPOs.Any(po =>
                    EF.Functions.ILike((po.MerchandiseOrder.ExternalId ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike((po.MerchandiseOrder.PONo ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                x.Details.Any(d =>
                    (d.Product != null && d.Product.CompanyId == x.CompanyId &&
                        (EF.Functions.ILike(d.Product.ColourCode ?? string.Empty, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                         EF.Functions.ILike(d.Product.Name ?? string.Empty, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter))) ||
                    EF.Functions.ILike((d.ProductExternalIdSnapShot ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike((d.ProductNameSnapShot ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike((d.PONo ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                    d.LotConsumptions.Any(lot =>
                        lot.IsActive && EF.Functions.ILike(lot.LotNo, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                    (!d.LotConsumptions.Any(lot => lot.IsActive) &&
                     EF.Functions.ILike((d.LotNoList ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter))));
        }

        if (await smartSearch.SuggestionsAsync<DeliveryOrderListItemDto>(query, request, cancellationToken) is { } suggestions)
            return suggestions;
        query = ApplySorting(query, request);

        var labelNames = HRM.Application.Features.Dispatch.DeliveryOrders.Services.DeliveryCustomerLabelNames.Query(
            _dbContext.CustomerLabelDetails.AsNoTracking(), currentCompanyId);
        var projected = query
            .Select(x => new DeliveryOrderListItemDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                Status = x.Status == "Cancelled" ? "Canceled" : x.Status,
                CustomerId = x.CustomerId,
                CustomerExternalIdSnapshot = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                MerchandiseOrderExternalIds = string.Join(", ",
                    x.DeliveryOrderPOs
                        .Where(po => po.IsActive)
                        .Select(po => po.MerchandiseOrder.ExternalId)
                        .Where(value => value != null && value != string.Empty)
                        .Distinct()),
                DelivererNames = string.Join(", ",
                    x.Deliverers
                        .Select(deliverer => deliverer.DelivererInfor.Name)
                        .Where(value => value != string.Empty)
                        .Distinct()),
                PaymentDeadline = x.PaymentDeadline,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate,
                Note = x.Note,
                IsActive = x.IsActive,
                HasPrinted = x.HasPrinted,
                CanEdit = canManage && x.IsActive && x.Status == "Pending" && !x.HasPrinted,
                LineCount = x.Details.Count(d => d.IsActive && !d.IsAttach),
                TotalQuantity = x.Details
                    .Where(d => d.IsActive && !d.IsAttach)
                    .Sum(d => d.Quantity),
                TotalNumOfBags = x.Details
                    .Where(d => d.IsActive && !d.IsAttach)
                    .Sum(d => d.NumOfBags),
                Lines = x.Details
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.PONo)
                    .ThenBy(d => d.ProductExternalIdSnapShot)
                    .Select(d => new DeliveryOrderLineDto
                    {
                        Id = d.Id,
                        MerchandiseOrderDetailId = d.MerchandiseOrderDetailId,
                        ProductId = d.ProductId,
                        ProductExternalId = d.ProductExternalIdSnapShot,
                        ProductName = d.ProductNameSnapShot,
                        ProductDisplayCode = d.Product != null && d.Product.CompanyId == x.CompanyId ? d.Product.ColourCode : null,
                        ProductDisplayName = labelNames.Where(n => n.Header.CustomerId == x.CustomerId && n.Header.ProductId == d.ProductId)
                            .Select(n => n.FieldValue!.Trim()).FirstOrDefault() ??
                            (d.Product != null && d.Product.CompanyId == x.CompanyId ? d.Product.Name : null),
                        LotNoList = d.LotConsumptions.Any(lot => lot.IsActive)
                            ? string.Join(", ", d.LotConsumptions
                                .Where(lot => lot.IsActive)
                                .OrderBy(lot => lot.LotNo)
                                .Select(lot => lot.LotNo))
                            : d.LotNoList,
                        Lots = d.LotConsumptions
                            .Where(lot => lot.IsActive)
                            .OrderBy(lot => lot.LotNo)
                            .Select(lot => new DeliveryOrderLotDto
                            {
                                LotNo = lot.LotNo,
                                Quantity = lot.Quantity,
                                UnitCostSnapshot = canViewCost ? lot.UnitCostSnapshot : null,
                                TotalCostSnapshot = canViewCost ? lot.TotalCostSnapshot : null
                            })
                            .ToList(),
                        PONo = d.PONo,
                        Quantity = d.Quantity,
                        NumOfBags = d.NumOfBags,
                        IsAttach = d.IsAttach
                    })
                    .ToList()
            });

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }

    private static PagedResult<DeliveryOrderListItemDto> EmptyResult(
        GetDeliveryOrdersQuery request)
        => new(
            [],
            0,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);

    // ======================================== Helper Methods ========================================
    private static IQueryable<DeliveryOrder> ApplySorting(
        IQueryable<DeliveryOrder> query,
        GetDeliveryOrdersQuery request)
    {
        return request.NormalizedSortBy switch
        {
            DeliveryOrderSortFields.ExternalId => request.SortDescending
                ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.Status => request.SortDescending
                ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Status).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.CustomerExternalId => request.SortDescending
                ? query.OrderByDescending(x => x.CustomerExternalIdSnapShot).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CustomerExternalIdSnapShot).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.CustomerName => request.SortDescending
                ? query.OrderByDescending(x => x.Customer.CustomerName).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Customer.CustomerName).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.Receiver => request.SortDescending
                ? query.OrderByDescending(x => x.Receiver).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Receiver).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.PaymentDeadline => request.SortDescending
                ? query.OrderByDescending(x => x.PaymentDeadline).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.PaymentDeadline).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.UpdatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.UpdatedDate).ThenByDescending(x => x.Id),

            DeliveryOrderSortFields.CreatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedDate).ThenByDescending(x => x.Id),

            _ => query
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
        };
    }
}
