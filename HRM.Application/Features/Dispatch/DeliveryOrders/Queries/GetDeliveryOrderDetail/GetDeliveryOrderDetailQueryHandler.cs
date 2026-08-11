using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.Deliverers.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;

internal sealed class GetDeliveryOrderDetailQueryHandler
    : IRequestHandler<GetDeliveryOrderDetailQuery, DeliveryOrderDetailDto?>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetDeliveryOrderDetailQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DeliveryOrderDetailDto?> Handle(
        GetDeliveryOrderDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty ||
            !DeliveryOrderAccessRules.CanRead(_currentUser) ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var canViewCost = DeliveryOrderCostVisibilityRules.CanViewCost(_currentUser);
        var canManage = DeliveryOrderAccessRules.CanManage(_currentUser);

        return await _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(x => x.Id == request.Id && x.CompanyId == companyId)
            .Select(x => new DeliveryOrderDetailDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                Status = x.Status == "Cancelled" ? "Canceled" : x.Status,
                CompanyId = x.CompanyId,
                CustomerId = x.CustomerId,
                CustomerExternalIdSnapshot = x.CustomerExternalIdSnapShot,
                CustomerName = x.Customer.CustomerName,
                MerchandiseOrderExternalIds = string.Join(", ",
                    x.DeliveryOrderPOs
                        .Where(po => po.IsActive)
                        .Select(po => po.MerchandiseOrder.ExternalId)
                        .Where(value => value != null && value != string.Empty)
                        .Distinct()),
                Receiver = x.Receiver,
                DeliveryAddress = x.DeliveryAddress,
                PaymentType = x.PaymentType,
                PaymentDeadline = x.PaymentDeadline,
                TaxNumber = x.TaxNumber,
                PhoneSnapshot = x.PhoneSnapshot,
                RequiresUnloading = x.RequiresUnloading,
                DeliveryPrice = x.DeliveryPrice,
                Note = x.Note,
                IsActive = x.IsActive,
                CreatedBy = x.CreatedBy,
                CreatedDate = x.CreatedDate,
                UpdatedBy = x.UpdatedBy,
                UpdatedDate = x.UpdatedDate,
                CanEdit = canManage && x.IsActive && x.Status == "Pending",
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
                    .ToList(),
                Deliverers = x.Deliverers
                    .OrderBy(d => d.DelivererInfor.Name)
                    .Select(d => new DelivererDto
                    {
                        Id = d.DelivererInforId,
                        Name = d.DelivererInfor.Name,
                        DelivererType = d.DelivererInfor.DelivererType,
                        Phone = d.DelivererInfor.Phone,
                        Note = d.DelivererInfor.Note,
                        IsActive = d.DelivererInfor.IsActive
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
