using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ApproveSaleOrder;

/// <summary>
/// Duyệt SaleOrder và yêu cầu tạo lệnh sản xuất cho các dòng hàng hợp lệ trong cùng công ty.
/// </summary>
public sealed record ApproveSaleOrderCommand(
    Guid MerchandiseOrderId) : IRequest<OperationResult>;
