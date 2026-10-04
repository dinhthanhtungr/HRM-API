using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;

/// <summary>Tải chứng từ đã lưu; không cập nhật HasPrinted, lượng PO, trạng thái hoặc tồn kho.</summary>
public sealed record ExportDeliveryOrderQuery(Guid Id, bool Excel = false) : IRequest<DeliveryOrderFileDto?>;
