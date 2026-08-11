using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Deliveries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderById;

/// <summary>
/// Trả chi tiết SaleOrder và các dòng active, kèm số lượng đã giao/còn lại được tổng hợp từ DeliveryOrder;
/// dữ liệu được giới hạn theo CompanyId và trạng thái pause hiệu lực được tính tại thời điểm đọc.
/// </summary>
internal sealed class GetSaleOrderByIdQueryHandler
    : IRequestHandler<GetSaleOrderByIdQuery, SaleOrderDetailDto?>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetSaleOrderByIdQueryHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<SaleOrderDetailDto?> Handle(
        GetSaleOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var delivered = _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(order =>
                order.CompanyId == companyId &&
                order.IsActive &&
                order.Status != DeliveryOrderStatus.Canceled.ToString() &&
                order.Status != "Cancelled")
            .SelectMany(order => order.Details)
            .Where(detail => detail.IsActive && !detail.IsAttach && detail.MerchandiseOrderDetailId.HasValue)
            .GroupBy(detail => detail.MerchandiseOrderDetailId!.Value)
            .Select(group => new
            {
                MerchandiseOrderDetailId = group.Key,
                Quantity = group.Sum(x => x.Quantity)
            });

        var dto = await _dbContext.MerchandiseOrders
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.MerchandiseOrderId == request.MerchandiseOrderId &&
                x.IsActive)
            .Select(x => new SaleOrderDetailDto
            {
                MerchandiseOrderId = x.MerchandiseOrderId,
                ExternalId = x.ExternalId,
                CustomerId = x.CustomerId,
                CustomerNameSnapshot = x.CustomerNameSnapshot,
                CustomerExternalIdSnapshot = x.CustomerExternalIdSnapshot,
                PhoneSnapshot = x.PhoneSnapshot,
                ManagerById = x.ManagerById,
                ManagerByNameSnapshot = x.ManagerByNameSnapshot,
                Receiver = x.Receiver,
                DeliveryAddress = x.DeliveryAddress,
                TotalPrice = x.TotalPrice,
                PaymentType = x.PaymentType,
                Vat = x.Vat,
                Status = x.Status,
                Currency = x.Currency,
                ExchangeRate = x.ExchangeRate,
                IsPaid = x.IsPaid,
                PaymentDate = x.PaymentDate,
                Note = x.Note,
                ShippingMethod = x.ShippingMethod,
                PONo = x.PONo,
                CreateDate = x.CreateDate,
                AttachmentCollectionId = x.AttachmentCollectionId,
                IsDeliveryPaused = x.IsDeliveryPaused,
                DeliveryPausedFrom = x.DeliveryPausedFrom,
                DeliveryPausedTo = x.DeliveryPausedTo,
                DeliveryPauseReason = x.DeliveryPauseReason,
                DeliveryPauseType = x.DeliveryPauseType,
                Lines = x.MerchandiseOrderDetails
                    .Where(detail => detail.IsActive)
                    .OrderBy(detail => detail.ExpectedDeliveryDate)
                    .Select(detail => new SaleOrderLineDto
                    {
                        MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                        ProductId = detail.ProductId,
                        ProductExternalIdSnapshot = detail.ProductExternalIdSnapshot,
                        ProductNameSnapshot = detail.ProductNameSnapshot,
                        FormulaId = detail.FormulaId,
                        FormulaExternalIdSnapshot = detail.FormulaExternalIdSnapshot,
                        ExpectedQuantity = detail.ExpectedQuantity,
                        DeliveredQuantity = delivered
                            .Where(row => row.MerchandiseOrderDetailId == detail.MerchandiseOrderDetailId)
                            .Select(row => row.Quantity)
                            .FirstOrDefault(),
                        RemainingQuantity = detail.ExpectedQuantity > delivered
                            .Where(row => row.MerchandiseOrderDetailId == detail.MerchandiseOrderDetailId)
                            .Select(row => row.Quantity)
                            .FirstOrDefault()
                                ? detail.ExpectedQuantity - delivered
                                    .Where(row => row.MerchandiseOrderDetailId == detail.MerchandiseOrderDetailId)
                                    .Select(row => row.Quantity)
                                    .FirstOrDefault()
                                : 0,
                        RealQuantity = detail.RealQuantity,
                        BagType = detail.BagType,
                        PackageWeight = detail.PackageWeight,
                        Status = detail.Status,
                        Comment = detail.Comment,
                        DeliveryRequestDate = detail.DeliveryRequestDate,
                        DeliveryActualDate = detail.DeliveryActualDate,
                        ExpectedDeliveryDate = detail.ExpectedDeliveryDate,
                        BaseCostSnapshot = detail.BaseCostSnapshot,
                        RecommendedUnitPrice = detail.RecommendedUnitPrice,
                        UnitPriceAgreed = detail.UnitPriceAgreed,
                        TotalPriceAgreed = detail.TotalPriceAgreed
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is not null)
        {
            SaleOrderStatusRules.ApplyEffectivePausedStatus(dto, _dateTimeProvider.Now);
        }

        return dto;
    }
}
