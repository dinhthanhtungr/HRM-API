using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.UpdateSaleOrder;

/// <summary>
/// Cập nhật từng phần thông tin header của SaleOrder; mã đơn, công ty và audit không nhận từ client.
/// Khi trạng thái chuyển sang Approved, command chuyển sang flow duyệt đơn chuyên biệt.
/// </summary>
public sealed record UpdateSaleOrderCommand(
    UpdateSaleOrderRequest Request) : IRequest<OperationResult>;
