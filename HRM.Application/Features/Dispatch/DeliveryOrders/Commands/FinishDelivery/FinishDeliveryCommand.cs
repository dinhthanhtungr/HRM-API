using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.FinishDelivery;

/// <summary>Xác nhận PO đang Delivering thành Delivered theo thao tác điều vận legacy.</summary>
public sealed record FinishDeliveryCommand(Guid MerchandiseOrderId) : IRequest<OperationResult>;
