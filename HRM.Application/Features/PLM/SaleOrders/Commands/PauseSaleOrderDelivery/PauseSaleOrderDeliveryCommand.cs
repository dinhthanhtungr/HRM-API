using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.PauseSaleOrderDelivery;

/// <summary>
/// Cập nhật trạng thái và khoảng thời gian tạm dừng giao hàng của một SaleOrder trong công ty hiện tại.
/// </summary>
public sealed record PauseSaleOrderDeliveryCommand(
    PauseSaleOrderDeliveryRequest Request) : IRequest<OperationResult<PauseSaleOrderDeliveryDto>>;
