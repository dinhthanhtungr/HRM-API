using HRM.Application.Abstractions.Persistence.Dispatch;
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

    public GetDeliveryOrdersQueryHandler(IDispatchReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<DeliveryOrderListItemDto>> Handle(
        GetDeliveryOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DeliveryOrders
            .AsNoTracking()
            .AsQueryable();

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

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
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
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

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                (x.ExternalId ?? string.Empty).Contains(keyword) ||
                (x.CustomerExternalIdSnapShot ?? string.Empty).Contains(keyword) ||
                (x.Customer.CustomerName ?? string.Empty).Contains(keyword) ||
                x.DeliveryOrderPOs.Any(po =>
                    (po.MerchandiseOrder.ExternalId ?? string.Empty).Contains(keyword) ||
                    (po.MerchandiseOrder.PONo ?? string.Empty).Contains(keyword)) ||
                x.Details.Any(d =>
                    (d.ProductExternalIdSnapShot ?? string.Empty).Contains(keyword) ||
                    (d.ProductNameSnapShot ?? string.Empty).Contains(keyword) ||
                    (d.PONo ?? string.Empty).Contains(keyword) ||
                    (d.LotNoList ?? string.Empty).Contains(keyword)));
        }

        query = ApplySorting(query, request);

        var projected = query
            .Select(x => new DeliveryOrderListItemDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                Status = x.Status,
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
                Note = x.Note,
                IsActive = x.IsActive,
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
                        LotNoList = d.LotNoList,
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
