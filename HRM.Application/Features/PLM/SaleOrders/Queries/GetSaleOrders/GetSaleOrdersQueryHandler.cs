using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrders;

/// <summary>
/// Áp dụng CompanyId, bộ lọc và phân trang trên SaleOrder active; trạng thái Paused được tính theo
/// khoảng thời gian tạm dừng tại thời điểm truy vấn thay vì ghi đè trạng thái gốc trong database.
/// </summary>
internal sealed class GetSaleOrdersQueryHandler
    : IRequestHandler<GetSaleOrdersQuery, PagedResult<SaleOrderListItemDto>>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetSaleOrdersQueryHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PagedResult<SaleOrderListItemDto>> Handle(
        GetSaleOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var query = _dbContext.MerchandiseOrders
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive);

        if (request.MerchandiseOrderId is { } orderId && orderId != Guid.Empty)
        {
            query = query.Where(x => x.MerchandiseOrderId == orderId);
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (request.ManagerById is { } managerId && managerId != Guid.Empty)
        {
            query = query.Where(x => x.ManagerById == managerId);
        }

        if (request.From.HasValue)
        {
            var from = request.From.Value.Date;
            query = query.Where(x => x.CreateDate >= from);
        }

        if (request.To.HasValue)
        {
            var toExclusive = request.To.Value.Date.AddDays(1);
            query = query.Where(x => x.CreateDate < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(x =>
                x.CustomerNameSnapshot.Contains(keyword) ||
                x.CustomerExternalIdSnapshot.Contains(keyword) ||
                x.ExternalId.Contains(keyword) ||
                x.PONo.Contains(keyword) ||
                x.MerchandiseOrderDetails.Any(d =>
                    d.Product != null &&
                    (
                        (d.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                        (d.Product.Name ?? string.Empty).Contains(keyword) ||
                        d.Product.SampleRequests.Any(sampleRequest =>
                            sampleRequest.IsActive &&
                            sampleRequest.CompanyId == companyId &&
                            sampleRequest.ExternalId.Contains(keyword)) ||
                        d.Product.Formulas.Any(formula =>
                            formula.IsActive &&
                            formula.CompanyId == companyId &&
                            EF.Functions.ILike(formula.ExternalId, $"%{keyword}%"))) ||
                    EF.Functions.ILike(d.Formula.ExternalId, $"%{keyword}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.MerchandiseOrderId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new SaleOrderListItemDto
            {
                MerchandiseOrderId = x.MerchandiseOrderId,
                ExternalId = x.ExternalId,
                CustomerId = x.CustomerId,
                CustomerNameSnapshot = x.CustomerNameSnapshot,
                CustomerExternalIdSnapshot = x.CustomerExternalIdSnapshot,
                ManagerById = x.ManagerById,
                ManagerByNameSnapshot = x.ManagerByNameSnapshot,
                TotalPrice = x.TotalPrice,
                PaymentType = x.PaymentType,
                IsPaid = x.IsPaid,
                Status = x.Status,
                PONo = x.PONo,
                CreateDate = x.CreateDate,
                AttachmentCollectionId = x.AttachmentCollectionId,
                IsDeliveryPaused = x.IsDeliveryPaused,
                DeliveryPausedFrom = x.DeliveryPausedFrom,
                DeliveryPausedTo = x.DeliveryPausedTo,
                DeliveryPauseReason = x.DeliveryPauseReason,
                DeliveryPauseType = x.DeliveryPauseType
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            SaleOrderStatusRules.ApplyEffectivePausedStatus(item, _dateTimeProvider.Now);
        }

        return new PagedResult<SaleOrderListItemDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }
}
