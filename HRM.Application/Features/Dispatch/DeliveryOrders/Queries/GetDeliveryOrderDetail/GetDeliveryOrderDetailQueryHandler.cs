using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.Deliverers.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;

internal sealed class GetDeliveryOrderDetailQueryHandler
    : IRequestHandler<GetDeliveryOrderDetailQuery, DeliveryOrderDetailDto?>
{
    private readonly IDispatchReadDbContext _dbContext;

    public GetDeliveryOrderDetailQueryHandler(IDispatchReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeliveryOrderDetailDto?> Handle(
        GetDeliveryOrderDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new DeliveryOrderDetailDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                Status = x.Status,
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
