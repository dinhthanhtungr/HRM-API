using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ConfirmSaleOrderDelivery;

/// <summary>
/// Xac nhan SaleOrder da giao du, chuyen header va cac dong active sang Delivered.
/// </summary>
public sealed record ConfirmSaleOrderDeliveryCommand(Guid MerchandiseOrderId) : IRequest<OperationResult>;
