using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CancelSaleOrder;

/// <summary>
/// Hủy SaleOrder trong công ty hiện tại và cập nhật các dữ liệu sản xuất liên quan theo trạng thái đơn.
/// </summary>
public sealed record CancelSaleOrderCommand(
    CancelSaleOrderRequest Request) : IRequest<OperationResult>;
