using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;

internal static class DeliveryOrderDocumentQuery
{
    public static IQueryable<DeliveryOrderDocumentDto> Project(
        IQueryable<DeliveryOrder> orders, Guid id, Guid companyId)
        => orders.Where(x => companyId != Guid.Empty && x.Id == id && x.CompanyId == companyId && x.IsActive)
            .Select(x => new DeliveryOrderDocumentDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                CompanyName = x.Company.Name,
                CompanyAddress = x.Company.Address,
                CustomerName = x.Customer.CustomerName,
                CustomerAddress = x.Customer.RegistrationAddress,
                PaymentDeadline = x.PaymentDeadline,
                MerchandiseOrderExternalIds = string.Join(", ", x.DeliveryOrderPOs.Where(p => p.IsActive &&
                    p.MerchandiseOrder.CompanyId == companyId).Select(p => p.MerchandiseOrder.ExternalId).Distinct()),
                Status = x.Status,
                CreatedDate = x.CreatedDate,
                Receiver = x.Receiver,
                DeliveryAddress = x.DeliveryAddress,
                Phone = x.PhoneSnapshot != null && x.PhoneSnapshot.Trim() != "" ? x.PhoneSnapshot : x.Customer.Phone,
                TaxNumber = x.TaxNumber,
                PaymentType = x.PaymentType,
                Note = x.Note,
                Deliverers = x.Deliverers.OrderBy(d => d.DelivererInfor.Name)
                    .Select(d => d.DelivererInfor.Name).ToList(),
                Lines = x.Details.Where(d => d.IsActive).OrderBy(d => d.PONo)
                    .ThenBy(d => d.ProductExternalIdSnapShot).ThenBy(d => d.Id)
                    .Select(d => new DeliveryOrderDocumentLineDto
                    {
                        MerchandiseOrderDetailId = d.MerchandiseOrderDetailId,
                        ProductId = d.ProductId,
                        ProductCode = d.ProductExternalIdSnapShot,
                        ProductName = d.ProductNameSnapShot,
                        PONo = d.PONo,
                        Quantity = d.Quantity,
                        NumOfBags = d.NumOfBags,
                        IsAttach = d.IsAttach,
                        // Không đoán phân bổ của chuỗi nhiều lot lịch sử.
                        LotNo = d.LotConsumptions.Any(l => l.IsActive)
                            ? string.Join(", ", d.LotConsumptions.Where(l => l.IsActive)
                                .OrderBy(l => l.LotNo).Select(l => l.LotNo))
                            : d.LotNoList
                    }).ToList()
            });
}
