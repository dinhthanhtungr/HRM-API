using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.DeleteDeliveryOrder;

public sealed record DeleteDeliveryOrderCommand(Guid Id) : IRequest<OperationResult>;
