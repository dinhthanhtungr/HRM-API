using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PrintDeliveryOrder;

/// <summary>In PDF và ghi nhận lượng giao vào PO đúng một lần trong company hiện tại.</summary>
public sealed record PrintDeliveryOrderCommand(Guid Id) : IRequest<OperationResult<DeliveryOrderFileDto>>;
