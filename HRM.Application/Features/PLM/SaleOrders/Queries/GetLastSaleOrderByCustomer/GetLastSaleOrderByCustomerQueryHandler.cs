using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;

/// <summary>
/// Tìm dòng SaleOrder active mới nhất theo CustomerId và ProductId, loại trừ dòng đã hủy
/// và không trả dữ liệu thuộc công ty khác.
/// </summary>
internal sealed class GetLastSaleOrderByCustomerQueryHandler
    : IRequestHandler<GetLastSaleOrderByCustomerQuery, SaleOrderProductDefaultsDto?>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICustomerVisibilityService _customerVisibilityService;

    public GetLastSaleOrderByCustomerQueryHandler(
        ISaleOrderDbContext dbContext,
        ICustomerVisibilityService customerVisibilityService)
    {
        _dbContext = dbContext;
        _customerVisibilityService = customerVisibilityService;
    }

    public async Task<SaleOrderProductDefaultsDto?> Handle(
        GetLastSaleOrderByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return null;
        }

        var scope = await _customerVisibilityService.BuildScopeAsync(cancellationToken);
        var orderQuery = _customerVisibilityService.ApplyMerchandiseOrderVisibility(
            _dbContext.MerchandiseOrders.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        return await orderQuery
            .Where(order =>
                order.CustomerId == request.CustomerId &&
                order.IsActive)
            .SelectMany(order => order.MerchandiseOrderDetails, (order, detail) => new { order, detail })
            .Where(x =>
                x.detail.ProductId == request.ProductId &&
                x.detail.IsActive &&
                x.detail.Status != MerchadiseStatus.Cancelled.ToString())
            .OrderByDescending(x => x.order.CreateDate)
            .ThenByDescending(x => x.order.MerchandiseOrderId)
            .ThenByDescending(x => x.detail.MerchandiseOrderDetailId)
            .Select(x => new SaleOrderProductDefaultsDto
            {
                MerchandiseOrderId = x.order.MerchandiseOrderId,
                MerchandiseOrderDetailId = x.detail.MerchandiseOrderDetailId,
                ProductId = x.detail.ProductId,
                FormulaId = x.detail.FormulaId,
                BagType = x.detail.BagType,
                PackageWeight = x.detail.PackageWeight,
                ExpectedQuantity = x.detail.ExpectedQuantity,
                FormulaExternalIdSnapshot = x.detail.FormulaExternalIdSnapshot,
                Comment = x.detail.Comment,
                UnitPriceAgreed = x.detail.UnitPriceAgreed,
                CreateDate = x.order.CreateDate
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
